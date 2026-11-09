// WavClipWriter - streams 16-bit mono PCM to a WAV file and patches the header on close, so a crash loses at most the header sizes.
namespace RandySDR
{
    public sealed class WavClipWriter : IDisposable
    {
        private readonly FileStream _fs;
        private readonly int _sampleRate;
        private long _dataBytes;
        private bool _closed;

        public long SampleCount => _dataBytes / 2;
        public string Path { get; }

        public WavClipWriter(string path, int sampleRate)
        {
            Path = path;
            _sampleRate = sampleRate;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            _fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            WriteHeader();
        }

        public void Write(float[] samples)
        {
            if (_closed || samples.Length == 0) return;
            var buf = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short s = (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * 32767f);
                buf[2 * i] = (byte)(s & 0xFF);
                buf[2 * i + 1] = (byte)((s >> 8) & 0xFF);
            }
            _fs.Write(buf, 0, buf.Length);
            _dataBytes += buf.Length;
        }

        private void WriteHeader()
        {
            _fs.Position = 0;
            using var w = new BinaryWriter(_fs, System.Text.Encoding.ASCII, leaveOpen: true);
            w.Write("RIFF".ToCharArray());
            w.Write((int)(36 + _dataBytes));
            w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray());
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(_sampleRate);
            w.Write(_sampleRate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write("data".ToCharArray());
            w.Write((int)_dataBytes);
            w.Flush();
            _fs.Position = _fs.Length;
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            WriteHeader();
            _fs.Dispose();
        }

        public void Dispose() => Close();
    }
}
