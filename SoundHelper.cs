using NAudio.Wave;
using System;
using System.IO;
using System.Media;

namespace ScheduleTimer
{
    /// <summary>
    /// Одноразовое воспроизведение звуков событий периода (start / beforeFinish /
    /// finish) из внешних файлов рядом с exe. Поддерживает WAV и MP3 (через NAudio).
    /// </summary>
    public static class SoundHelper
    {
        public static void Play(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path)) return;

            try
            {
                // WaveOutEvent играет на своём потоке; ресурсы освобождаем по событию.
                var reader = new AudioFileReader(path);
                var waveOut = new WaveOutEvent();
                waveOut.PlaybackStopped += (_, _) => { waveOut.Dispose(); reader.Dispose(); };
                waveOut.Init(reader);
                waveOut.Play();
            }
            catch
            {
                // Сбой воспроизведения не должен ронять таймер.
            }
        }
    }

    /// <summary>
    /// Готовит звук тиканья из встроенной в сборку записи часов.
    /// Из записи вырезается один «тик» (с обрезкой тишины в начале), из него
    /// собираются два <see cref="SoundPlayer"/> — тихий и громкий. Проигрывание
    /// раз в секунду по таймеру гарантирует синхронность с цифрами секунд.
    /// </summary>
    public static class TickSounds
    {
        private const double TickWindowSeconds = 0.18; // длительность одного «тика»
        private const short OnsetThreshold = 2500;     // порог начала звука (16-бит)

        /// <summary>Загружает встроенный ресурс и возвращает (тихий, громкий) плееры, либо null.</summary>
        public static (SoundPlayer quiet, SoundPlayer loud)? FromEmbedded(string resourceName)
        {
            try
            {
                var asm = typeof(TickSounds).Assembly;
                var res = Array.Find(asm.GetManifestResourceNames(),
                    n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
                if (res == null) return null;

                using var stream = asm.GetManifestResourceStream(res);
                if (stream == null) return null;

                using var mp3 = new Mp3FileReader(stream);
                if (!TryExtractTick(mp3, out var pcm)) return null;

                var quiet = LoadPlayer(BuildWav(pcm, mp3.WaveFormat, gain: 0.35f));
                var loud = LoadPlayer(BuildWav(pcm, mp3.WaveFormat, gain: 0.90f));
                return (quiet, loud);
            }
            catch { return null; }
        }

        private static SoundPlayer LoadPlayer(Stream wav)
        {
            var player = new SoundPlayer(wav);
            player.Load();
            return player;
        }

        // Читает ~1 c PCM, находит начало первого «тика» и возвращает окно нужной длины.
        private static bool TryExtractTick(Mp3FileReader mp3, out byte[] tick)
        {
            tick = Array.Empty<byte>();
            var fmt = mp3.WaveFormat;
            int frame = fmt.Channels * (fmt.BitsPerSample / 8);

            var buf = new byte[fmt.AverageBytesPerSecond];
            int read = 0, n;
            while (read < buf.Length && (n = mp3.Read(buf, read, buf.Length - read)) > 0) read += n;
            if (read < frame * 64) return false;

            // Начало звука — первый сэмпл выше порога.
            int onset = 0;
            for (int i = 0; i + 1 < read; i += 2)
            {
                short s = (short)(buf[i] | (buf[i + 1] << 8));
                if (Math.Abs((int)s) > OnsetThreshold) { onset = i; break; }
            }

            int start = Math.Max(0, onset - frame * 4);   // чуть раньше начала
            start -= start % frame;                        // выравниваем по кадру
            int window = (int)(fmt.SampleRate * TickWindowSeconds) * frame;
            int len = Math.Min(window, read - start);
            len -= len % frame;
            if (len <= 0) return false;

            tick = new byte[len];
            Buffer.BlockCopy(buf, start, tick, 0, len);
            return true;
        }

        // Применяет усиление и короткий fade-out (чтобы не щёлкало) и оборачивает в WAV.
        private static Stream BuildWav(byte[] pcm, WaveFormat fmt, float gain)
        {
            var data = (byte[])pcm.Clone();
            int samples = data.Length / 2;
            int fade = Math.Min(samples, fmt.SampleRate / 100); // 10 мс

            for (int i = 0; i < samples; i++)
            {
                short s = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
                double g = gain;
                if (i > samples - fade) g *= (double)(samples - i) / fade;
                int v = Math.Clamp((int)(s * g), short.MinValue, short.MaxValue);
                data[i * 2] = (byte)v;
                data[i * 2 + 1] = (byte)(v >> 8);
            }

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            int blockAlign = fmt.Channels * 2;
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + data.Length);
            bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);                 // PCM
            bw.Write((short)fmt.Channels);
            bw.Write(fmt.SampleRate);
            bw.Write(fmt.SampleRate * blockAlign);
            bw.Write((short)blockAlign);
            bw.Write((short)16);
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(data.Length);
            bw.Write(data);
            bw.Flush();
            ms.Position = 0;
            return ms;
        }
    }
}
