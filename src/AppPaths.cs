using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ScheduleTimer
{
    /// <summary>
    /// Куда класть записываемые данные (лог, id аналитики, пользовательский config.json).
    /// Портативная сборка — рядом с exe (как раньше). Под MSIX папка установки только для чтения,
    /// поэтому используем %LOCALAPPDATA%\ScheduleTimer. Упакованность определяется по WinRT-пакету.
    /// </summary>
    public static class AppPaths
    {
        private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

        // Есть ли у процесса идентичность пакета (MSIX). Кэшируем — значение не меняется за сессию.
        private static readonly Lazy<bool> _packaged = new(() =>
        {
            try
            {
                int len = 0;
                return GetCurrentPackageFullName(ref len, null) != APPMODEL_ERROR_NO_PACKAGE;
            }
            catch { return false; } // API нет (старая ОС) → считаем непакованным
        });

        public static bool IsPackaged => _packaged.Value;

        /// <summary>Каталог для записываемых данных (создаётся при необходимости).</summary>
        public static string DataDir
        {
            get
            {
                string dir;
                if (IsPackaged)
                {
                    dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ScheduleTimer");
                }
                else
                {
                    var exe = Environment.ProcessPath;
                    dir = !string.IsNullOrEmpty(exe) ? Path.GetDirectoryName(exe)! : AppContext.BaseDirectory;
                }

                try { Directory.CreateDirectory(dir); } catch { /* если не вышло — вернём как есть */ }
                return dir;
            }
        }

        /// <summary>Папка установки (под MSIX — только чтение). Там лежат вшитые в пакет ресурсы,
        /// например дефолтный config.json.</summary>
        public static string InstallDir => AppContext.BaseDirectory;
    }
}
