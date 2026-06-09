namespace YSMViewer.Services.Audio;

public interface IAudioInstance
{
    void Stop();
    void SetVolume(float volume);
}

public interface IPlatformAudioPlayer : IDisposable
{
    IAudioInstance Play(byte[] oggData, float volume);
}
