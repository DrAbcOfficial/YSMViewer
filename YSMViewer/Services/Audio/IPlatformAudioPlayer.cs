namespace YSMViewer.Services.Audio;

public interface IAudioInstance
{
    event Action? PlaybackStopped;
    void Pause();
    void Resume();
    void Stop();
    void SetVolume(float volume);
}

public interface IPlatformAudioPlayer : IDisposable
{
    IAudioInstance Play(byte[] oggData, float volume);
    IAudioInstance PlayPcm(string soundKey, float volume);
    void PreDecode(byte[] oggData, string key);
}
