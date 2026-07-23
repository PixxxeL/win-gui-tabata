using System;
using System.Collections.Generic;
using System.Globalization;

namespace ScheduleTimer
{
    /// <summary>
    /// Простая локализация строк интерфейса. Язык выбирается по языку ОС
    /// (<see cref="CultureInfo.CurrentUICulture"/>), с фолбэком в английский.
    /// Можно форсировать переменной окружения SCHEDULETIMER_LANG (напр. "ja").
    /// Названия периодов берутся из config.json и не локализуются — это данные.
    /// </summary>
    public static class Loc
    {
        private static readonly string _lang;

        static Loc()
        {
            var code = Environment.GetEnvironmentVariable("SCHEDULETIMER_LANG");
            if (string.IsNullOrWhiteSpace(code))
                code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            code = code.ToLowerInvariant();
            _lang = _langs.ContainsKey(code) ? code : "en";
        }

        /// <summary>Локализованная строка по ключу; при отсутствии — английская, затем сам ключ.</summary>
        public static string Get(string key)
        {
            if (_langs.TryGetValue(_lang, out var m) && m.TryGetValue(key, out var s)) return s;
            if (_langs["en"].TryGetValue(key, out var en)) return en;
            return key;
        }

        private static readonly Dictionary<string, Dictionary<string, string>> _langs = new()
        {
            ["en"] = new()
            {
                ["app_title"] = "Schedule Timer", ["prepare"] = "Preparation", ["finish"] = "Finish",
                ["tip_play"] = "Start / Resume", ["tip_pause"] = "Pause / Resume", ["tip_stop"] = "Stop",
                ["tip_sound"] = "Ticking sound on/off", ["tip_minimize"] = "Minimize", ["tip_close"] = "Close",
                ["key_space"] = "Space", ["err_no_config"] = "config.json not found",
                ["err_no_active"] = "No active schedules", ["err_prefix"] = "Error", ["loading"] = "Loading…",
                ["already_running"] = "The application is already running",
                ["summary_title"] = "Time summary", ["summary_total"] = "Total",
                ["unit_d"] = "d", ["unit_h"] = "h", ["unit_m"] = "min", ["unit_s"] = "s",
            },
            ["ru"] = new()
            {
                ["app_title"] = "Таймер расписания", ["prepare"] = "Подготовка", ["finish"] = "Финиш",
                ["tip_play"] = "Старт / Продолжить", ["tip_pause"] = "Пауза / Продолжить", ["tip_stop"] = "Стоп",
                ["tip_sound"] = "Звук тиканья вкл/выкл", ["tip_minimize"] = "Свернуть", ["tip_close"] = "Закрыть",
                ["key_space"] = "Пробел", ["err_no_config"] = "Файл config.json не найден",
                ["err_no_active"] = "Нет активных расписаний", ["err_prefix"] = "Ошибка", ["loading"] = "Загрузка…",
                ["already_running"] = "Приложение уже запущено",
                ["summary_title"] = "Итоги", ["summary_total"] = "Итого",
                ["unit_d"] = "д", ["unit_h"] = "ч", ["unit_m"] = "мин", ["unit_s"] = "сек",
            },
            ["zh"] = new()
            {
                ["app_title"] = "日程计时器", ["prepare"] = "准备", ["finish"] = "结束",
                ["tip_play"] = "开始 / 继续", ["tip_pause"] = "暂停 / 继续", ["tip_stop"] = "停止",
                ["tip_sound"] = "滴答声 开/关", ["tip_minimize"] = "最小化", ["tip_close"] = "关闭",
                ["key_space"] = "空格", ["err_no_config"] = "未找到 config.json",
                ["err_no_active"] = "没有启用的日程", ["err_prefix"] = "错误", ["loading"] = "加载中…",
                ["already_running"] = "应用程序已在运行",
                ["summary_title"] = "统计", ["summary_total"] = "总计",
                ["unit_d"] = "天", ["unit_h"] = "小时", ["unit_m"] = "分", ["unit_s"] = "秒",
            },
            ["ja"] = new()
            {
                ["app_title"] = "スケジュールタイマー", ["prepare"] = "準備", ["finish"] = "終了",
                ["tip_play"] = "開始 / 再開", ["tip_pause"] = "一時停止 / 再開", ["tip_stop"] = "停止",
                ["tip_sound"] = "秒針音 オン/オフ", ["tip_minimize"] = "最小化", ["tip_close"] = "閉じる",
                ["key_space"] = "スペース", ["err_no_config"] = "config.json が見つかりません",
                ["err_no_active"] = "有効なスケジュールがありません", ["err_prefix"] = "エラー", ["loading"] = "読み込み中…",
                ["already_running"] = "アプリはすでに起動しています",
                ["summary_title"] = "集計", ["summary_total"] = "合計",
                ["unit_d"] = "日", ["unit_h"] = "時間", ["unit_m"] = "分", ["unit_s"] = "秒",
            },
            ["de"] = new()
            {
                ["app_title"] = "Zeitplan-Timer", ["prepare"] = "Vorbereitung", ["finish"] = "Ende",
                ["tip_play"] = "Start / Fortsetzen", ["tip_pause"] = "Pause / Fortsetzen", ["tip_stop"] = "Stopp",
                ["tip_sound"] = "Tickgeräusch ein/aus", ["tip_minimize"] = "Minimieren", ["tip_close"] = "Schließen",
                ["key_space"] = "Leertaste", ["err_no_config"] = "config.json nicht gefunden",
                ["err_no_active"] = "Keine aktiven Zeitpläne", ["err_prefix"] = "Fehler", ["loading"] = "Wird geladen…",
                ["summary_title"] = "Zusammenfassung", ["summary_total"] = "Gesamt",
                ["unit_d"] = "T", ["unit_h"] = "Std.", ["unit_m"] = "Min.", ["unit_s"] = "Sek.",
            },
            ["es"] = new()
            {
                ["app_title"] = "Temporizador de horario", ["prepare"] = "Preparación", ["finish"] = "Fin",
                ["tip_play"] = "Iniciar / Reanudar", ["tip_pause"] = "Pausar / Reanudar", ["tip_stop"] = "Detener",
                ["tip_sound"] = "Sonido de tictac sí/no", ["tip_minimize"] = "Minimizar", ["tip_close"] = "Cerrar",
                ["key_space"] = "Espacio", ["err_no_config"] = "config.json no encontrado",
                ["err_no_active"] = "No hay horarios activos", ["err_prefix"] = "Error", ["loading"] = "Cargando…",
                ["summary_title"] = "Resumen", ["summary_total"] = "Total",
                ["unit_d"] = "d", ["unit_h"] = "h", ["unit_m"] = "min", ["unit_s"] = "s",
            },
            ["fr"] = new()
            {
                ["app_title"] = "Minuteur de programme", ["prepare"] = "Préparation", ["finish"] = "Fin",
                ["tip_play"] = "Démarrer / Reprendre", ["tip_pause"] = "Pause / Reprendre", ["tip_stop"] = "Arrêter",
                ["tip_sound"] = "Son de tic-tac activé/désactivé", ["tip_minimize"] = "Réduire", ["tip_close"] = "Fermer",
                ["key_space"] = "Espace", ["err_no_config"] = "config.json introuvable",
                ["err_no_active"] = "Aucun programme actif", ["err_prefix"] = "Erreur", ["loading"] = "Chargement…",
                ["summary_title"] = "Résumé", ["summary_total"] = "Total",
                ["unit_d"] = "j", ["unit_h"] = "h", ["unit_m"] = "min", ["unit_s"] = "s",
            },
            ["it"] = new()
            {
                ["app_title"] = "Timer pianificato", ["prepare"] = "Preparazione", ["finish"] = "Fine",
                ["tip_play"] = "Avvia / Riprendi", ["tip_pause"] = "Pausa / Riprendi", ["tip_stop"] = "Ferma",
                ["tip_sound"] = "Ticchettio on/off", ["tip_minimize"] = "Riduci a icona", ["tip_close"] = "Chiudi",
                ["key_space"] = "Spazio", ["err_no_config"] = "config.json non trovato",
                ["err_no_active"] = "Nessuna pianificazione attiva", ["err_prefix"] = "Errore", ["loading"] = "Caricamento…",
                ["summary_title"] = "Riepilogo", ["summary_total"] = "Totale",
                ["unit_d"] = "g", ["unit_h"] = "h", ["unit_m"] = "min", ["unit_s"] = "s",
            },
            ["pt"] = new()
            {
                ["app_title"] = "Temporizador de agenda", ["prepare"] = "Preparação", ["finish"] = "Fim",
                ["tip_play"] = "Iniciar / Retomar", ["tip_pause"] = "Pausar / Retomar", ["tip_stop"] = "Parar",
                ["tip_sound"] = "Som de tique-taque lig/desl", ["tip_minimize"] = "Minimizar", ["tip_close"] = "Fechar",
                ["key_space"] = "Espaço", ["err_no_config"] = "config.json não encontrado",
                ["err_no_active"] = "Nenhuma agenda ativa", ["err_prefix"] = "Erro", ["loading"] = "Carregando…",
                ["summary_title"] = "Resumo", ["summary_total"] = "Total",
                ["unit_d"] = "d", ["unit_h"] = "h", ["unit_m"] = "min", ["unit_s"] = "s",
            },
        };
    }
}
