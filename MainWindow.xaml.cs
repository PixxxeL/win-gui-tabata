using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
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
        private double _circumference;
        private string _scheduleName = "Нет данных";

        public MainWindow()
        {
            InitializeComponent();
            _timer.Tick += Timer_Tick;

            // Рассчитываем длину окружности для прогресс-бара (R = 170 - 8 (половина толщины) = 162)
            _circumference = 2 * Math.PI * 162;
            RingProgress.StrokeDashArray = new System.Windows.Media.DoubleCollection { _circumference, _circumference };
            RingProgress.StrokeDashOffset = 0;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e) => LoadSchedule();

        private void LoadSchedule()
        {
            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "config.json");
                if (!File.Exists(jsonPath))
                {
                    LblTitle.Text = "Файл config.json не найден";
                    return;
                }

                var json = File.ReadAllText(jsonPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var config = JsonSerializer.Deserialize<ScheduleConfig>(json, options);
                var active = config?.Schedules?.FirstOrDefault(s => s.Active);

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

        // Рекурсивно распаковывает вложенные периоды в плоскую очередь задач
        private void BuildQueue(List<Period> periods)
        {
            foreach (var p in periods)
            {
                for (int i = 0; i < p.Repeat; i++)
                {
                    if (p.Prepare > 0)
                        _taskQueue.Add(new PeriodTask(p, true, p.Prepare, 0, i + 1, p.Repeat));
                    _taskQueue.Add(new PeriodTask(p, false, p.Duration, 0, i + 1, p.Repeat));
                }
                BuildQueue(p.Periods); // Вложенные периоды выполняются после всех повторов родителя
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
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_state == RunState.Paused || _currentTask == null) return;

            _elapsedSeconds++;
            _currentTask = _currentTask with { ElapsedSeconds = _elapsedSeconds };
            int remaining = _currentTask.TotalSeconds - _elapsedSeconds;

            // Триггеры звуков
            if (_elapsedSeconds == 1) SoundHelper.Play(_currentTask.Period.Sound.Start);
            if (remaining == 5 && !string.IsNullOrEmpty(_currentTask.Period.Sound.BeforeFinish))
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

            // Прогресс-бар (уменьшается против часовой стрелки)
            double progress = (double)_elapsedSeconds / _currentTask.TotalSeconds;
            RingProgress.StrokeDashOffset = _circumference * progress;

            string phaseName = _currentTask.IsPrepare ? "Подготовка" : "Работа";
            string repeatInfo = $"Повтор {_currentTask.CurrentRepeat}/{_currentTask.TotalRepeat}";
            LblPhase.Text = $"{_currentTask.Period.Name} | {phaseName} | {repeatInfo}";
        }

        private void UpdateColor()
        {
            int c = _currentTask!.Period.Color;
            var color = Color.FromArgb(0xFF, (byte)(c >> 16), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF));
            RingProgress.Stroke = new SolidColorBrush(color);
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
                _state = _currentTask?.IsPrepare == true ? RunState.Prepare : RunState.Working;
                _timer.Start();
            }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (_state is RunState.Working or RunState.Prepare)
            {
                _state = RunState.Paused;
                _timer.Stop();
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => Stop();

        private void Stop()
        {
            _timer.Stop();
            _state = RunState.Idle;
            _taskQueue.Clear();
            _currentTask = null;
            _elapsedSeconds = 0;

            RingProgress.StrokeDashOffset = 0;
            TxtHours.Text = ""; TxtMinutes.Text = "00"; TxtSeconds.Text = ":00";
            LblPhase.Text = "";
            LoadSchedule(); // Перезагрузка JSON для сброса
        }
    }

    public record PeriodTask(Period Period, bool IsPrepare, int TotalSeconds, int ElapsedSeconds, int CurrentRepeat, int TotalRepeat);
    public enum RunState { Idle, Prepare, Working, Paused }
}