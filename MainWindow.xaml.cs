using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ScheduleTimer
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly List<PeriodTask> _taskQueue = new();
        private PeriodTask? _currentTask;
        private int _elapsedSeconds;
        private RunState _state = RunState.Idle;
        private string _scheduleName = "Нет данных";

        // За сколько секунд до конца периода проигрывается предупреждающий сигнал
        private const int BeforeFinishLeadSeconds = 5;

        // --- Циферблат из рисок ---
        private const int TickCount = 240;
        private readonly Line[] _ticks = new Line[TickCount];
        private readonly Brush _dimBrush = CreateFrozenBrush(Color.FromRgb(0x36, 0x3B, 0x45));
        private Brush _activeBrush = CreateFrozenBrush(Color.FromRgb(0x00, 0xBF, 0xFF));
        private DropShadowEffect? _tickGlow;

        // --- Звук тиканья: один «тик» из встроенной записи, проигрывается раз в
        //     секунду по таймеру — то есть синхронно с обновлением цифр секунд ---
        private SoundPlayer? _tickPlayer;        // тихий — обычный ход
        private SoundPlayer? _tickStrongPlayer;  // громкий — последние 5 секунд
        private bool _tickEnabled = true;        // тиканье вкл/выкл (кнопка в заголовке)
        private const string SoundOnGlyph = "";   // Segoe MDL2: Volume
        private const string SoundOffGlyph = "";  // Segoe MDL2: Mute

        private static SolidColorBrush CreateFrozenBrush(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public MainWindow()
        {
            InitializeComponent();
            _timer.Tick += Timer_Tick;

            ApplyLocalization();
            LoadTickSound();

            // Версия из метаданных сборки (задаётся <Version> в .csproj)
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            if (v != null) LblVersion.Text = $"v{v.Major}.{v.Minor}.{v.Build}";

            // Риски строятся в DialArea_SizeChanged, когда известен реальный размер области.
            Loaded += MainWindow_Loaded;
        }

        // Подставляет строки интерфейса по языку ОС (см. Loc).
        private void ApplyLocalization()
        {
            Title = Loc.Get("app_title");
            LblCaption.Text = "⏱  " + Loc.Get("app_title");
            LblTitle.Text = Loc.Get("loading");

            string space = Loc.Get("key_space");
            BtnPlay.ToolTip = $"{Loc.Get("tip_play")} ({space})";
            BtnPause.ToolTip = $"{Loc.Get("tip_pause")} ({space})";
            BtnStop.ToolTip = $"{Loc.Get("tip_stop")} (S)";
            BtnSound.ToolTip = $"{Loc.Get("tip_sound")} (M)";
            BtnMinimize.ToolTip = Loc.Get("tip_minimize");
            BtnClose.ToolTip = Loc.Get("tip_close");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSchedule();
        }

        // Пересчитывает размер циферблата под доступную площадь (квадрат) и строит риски.
        private void DialArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double side = Math.Min(e.NewSize.Width, e.NewSize.Height);
            if (side <= 0) return;

            TicksCanvas.Width = side;
            TicksCanvas.Height = side;
            BuildTicks();

            // Восстановить текущую картинку после перестроения рисок
            if (_currentTask != null)
                UpdateColor();
            else
                UpdateTicks(0);
        }

        private void LoadTickSound()
        {
            if (TickSounds.FromEmbedded("zvuk-chasov.mp3") is { } tick)
            {
                _tickPlayer = tick.quiet;
                _tickStrongPlayer = tick.loud;
            }
        }

        // Генерирует 60 рисок по кругу (как на циферблате часов).
        // Каждая 5-я — «часовая», длиннее и толще. Все стартуют приглушёнными.
        private void BuildTicks()
        {
            TicksCanvas.Children.Clear();

            double side = TicksCanvas.Width;
            double cx = side / 2, cy = side / 2;
            double rOuter = cx - side * 0.02;            // внешний край рисок
            double hourLen = side * 0.050;              // длина «часовой» риски
            double minorLen = side * 0.030;             // длина обычной риски

            for (int i = 0; i < TickCount; i++)
            {
                bool isHour = i % (TickCount / 12) == 0; // 12 длинных «часовых» меток
                double rInner = rOuter - (isHour ? hourLen : minorLen);
                double angle = i * (2 * Math.PI / TickCount); // 0 — сверху, по часовой

                double sin = Math.Sin(angle), cos = Math.Cos(angle);
                var line = new Line
                {
                    X1 = cx + rInner * sin,
                    Y1 = cy - rInner * cos,
                    X2 = cx + rOuter * sin,
                    Y2 = cy - rOuter * cos,
                    Stroke = _dimBrush,
                    StrokeThickness = isHour ? side * 0.006 : side * 0.0035, // тоньше
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

                _ticks[i] = line;
                TicksCanvas.Children.Add(line);
            }
        }

        // Подсвечивает риски пропорционально прогрессу (растёт от верха по часовой).
        private void UpdateTicks(double progress)
        {
            int active = (int)Math.Round(progress * TickCount);
            if (active < 0) active = 0;
            if (active > TickCount) active = TickCount;

            for (int i = 0; i < TickCount; i++)
            {
                bool on = i < active;
                _ticks[i].Stroke = on ? _activeBrush : _dimBrush;
                _ticks[i].Effect = on ? _tickGlow : null;
            }
        }

        private void LoadSchedule()
        {
            try
            {
                var jsonPath = System.IO.Path.Combine(AppContext.BaseDirectory, "config.json");
                if (!File.Exists(jsonPath))
                {
                    LblTitle.Text = Loc.Get("err_no_config");
                    return;
                }

                var json = File.ReadAllText(jsonPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var config = JsonSerializer.Deserialize<ScheduleConfig>(json, options);
                var active = config?.Schedules?.FirstOrDefault(s => s.Active);

                // Всегда начинаем с пустой очереди — LoadSchedule может вызываться повторно
                _taskQueue.Clear();

                if (active != null)
                {
                    _scheduleName = active.Name;
                    LblTitle.Text = active.Name;
                    BuildQueue(active.Periods);
                }
                else
                {
                    LblTitle.Text = Loc.Get("err_no_active");
                }

                UpdatePhaseText(); // показать «текущий → следующий» и в покое
            }
            catch (Exception ex)
            {
                LblTitle.Text = $"{Loc.Get("err_prefix")}: {ex.Message}";
            }
        }

        // Рекурсивно распаковывает вложенные периоды в плоскую очередь задач.
        // Помодоро: вложенные периоды (например «Отдых») ставятся МЕЖДУ повторами
        // родителя — Работа → Отдых → Работа → Отдых → Работа (после последнего
        // повтора отдыха нет).
        private void BuildQueue(List<Period> periods)
        {
            foreach (var p in periods)
            {
                for (int i = 0; i < p.Repeat; i++)
                {
                    // Подготовка — только перед первым повтором, дальше не повторяется
                    if (p.Prepare > 0 && i == 0)
                        _taskQueue.Add(new PeriodTask(p, true, p.Prepare, 0, i + 1, p.Repeat));
                    _taskQueue.Add(new PeriodTask(p, false, p.Duration, 0, i + 1, p.Repeat));

                    // Вложенные периоды — между повторами (после последнего не нужны)
                    if (i < p.Repeat - 1)
                        BuildQueue(p.Periods);
                }
            }
        }

        private void StartNextTask()
        {
            if (_taskQueue.Count == 0) return;

            _currentTask = _taskQueue[0];
            _elapsedSeconds = 0;
            _state = _currentTask.IsPrepare ? RunState.Prepare : RunState.Working;

            UpdateColor();
            SoundHelper.Play(_currentTask.Period.Sound.Start);
            UpdateUI();
            UpdateButtonStates();
        }

        // Подсвечивает активную кнопку по текущему состоянию.
        private void UpdateButtonStates()
        {
            BtnPlay.Tag = _state is RunState.Working or RunState.Prepare ? "active" : null;
            BtnPause.Tag = _state == RunState.Paused ? "active" : null;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_state == RunState.Paused || _currentTask == null) return;

            _elapsedSeconds++;
            _currentTask = _currentTask with { ElapsedSeconds = _elapsedSeconds };
            int remaining = _currentTask.TotalSeconds - _elapsedSeconds;

            // Триггеры звуков (стартовый сигнал уже проигран в StartNextTask)
            if (remaining == BeforeFinishLeadSeconds && !string.IsNullOrEmpty(_currentTask.Period.Sound.BeforeFinish))
                SoundHelper.Play(_currentTask.Period.Sound.BeforeFinish);
            if (remaining == 0)
            {
                SoundHelper.Play(_currentTask.Period.Sound.Finish);
                _taskQueue.RemoveAt(0);

                if (_taskQueue.Count > 0) StartNextTask();
                else
                {
                    Stop();
                    LblTitle.Text = $"{_scheduleName} ✅";
                }
                return;
            }

            // Тик раз в секунду — синхронно с обновлением цифр. В последние 5 c
            // периода (если он ≥5 c) звучит громкий вариант.
            if (_tickEnabled)
            {
                bool lastFive = _currentTask.TotalSeconds >= 5 && remaining <= BeforeFinishLeadSeconds;
                (lastFive ? _tickStrongPlayer : _tickPlayer)?.Play();
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_currentTask == null) return;

            int remaining = _currentTask.TotalSeconds - _elapsedSeconds;
            int hrs = remaining / 3600;
            int mins = (remaining % 3600) / 60;
            int secs = remaining % 60;

            // Двоеточия — отдельные блоки фикс. размера (в XAML), их не трогаем.
            TxtHours.Text = hrs > 0 ? $"{hrs}" : "";
            ColonH.Visibility = hrs > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtMinutes.Text = $"{mins:D2}";
            TxtSeconds.Text = $"{secs:D2}";

            // Крупным делаем самый значимый разряд: секунды (< 1 мин) или минуты.
            bool isLessThanMinute = remaining < 60;
            const double large = 80, small = 32;
            TxtSeconds.FontSize = isLessThanMinute ? large : small;
            TxtMinutes.FontSize = isLessThanMinute ? small : large;
            TxtHours.FontSize = small;

            // Циферблат заполняется по мере прогресса (сверху по часовой)
            double progress = (double)_elapsedSeconds / _currentTask.TotalSeconds;
            UpdateTicks(progress);

            UpdatePhaseText();
        }

        // Текст под часами: «{текущее}  →  {следующее}». Отражает текущий момент и
        // показывается всегда — в игре, на паузе и в покое. Примеры:
        //   Подготовка → Работа
        //   Работа [1/3] → Отдых
        //   Отдых → Работа
        //   Работа [3/3] → Финиш
        private void UpdatePhaseText()
        {
            // В покое «текущий» — первая задача очереди (то, что запустится).
            var current = _currentTask ?? _taskQueue.FirstOrDefault();
            if (current == null) { LblPhase.Text = ""; return; }

            string currentName, nextName;
            if (current.IsPrepare)
            {
                // Подготовка — отдельная фаза; за ней идёт работа того же периода.
                currentName = Loc.Get("prepare");
                nextName = current.Period.Name;
            }
            else
            {
                string counter = current.TotalRepeat > 1
                    ? $" [{current.CurrentRepeat}/{current.TotalRepeat}]"
                    : "";
                currentName = $"{current.Period.Name}{counter}";

                // Следующий период = ближайшая задача другого периода; иначе конец.
                var next = _taskQueue.FirstOrDefault(t => t.Period != current.Period);
                nextName = next?.Period.Name ?? Loc.Get("finish");
            }

            LblPhase.Text = $"{currentName} → {nextName}";
        }

        private void UpdateColor()
        {
            int c = _currentTask!.Period.Color;
            var color = Color.FromArgb(0xFF, (byte)(c >> 16), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF));

            // Активные риски и их свечение подхватывают цвет периода из config.json
            _activeBrush = CreateFrozenBrush(color);
            _tickGlow = new DropShadowEffect { Color = color, BlurRadius = 14, ShadowDepth = 0, Opacity = 1.0 };
            _tickGlow.Freeze();

            UpdateTicks((double)_elapsedSeconds / _currentTask.TotalSeconds);
        }

        // --- Кастомная рамка окна ---
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        // Доля радиуса, начиная с которой клик считается «по кольцу рисок».
        // Риски занимают внешние ~10%; берём чуть шире для удобного попадания.
        private const double DialRingInnerFraction = 0.80;

        // Клик по КОЛЬЦУ рисок «подматывает» время пропорционально углу.
        // Клик в центре (по тексту) или вне круга не гасим — окно перетаскивается.
        private void Dial_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            double side = TicksCanvas.Width;
            if (side <= 0) return;

            var pos = e.GetPosition(TicksCanvas);
            double cx = side / 2, cy = side / 2;
            double dx = pos.X - cx, dy = pos.Y - cy;
            double r = Math.Sqrt(dx * dx + dy * dy);
            if (r > cx || r < cx * DialRingInnerFraction) return; // не по кольцу → тянем окно

            e.Handled = true;                 // по кольцу — окно не тянем
            if (_currentTask == null) return; // нет активного периода — мотать нечего

            double angle = Math.Atan2(dx, -dy); // 0 — сверху, по часовой
            if (angle < 0) angle += 2 * Math.PI;
            double progress = angle / (2 * Math.PI);

            int newElapsed = (int)Math.Round(progress * _currentTask.TotalSeconds);
            newElapsed = Math.Clamp(newElapsed, 0, Math.Max(0, _currentTask.TotalSeconds - 1));

            _elapsedSeconds = newElapsed;
            _currentTask = _currentTask with { ElapsedSeconds = newElapsed };
            UpdateUI();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnSound_Click(object sender, RoutedEventArgs e)
        {
            _tickEnabled = !_tickEnabled;
            BtnSound.Content = _tickEnabled ? SoundOnGlyph : SoundOffGlyph;
            BtnSound.Foreground = _tickEnabled
                ? CreateFrozenBrush(Color.FromRgb(0x8A, 0x90, 0x9C))
                : CreateFrozenBrush(Color.FromRgb(0x55, 0x5A, 0x64));
        }

        // --- Управление ---
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_state == RunState.Idle && _taskQueue.Count > 0)
            {
                StartNextTask();
                _timer.Start();
            }
            else if (_state == RunState.Paused)
            {
                Resume();
            }
            UpdateButtonStates();
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (_state is RunState.Working or RunState.Prepare)
            {
                _state = RunState.Paused;
                _timer.Stop();
            }
            else if (_state == RunState.Paused)
            {
                Resume(); // повторное нажатие снимает с паузы
            }
            UpdateButtonStates();
        }

        // Возобновление с паузы: восстановить фазу и запустить таймер.
        private void Resume()
        {
            _state = _currentTask?.IsPrepare == true ? RunState.Prepare : RunState.Working;
            _timer.Start();
        }

        // Пробел — старт/пауза, S — стоп, M — звук. Preview, чтобы Пробел не
        // «нажимал» сфокусированную кнопку.
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space: TogglePlayPause(); break;
                case Key.S: Stop(); break;
                case Key.M: BtnSound_Click(this, new RoutedEventArgs()); break;
                default: return;
            }
            e.Handled = true;
        }

        // Единый тоггл для Пробела: идёт — на паузу, иначе — старт/продолжить.
        private void TogglePlayPause()
        {
            if (_state is RunState.Working or RunState.Prepare)
                BtnPause_Click(this, new RoutedEventArgs());
            else
                BtnPlay_Click(this, new RoutedEventArgs());
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => Stop();

        private void Stop()
        {
            _timer.Stop();
            _state = RunState.Idle;
            _taskQueue.Clear();
            _currentTask = null;
            _elapsedSeconds = 0;

            UpdateTicks(0); // все риски гаснут
            TxtHours.Text = ""; ColonH.Visibility = Visibility.Collapsed;
            TxtMinutes.Text = "00"; TxtSeconds.Text = "00";
            UpdateButtonStates();
            LoadSchedule(); // перечитывает JSON и обновляет текст фазы (в т.ч. в покое)
        }
    }

    public record PeriodTask(Period Period, bool IsPrepare, int TotalSeconds, int ElapsedSeconds, int CurrentRepeat, int TotalRepeat);
    public enum RunState { Idle, Prepare, Working, Paused }
}