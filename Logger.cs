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
        // Портатив — рядом с exe; MSIX — %LOCALAPPDATA%\ScheduleTimer (install-папка read-only).
        private static readonly string LogPath = Path.Combine(AppPaths.DataDir, "timetracker.log");

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
