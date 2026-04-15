using NAudio.Wave;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ScheduleTimer
{
    public static class SoundHelper
    {
        public static void Play(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path)) return;

            // Fire-and-forget воспроизведение в фоне
            Task.Run(() =>
            {
                using var reader = new AudioFileReader(path);
                using var waveOut = new WaveOutEvent();
                waveOut.Init(reader);
                waveOut.Play();

                // Ждем окончания, чтобы не освободить файл раньше времени
                while (waveOut.PlaybackState == PlaybackState.Playing)
                    Thread.Sleep(50);
            });
        }
    }
}
