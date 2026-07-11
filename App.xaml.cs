using System;
using System.Threading;
using System.Windows;

namespace ScheduleTimer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Именованный Mutex на уровне ОС: гарантирует единственный экземпляр приложения,
        // чтобы не было гонки при записи в общий лог-файл.
        private const string InstanceMutexName = @"Global\ScheduleTimer_SingleInstance";
        private Mutex? _instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mutex = new Mutex(initiallyOwned: true, InstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                // Экземпляр уже запущен — предупреждаем и выходим (лог не трогаем).
                mutex.Dispose();
                MessageBox.Show(Loc.Get("already_running"), Loc.Get("app_title"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            _instanceMutex = mutex; // владеем мьютексом на всё время работы
            Logger.AppStarted();

            new MainWindow().Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
                _instanceMutex.Dispose();
                _instanceMutex = null;
            }
            base.OnExit(e);
        }
    }
}
