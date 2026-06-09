using NAudio.Wave;
using NAudio.Vorbis;

namespace YSMViewer.Services.Audio;

public sealed class DesktopAudioPlayer : IPlatformAudioPlayer
{
    private readonly List<DesktopAudioInstance> _instances = [];

    public IAudioInstance Play(byte[] oggData, float volume)
    {
        var instance = new DesktopAudioInstance(oggData, volume);
        _instances.Add(instance);
        instance.Play();
        return instance;
    }

    public void Dispose()
    {
        foreach (var inst in _instances)
            inst.Dispose();
        _instances.Clear();
    }
}

internal sealed class DesktopAudioInstance : IAudioInstance, IDisposable
{
    private WaveOutEvent? _output;
    private VorbisWaveReader? _reader;

    public DesktopAudioInstance(byte[] oggData, float volume)
    {
        var stream = new MemoryStream(oggData);
        _reader = new VorbisWaveReader(stream, false);
        _output = new WaveOutEvent();
        _output.Init(_reader);
        _output.Volume = float.Clamp(volume, 0f, 1f);
    }

    public void Play() => _output?.Play();

    public void Stop() => _output?.Stop();

    public void SetVolume(float volume)
    {
        if (_output is not null)
            _output.Volume = float.Clamp(volume, 0f, 1f);
    }

    public void Dispose()
    {
        _output?.Dispose();
        _reader?.Dispose();
    }
}
