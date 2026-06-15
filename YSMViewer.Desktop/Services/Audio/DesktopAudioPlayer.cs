using NAudio.Vorbis;
using NAudio.Wave;
using YSMViewer.Services.Audio;

namespace YSMViewer.Desktop.Services.Audio;

public sealed class DesktopAudioPlayer : IPlatformAudioPlayer
{
    private readonly List<DesktopAudioInstance> _instances = [];
    private readonly Dictionary<string, CachedPcm> _pcmCache = [];
    private readonly Lock _cacheLock = new();

    public IAudioInstance Play(byte[] oggData, float volume)
    {
        var instance = new DesktopAudioInstance(oggData, volume);
        lock (_instances)
            _instances.Add(instance);
        instance.Play();
        return instance;
    }

    public IAudioInstance PlayPcm(string soundKey, float volume)
    {
        CachedPcm? cached;
        lock (_cacheLock)
        {
            _pcmCache.TryGetValue(soundKey, out cached);
        }

        if (cached is null)
            return Play([], volume);

        var instance = new DesktopAudioInstance(cached.Samples, cached.SampleRate, cached.Channels, volume);
        lock (_instances)
            _instances.Add(instance);
        instance.Play();
        return instance;
    }

    public void PreDecode(byte[] oggData, string key)
    {
        lock (_cacheLock)
        {
            if (_pcmCache.ContainsKey(key))
                return;
        }

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                using var stream = new MemoryStream(oggData);
                using var reader = new VorbisWaveReader(stream, false);
                var decodeStream = new RawSourceWaveStream(reader, new WaveFormat(reader.WaveFormat.SampleRate, reader.WaveFormat.Channels));

                var pcmBlocks = new List<byte[]>();
                int totalBytes = 0;
                byte[] buffer = new byte[8192];
                int read;
                while ((read = decodeStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var block = new byte[read];
                    Array.Copy(buffer, block, read);
                    pcmBlocks.Add(block);
                    totalBytes += read;
                }

                var pcmData = new byte[totalBytes];
                int offset = 0;
                foreach (var block in pcmBlocks)
                {
                    Array.Copy(block, 0, pcmData, offset, block.Length);
                    offset += block.Length;
                }

                lock (_cacheLock)
                {
                    _pcmCache[key] = new CachedPcm
                    {
                        Samples = pcmData,
                        SampleRate = reader.WaveFormat.SampleRate,
                        Channels = reader.WaveFormat.Channels,
                    };
                }
            }
            catch
            {
            }
        });
    }

    public void Dispose()
    {
        lock (_instances)
        {
            foreach (var inst in _instances)
                inst.Dispose();
            _instances.Clear();
        }
        lock (_cacheLock)
            _pcmCache.Clear();
    }

    private sealed class CachedPcm
    {
        public byte[] Samples = [];
        public int SampleRate;
        public int Channels;
    }
}

internal sealed class DesktopAudioInstance : IAudioInstance, IDisposable
{
    private readonly WaveOutEvent? _output;
    private readonly WaveStream? _stream;
    private readonly MemoryStream? _memStream;

    public DesktopAudioInstance(byte[] oggData, float volume)
    {
        _memStream = new MemoryStream(oggData);
        var reader = new VorbisWaveReader(_memStream, false);
        _stream = reader;
        _output = new WaveOutEvent();
        _output.Init(_stream);
        _output.Volume = float.Clamp(volume, 0f, 1f);
    }

    public DesktopAudioInstance(byte[] pcmData, int sampleRate, int channels, float volume)
    {
        var format = new WaveFormat(sampleRate, 16, channels);
        _memStream = new MemoryStream(pcmData);
        _stream = new RawSourceWaveStream(_memStream, format);
        _output = new WaveOutEvent();
        _output.Init(_stream);
        _output.Volume = float.Clamp(volume, 0f, 1f);
    }

    public void Play() => _output?.Play();

    public void Stop() => _output?.Stop();

    public void SetVolume(float volume)
    {
        _output?.Volume = float.Clamp(volume, 0f, 1f);
    }

    public void Dispose()
    {
        _output?.Dispose();
        _stream?.Dispose();
        _memStream?.Dispose();
    }
}
