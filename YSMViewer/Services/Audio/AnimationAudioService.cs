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

    public event Action<string>? SoundPlaybackStopped;

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

        StopAllSounds();

        var key = NormalizeSoundName(soundName);
        if (!_soundFiles.TryGetValue(key, out _))
        {
            var match = _soundFiles.Keys.FirstOrDefault(
                k => k.Contains(key, StringComparison.OrdinalIgnoreCase));
            if (match is null) return;
            key = match;
        }

        var instance = _player.PlayPcm(key, _volume);
        instance.PlaybackStopped += () => OnPlaybackStopped(soundName);
        _activeSounds[soundName] = instance;
    }

    public void StopSound(string soundName)
    {
        if (_activeSounds.TryGetValue(soundName, out var instance))
        {
            _activeSounds.Remove(soundName);
            instance.Stop();
            SoundPlaybackStopped?.Invoke(soundName);
        }
        else
        {
            var key = NormalizeSoundName(soundName);
            var match = _activeSounds.Keys.FirstOrDefault(
                k => NormalizeSoundName(k).Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                var matchedInstance = _activeSounds[match];
                _activeSounds.Remove(match);
                matchedInstance.Stop();
                SoundPlaybackStopped?.Invoke(match);
            }
        }
    }

    public void PauseSound(string soundName)
    {
        if (_activeSounds.TryGetValue(soundName, out var instance))
        {
            instance.Pause();
            return;
        }

        var key = NormalizeSoundName(soundName);
        var match = _activeSounds.Keys.FirstOrDefault(
            k => NormalizeSoundName(k).Equals(key, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            _activeSounds[match].Pause();
    }

    public void ResumeSound(string soundName)
    {
        if (_isMuted) return;

        if (_activeSounds.TryGetValue(soundName, out var instance))
        {
            instance.Resume();
            return;
        }

        var key = NormalizeSoundName(soundName);
        var match = _activeSounds.Keys.FirstOrDefault(
            k => NormalizeSoundName(k).Equals(key, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            _activeSounds[match].Resume();
    }

    public void StopAllSounds()
    {
        var activeSounds = _activeSounds.ToArray();
        _activeSounds.Clear();

        foreach (var (_, instance) in activeSounds)
            instance.Stop();
        foreach (var (soundName, _) in activeSounds)
            SoundPlaybackStopped?.Invoke(soundName);
    }

    private void OnPlaybackStopped(string soundName)
    {
        if (_activeSounds.Remove(soundName))
            SoundPlaybackStopped?.Invoke(soundName);
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
