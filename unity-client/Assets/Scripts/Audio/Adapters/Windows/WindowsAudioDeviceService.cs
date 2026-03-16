using UnityIsh.Audio;

namespace UnityIsh.Audio.Adapters.Windows
{
    // Placeholder implementation. Replace with a native plugin-backed service.
    public sealed class WindowsAudioDeviceService : IAudioDeviceService
    {
        public string[] GetInputDevices() => new[] { "default-input" };
        public string[] GetOutputDevices() => new[] { "default-output" };
        public bool SetInputDevice(string deviceId) => !string.IsNullOrWhiteSpace(deviceId);
        public bool SetOutputDevice(string deviceId) => !string.IsNullOrWhiteSpace(deviceId);
        public string GetCurrentInputDevice() => "default-input";
        public string GetCurrentOutputDevice() => "default-output";
    }
}
