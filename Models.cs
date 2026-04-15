using System.Collections.Generic;
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
        [JsonPropertyName("color")] public int Color { get; set; }
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
}
