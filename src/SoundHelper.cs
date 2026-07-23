using NAudio.Wave;
using System;
using System.Collections.Generic;
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
    /// Вырезается БЕСШОВНАЯ петля из целого числа пар «тик-так» (начало и конец — восходящие
    /// ноль-переходы в тихой паузе на одной фазе: без щелчка и сбоя ритма). Число пар берётся
    /// так, чтобы длина была ближе всего к целому числу секунд, после чего лёгким ресемплом
    /// подгоняется РОВНО под это число секунд — тогда петля кратна секунде и не уезжает от цифр.
    /// Кроме этой подгонки длины звук берётся «как есть»; громкость — единственная правка уровня.
    /// Возвращаются два <see cref="SoundPlayer"/> (тихий/громкий) для непрерывного PlayLooping.
    /// </summary>
    public static class TickSounds
    {
        private const int EnvelopeWindowMs = 5;      // окно сглаживания огибающей
        private const double OnsetHiFraction = 0.30; // начало удара: огибающая выше доли пика
        private const double OnsetLoFraction = 0.10; // гистерезис: спад ниже — удар закончился
        private const int MaxLoopPairs = 6;          // сколько пар максимум перебирать при подборе длины
        private const int LoopGuardMs = 24;          // насколько раньше удара ставить границу

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
                if (!TryExtractLoop(mp3, out var pcm)) return null;

                // Единственная обработка — громкость: тихий (обычный ход) и громкий (последние 5 c).
                var fmt = mp3.WaveFormat;
                var quiet = LoadPlayer(BuildWav(pcm, fmt, volume: 0.5f));
                var loud = LoadPlayer(BuildWav(pcm, fmt, volume: 0.9f));
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

        // Вырезает бесшовную петлю из целого числа пар «тик-так» и подгоняет её длину ровно
        // под ближайшее целое число секунд (лёгкий ресемпл) — чтобы петля была кратна секунде.
        private static bool TryExtractLoop(Mp3FileReader mp3, out byte[] loop)
        {
            loop = Array.Empty<byte>();
            var fmt = mp3.WaveFormat;
            int frame = fmt.Channels * (fmt.BitsPerSample / 8);
            int sr = fmt.SampleRate;

            // читаем ~4 c PCM — хватает на несколько пар для подбора длины
            var buf = new byte[fmt.AverageBytesPerSecond * 4];
            int read = 0, n;
            while (read < buf.Length && (n = mp3.Read(buf, read, buf.Length - read)) > 0) read += n;
            int frames = read / frame;
            if (frames < 64) return false;

            // Амплитуда первого канала по кадрам (со знаком — нужна для ноль-переходов).
            var x = new double[frames];
            for (int k = 0; k < frames; k++) { int i = k * frame; x[k] = (short)(buf[i] | (buf[i + 1] << 8)); }

            // Огибающая |амплитуды| — по ней ищем удары (по мгновенным сэмплам нельзя: волна пересекает ноль).
            int win = Math.Max(1, sr * EnvelopeWindowMs / 1000);
            var env = new double[frames];
            double sum = 0, maxEnv = 0;
            for (int k = 0; k < frames; k++)
            {
                sum += Math.Abs(x[k]);
                if (k >= win) sum -= Math.Abs(x[k - win]);
                env[k] = sum / Math.Min(k + 1, win);
                if (env[k] > maxEnv) maxEnv = env[k];
            }
            if (maxEnv < 1) return false; // тишина

            // Гистерезисная детекция ударов.
            double hi = OnsetHiFraction * maxEnv, lo = OnsetLoFraction * maxEnv;
            var onsets = new List<int>();
            bool active = false;
            for (int k = 0; k < frames; k++)
            {
                if (!active && env[k] > hi) { active = true; onsets.Add(k); }
                else if (active && env[k] < lo) active = false;
            }
            if (onsets.Count <= 2) return false;

            // Подбираем число пар так, чтобы длина петли была ближе всего к целому числу секунд
            // (минимальная растяжка). Напр. для этой записи 5 пар ≈ 2.001 c → подгон всего −0.07%.
            int bestPairs = 0, bestSeconds = 0;
            double bestErr = double.MaxValue;
            for (int pairs = 1; 2 * pairs < onsets.Count && pairs <= MaxLoopPairs; pairs++)
            {
                double sec = (double)(onsets[2 * pairs] - onsets[0]) / sr;
                int s = (int)Math.Round(sec);
                if (s < 1) continue;
                double err = Math.Abs(s - sec) / s;
                if (err < bestErr) { bestErr = err; bestPairs = pairs; bestSeconds = s; }
            }
            if (bestPairs == 0) return false;

            // Вырезаем bestPairs пар бесшовно: границы — ноль-переходы на одной фазе.
            int guard = sr * LoopGuardMs / 1000;
            int startF = RisingZeroNear(x, onsets[0] - guard);
            int endF = RisingZeroNear(x, startF + (onsets[2 * bestPairs] - onsets[0]));
            int srcF = endF - startF;
            if (srcF <= 0) return false;

            // Подгоняем длину ровно под bestSeconds секунд (лёгкий линейный ресемпл).
            loop = Resample(buf, startF, srcF, bestSeconds * sr, frame, fmt.Channels);
            return true;
        }

        // Ближайший к target кадр с восходящим ноль-переходом (x[k] ≤ 0 < x[k+1]).
        private static int RisingZeroNear(double[] x, int target)
        {
            for (int off = 0; off < x.Length; off++)
            {
                int a = target + off, b = target - off;
                if (a > 0 && a + 1 < x.Length && x[a] <= 0 && x[a + 1] > 0) return a;
                if (b > 0 && b + 1 < x.Length && x[b] <= 0 && x[b + 1] > 0) return b;
            }
            return Math.Max(0, Math.Min(target, x.Length - 1));
        }

        // Линейный ресемпл участка [startF, startF+srcF) кадров в dstF кадров (подгонка длины).
        private static byte[] Resample(byte[] buf, int startF, int srcF, int dstF, int frame, int channels)
        {
            var outBuf = new byte[dstF * frame];
            for (int j = 0; j < dstF; j++)
            {
                double sp = (double)j * srcF / dstF;
                int i0 = (int)Math.Floor(sp);
                double t = sp - i0;
                int i1 = Math.Min(i0 + 1, srcF - 1);
                for (int ch = 0; ch < channels; ch++)
                {
                    int a = (startF + i0) * frame + ch * 2, b = (startF + i1) * frame + ch * 2;
                    short va = (short)(buf[a] | (buf[a + 1] << 8));
                    short vb = (short)(buf[b] | (buf[b + 1] << 8));
                    short v = (short)Math.Round(va + (vb - va) * t);
                    int o = j * frame + ch * 2;
                    outBuf[o] = (byte)v;
                    outBuf[o + 1] = (byte)(v >> 8);
                }
            }
            return outBuf;
        }

        // Оборачивает PCM в WAV, применяя ТОЛЬКО громкость (без fade и прочей обработки).
        private static Stream BuildWav(byte[] pcm, WaveFormat fmt, float volume)
        {
            var data = (byte[])pcm.Clone();
            if (volume != 1f)
            {
                int samples = data.Length / 2;
                for (int i = 0; i < samples; i++)
                {
                    short s = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
                    int v = Math.Clamp((int)(s * volume), short.MinValue, short.MaxValue);
                    data[i * 2] = (byte)v;
                    data[i * 2 + 1] = (byte)(v >> 8);
                }
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
