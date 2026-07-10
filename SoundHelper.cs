using NAudio.Wave;
using System;
using System.IO;

namespace ScheduleTimer
{
    // Оборачивает WaveStream и зацикливает его при достижении конца.
    public sealed class LoopStream : WaveStream
    {
        private readonly WaveStream _source;
        public LoopStream(WaveStream source) => _source = source;

        public override WaveFormat WaveFormat => _source.WaveFormat;
        public override long Length => _source.Length;
        public override long Position { get => _source.Position; set => _source.Position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = _source.Read(buffer, offset + total, count - total);
                if (read == 0)
                {
                    if (_source.Position == 0) break; // источник пуст
                    _source.Position = 0;             // повтор с начала
                }
                total += read;
            }
            return total;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _source.Dispose();
            base.Dispose(disposing);
        }
    }

    // Зацикленное фоновое тиканье часов (MP3/WAV) с управлением громкостью и паузой.
    public sealed class ClockSound : IDisposable
    {
        private readonly AudioFileReader _reader;
        private readonly LoopStream _loop;
        private readonly WaveOutEvent _output;
        private float _volume = 0.35f;

        private ClockSound(string path)
        {
            _reader = new AudioFileReader(path) { Volume = _volume };
            _loop = new LoopStream(_reader);
            _output = new WaveOutEvent();
            _output.Init(_loop);
        }

        // Возвращает null, если файла нет или он не читается — таймер работает без звука.
        public static ClockSound? TryCreate(string fileName)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, fileName);
                return File.Exists(path) ? new ClockSound(path) : null;
            }
            catch { return null; }
        }

        // Громкость 0..1 (мгновенно применяется к идущему воспроизведению).
        public float Volume
        {
            get => _volume;
            set { _volume = value; try { _reader.Volume = value; } catch { } }
        }

        public void Start()   { try { _reader.Position = 0; _output.Play(); } catch { } }
        public void Pause()   { try { _output.Pause(); } catch { } }
        public void Resume()  { try { _output.Play(); } catch { } }
        public void Stop()    { try { _output.Stop(); _reader.Position = 0; } catch { } }

        public void Dispose()
        {
            try { _output.Dispose(); _loop.Dispose(); } catch { }
        }
    }

    public static class SoundHelper
    {
        public static void Play(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path)) return;

            try
            {
                // WaveOutEvent проигрывает на собственном потоке; освобождаем
                // ресурсы по событию, а не busy-wait циклом.
                var reader = new AudioFileReader(path);
                var waveOut = new WaveOutEvent();
                waveOut.PlaybackStopped += (_, _) =>
                {
                    waveOut.Dispose();
                    reader.Dispose();
                };
                waveOut.Init(reader);
                waveOut.Play();
            }
            catch
            {
                // Сбой воспроизведения не должен ронять таймер.
            }
        }
    }
}
