using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScheduleTimer
{
    public class ScheduleConfig
    {
        [JsonPropertyName("schedules")]
        public List<Schedule> Schedules { get; set; } = new();
    }

    public class Schedule
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("active")] public bool Active { get; set; }
        [JsonPropertyName("periods")] public List<Period> Periods { get; set; } = new();
    }

    public class Period
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("color")]
        [JsonConverter(typeof(FlexibleColorConverter))]
        public int Color { get; set; }
        [JsonPropertyName("prepare")] public int Prepare { get; set; }
        [JsonPropertyName("duration")] public int Duration { get; set; }
        [JsonPropertyName("repeat")] public int Repeat { get; set; } = 1;
        [JsonPropertyName("sound")] public SoundEvents Sound { get; set; } = new();
        [JsonPropertyName("periods")] public List<Period> Periods { get; set; } = new();
    }

    public class SoundEvents
    {
        [JsonPropertyName("start")] public string? Start { get; set; }
        [JsonPropertyName("running")] public string? Running { get; set; }
        [JsonPropertyName("beforeFinish")] public string? BeforeFinish { get; set; }
        [JsonPropertyName("finish")] public string? Finish { get; set; }
    }

    /// <summary>
    /// Позволяет задавать цвет в config.json как числом (123456),
    /// так и строкой в hex-формате: "0x990000", "#990000" или "990000".
    /// </summary>
    public class FlexibleColorConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
                return reader.GetInt32();

            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return 0;

                s = s.Trim();
                bool isHex = false;

                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(2);
                    isHex = true;
                }
                else if (s.StartsWith("#"))
                {
                    s = s.Substring(1);
                    isHex = true;
                }

                // uint.Parse корректно читает и 6-, и 8-значный hex (ARGB) без переполнения.
                if (isHex)
                    return unchecked((int)uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

                // Строка без явного префикса: пробуем десятичное, затем hex.
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec))
                    return dec;

                return unchecked((int)uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }

            throw new JsonException($"Не удалось прочитать цвет из токена {reader.TokenType}.");
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            // Записываем в привычном виде "0xRRGGBB".
            writer.WriteStringValue("0x" + value.ToString("X6", CultureInfo.InvariantCulture));
        }
    }
}
