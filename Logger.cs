using System;
using System.IO;

namespace ScheduleTimer
{
    /// <summary>
    /// Простой файловый лог рядом с бинарником — для учёта отработанного времени по этапам.
    /// Пишется одна строка на событие: таймстемп + сообщение. Файл создаётся при отсутствии
    /// и дополняется при наличии. Запись идёт только из UI-потока, а один экземпляр приложения
    /// гарантируется Mutex-ом (см. App), поэтому гонок в файле нет.
    /// </summary>
    public static class Logger
    {
        private static readonly string LogPath = BuildPath();

        private static string BuildPath()
        {
            // Именно рядом с exe: в single-file сборке AppContext.BaseDirectory — это временная
            // папка распаковки, а Environment.ProcessPath указывает на сам исполняемый файл.
            var exe = Environment.ProcessPath;
            var dir = !string.IsNullOrEmpty(exe) ? Path.GetDirectoryName(exe) : null;
            return Path.Combine(dir ?? AppContext.BaseDirectory, "timetracker.log");
        }

        /// <summary>Отметка запуска приложения (текст лога — на английском).</summary>
        public static void AppStarted() => Write("--- Application started ---");

        /// <summary>Завершённый этап: имя из конфига «как есть» и фактически отработанные секунды.</summary>
        public static void StageCompleted(string name, int seconds) => Write($"{name} — {seconds} s");

        private static void Write(string message)
        {
            try
            {
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line); // UTF-8 без BOM, создаёт/дополняет
            }
            catch
            {
                // Сбой записи лога не должен ронять приложение.
            }
        }
    }
}
