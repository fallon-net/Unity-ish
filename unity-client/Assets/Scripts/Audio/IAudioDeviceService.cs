namespace UnityIsh.Audio
{
    public interface IAudioDeviceService
    {
        string[] GetInputDevices();
        string[] GetOutputDevices();
        bool SetInputDevice(string deviceId);
        bool SetOutputDevice(string deviceId);
        string GetCurrentInputDevice();
        string GetCurrentOutputDevice();
    }
}
