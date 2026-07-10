using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // --- Звук тиканья часов (зацикленный zvuk-chasov.mp3) ---
        private ClockSound? _clock;
        private bool _tickEnabled = true;
        private const float TickBaseVolume = 0.30f;  // тихо во время хода
        private const float TickLoudVolume = 0.85f;  // громче в последние 5 секунд
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

            LoadTickSound();
            // Риски строятся в DialArea_SizeChanged, когда известен реальный размер области.
            Loaded += MainWindow_Loaded;
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
            _clock = ClockSound.TryCreate("zvuk-chasov.mp3");
            if (_clock != null) _clock.Volume = TickBaseVolume;
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
                    LblTitle.Text = "Файл config.json не найден";
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
                    LblTitle.Text = "Нет активных расписаний";
                }
            }
            catch (Exception ex)
            {
                LblTitle.Text = $"Ошибка: {ex.Message}";
            }
        }

        // Рекурсивно распаковывает вложенные периоды в плоскую очередь задач.
        // Помодоро: вложенные периоды (например «Отдых») выполняются ПОСЛЕ КАЖДОГО
        // повтора родителя, то есть чередуются: Работа → Отдых → Работа → Отдых → ...
        private void BuildQueue(List<Period> periods)
        {
            foreach (var p in periods)
            {
                for (int i = 0; i < p.Repeat; i++)
                {
                    if (p.Prepare > 0)
                        _taskQueue.Add(new PeriodTask(p, true, p.Prepare, 0, i + 1, p.Repeat));
                    _taskQueue.Add(new PeriodTask(p, false, p.Duration, 0, i + 1, p.Repeat));

                    BuildQueue(p.Periods); // вложенные периоды — после каждого повтора
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

            // Громкость тиканья: тихо во время хода, громче в последние 5 c периода (≥5 c)
            if (_clock != null)
            {
                bool lastFive = _currentTask.TotalSeconds >= 5 && remaining <= BeforeFinishLeadSeconds;
                _clock.Volume = !_tickEnabled ? 0f : (lastFive ? TickLoudVolume : TickBaseVolume);
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

            TxtHours.Text = hrs > 0 ? $"{hrs}:" : "";
            TxtMinutes.Text = $"{mins:D2}";
            TxtSeconds.Text = $":{secs:D2}";

            bool isLessThanMinute = remaining < 60;
            double large = 80, small = 32;

            if (isLessThanMinute)
            {
                TxtSeconds.FontSize = large;
                TxtMinutes.FontSize = small;
                TxtHours.FontSize = small;
            }
            else
            {
                TxtMinutes.FontSize = large;
                TxtSeconds.FontSize = small;
                TxtHours.FontSize = small;
            }

            // Циферблат заполняется по мере прогресса (сверху по часовой)
            double progress = (double)_elapsedSeconds / _currentTask.TotalSeconds;
            UpdateTicks(progress);

            // Имя периода (Работа/Отдых) уже говорит о фазе — не дублируем словом.
            string prep = _currentTask.IsPrepare ? " · Подготовка" : "";
            string rep = _currentTask.TotalRepeat > 1
                ? $" · {_currentTask.CurrentRepeat}/{_currentTask.TotalRepeat}"
                : "";
            LblPhase.Text = $"{_currentTask.Period.Name}{prep}{rep}";
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

        // Клик по кругу циферблата «подматывает» время пропорционально углу.
        // За пределами круга событие не гасим — тогда окно перетаскивается как обычно.
        private void Dial_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            double side = TicksCanvas.Width;
            if (side <= 0) return;

            var pos = e.GetPosition(TicksCanvas);
            double cx = side / 2, cy = side / 2;
            double dx = pos.X - cx, dy = pos.Y - cy;
            if (Math.Sqrt(dx * dx + dy * dy) > cx) return; // вне круга → перетаскивание окна

            e.Handled = true;                 // внутри круга окно не тянем
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

            // Мгновенно применяем к идущему тиканью
            if (_clock != null) _clock.Volume = _tickEnabled ? TickBaseVolume : 0f;
        }

        // --- Управление ---
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_state == RunState.Idle && _taskQueue.Count > 0)
            {
                StartNextTask();
                _timer.Start();
                if (_clock != null) { _clock.Volume = _tickEnabled ? TickBaseVolume : 0f; _clock.Start(); }
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
                _clock?.Pause();
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
            _clock?.Resume();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => Stop();

        private void Stop()
        {
            _timer.Stop();
            _clock?.Stop();
            _state = RunState.Idle;
            _taskQueue.Clear();
            _currentTask = null;
            _elapsedSeconds = 0;

            UpdateTicks(0); // все риски гаснут
            TxtHours.Text = ""; TxtMinutes.Text = "00"; TxtSeconds.Text = ":00";
            LblPhase.Text = "";
            UpdateButtonStates();
            LoadSchedule(); // Перезагрузка JSON для сброса
        }
    }

    public record PeriodTask(Period Period, bool IsPrepare, int TotalSeconds, int ElapsedSeconds, int CurrentRepeat, int TotalRepeat);
    public enum RunState { Idle, Prepare, Working, Paused }
}