using YSMViewer.Models.Document;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services.Audio;

public sealed class AnimationAudioService : IAnimationAudioHost, IDisposable
{
    private readonly Dictionary<string, byte[]> _soundFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPlatformAudioPlayer _player;
    private readonly Dictionary<string, IAudioInstance> _activeSounds = [];
    private float _volume = 1f;
    private bool _isMuted;

    public AnimationAudioService(IPlatformAudioPlayer player, IReadOnlyList<YsmSoundResource> sounds)
    {
        _player = player;
        foreach (var snd in sounds)
        {
            var key = NormalizeSoundName(snd.Name);
            if (!_soundFiles.ContainsKey(key))
                _soundFiles[key] = snd.Data;
        }

        foreach (var (key, data) in _soundFiles)
            _player.PreDecode(data, key);
    }

    private static string NormalizeSoundName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path.AsSpan()).ToString();
        var lastSlash = name.LastIndexOf('/');
        if (lastSlash >= 0)
            name = name[(lastSlash + 1)..];
        lastSlash = name.LastIndexOf('\\');
        if (lastSlash >= 0)
            name = name[(lastSlash + 1)..];
        return name;
    }

    public void PlaySound(string soundName)
    {
        if (_isMuted) return;

        var key = NormalizeSoundName(soundName);
        if (!_soundFiles.TryGetValue(key, out _))
        {
            var match = _soundFiles.Keys.FirstOrDefault(
                k => k.Contains(key, StringComparison.OrdinalIgnoreCase));
            if (match is null) return;
            key = match;
        }

        var instance = _player.PlayPcm(key, _volume);
        _activeSounds[soundName] = instance;
    }

    public void StopSound(string soundName)
    {
        if (_activeSounds.TryGetValue(soundName, out var instance))
        {
            instance.Stop();
            _activeSounds.Remove(soundName);
        }
        else
        {
            var key = NormalizeSoundName(soundName);
            var match = _activeSounds.Keys.FirstOrDefault(
                k => NormalizeSoundName(k).Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                _activeSounds[match].Stop();
                _activeSounds.Remove(match);
            }
        }
    }

    public void StopAllSounds()
    {
        foreach (var instance in _activeSounds.Values)
            instance.Stop();
        _activeSounds.Clear();
    }

    public void SetVolume(float volume)
    {
        _volume = float.Clamp(volume, 0f, 1f);
        foreach (var instance in _activeSounds.Values)
            instance.SetVolume(_volume);
    }

    public void SetMuted(bool muted)
    {
        _isMuted = muted;
        if (muted) StopAllSounds();
    }

    public void Dispose()
    {
        StopAllSounds();
        _player.Dispose();
    }
}
