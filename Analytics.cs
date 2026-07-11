using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScheduleTimer
{
    /// <summary>
    /// Отправка событий в Яндекс AppMetrica через HTTP Import API (server-to-server) —
    /// у десктопа нет мобильного SDK, поэтому шлём события напрямую POST-запросом.
    /// Обязательные поля: post_api_key, application_id, event_name, event_timestamp и один
    /// идентификатор — для десктопа profile_id + os_name=windows.
    ///
    /// Ключ и id ЗАПЕКАЮТСЯ в бинарник при сборке из переменных окружения
    /// APPMETRICA_POST_API_KEY / APPMETRICA_APP_ID (через AssemblyMetadata в .csproj) —
    /// пользователь не может отключить метрику правкой конфига, а значения не хранятся в репозитории.
    /// Если хотя бы одна строка пуста (переменные при сборке не заданы) — аналитика выключена
    /// и в сеть ничего не уходит. Отправка «мягкая»: асинхронно, ошибки сети гасятся (офлайн ок).
    /// </summary>
    public static class Analytics
    {
        private const string Endpoint = "https://api.appmetrica.yandex.com/logs/v1/import/events";
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

        // Значения, вшитые в сборку из одноимённых переменных окружения при билде.
        private static readonly string PostApiKey = BuildValue("APPMETRICA_POST_API_KEY");
        private static readonly string AppId = BuildValue("APPMETRICA_APP_ID");

        // Обе строки должны быть непустыми — иначе запросы не идут.
        private static readonly bool Enabled = PostApiKey.Length > 0 && AppId.Length > 0;
        private static readonly string ProfileId = Enabled ? GetOrCreateProfileId() : "";

        public static void AppStarted() => Send("app_start", null);

        public static void StageCompleted(string name, int seconds) =>
            Send("stage_done", JsonSerializer.Serialize(new { name, seconds }));

        private static void Send(string eventName, string? eventJson)
        {
            if (!Enabled) return;

            var url = $"{Endpoint}?post_api_key={Uri.EscapeDataString(PostApiKey)}" +
                      $"&application_id={Uri.EscapeDataString(AppId)}" +
                      $"&profile_id={Uri.EscapeDataString(ProfileId)}" +
                      $"&os_name=windows" +
                      $"&event_name={Uri.EscapeDataString(eventName)}" +
                      $"&event_timestamp={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            if (!string.IsNullOrEmpty(eventJson))
                url += $"&event_json={Uri.EscapeDataString(eventJson)}";

            _ = PostAsync(url); // fire-and-forget: UI не блокируем
        }

        private static async Task PostAsync(string url)
        {
            try { using var _ = await Http.PostAsync(url, null).ConfigureAwait(false); }
            catch { /* оффлайн / сбой сети — не мешаем работе таймера */ }
        }

        // Значение, вшитое в сборку из одноимённой переменной окружения (AssemblyMetadata в .csproj).
        private static string BuildValue(string key)
        {
            foreach (var a in Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>())
                if (a.Key == key) return a.Value?.Trim() ?? "";
            return "";
        }

        // Стабильный анонимный идентификатор (случайный GUID) в файле в каталоге данных —
        // чтобы события одного пользователя связывались. Персональных данных нет.
        private static string GetOrCreateProfileId()
        {
            try
            {
                var path = Path.Combine(AppPaths.DataDir, "analytics-id.txt");

                if (File.Exists(path))
                {
                    var id = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(id)) return id;
                }

                var newId = Guid.NewGuid().ToString("N");
                File.WriteAllText(path, newId);
                return newId;
            }
            catch { return "windows-desktop"; }
        }
    }
}
