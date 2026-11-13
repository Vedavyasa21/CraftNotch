using System;
using System.Runtime.InteropServices;

namespace Scout.Services;

public class AudioService : IDisposable
{
    public event Action<float, bool>? VolumeChanged;
    public event Action<bool>? MicMuteChanged;

    private IAudioEndpointVolume? _renderVolume;
    private IAudioEndpointVolume? _captureVolume;
    private AudioEndpointVolumeCallback? _renderCallback;

    public float CurrentVolume { get; private set; } = 50f;
    public bool IsMuted { get; private set; }
    public bool IsMicMuted { get; private set; }

    public void Initialize()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();

            // Default audio playback device (eRender, eMultimedia)
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out var renderDevice) == 0 && renderDevice != null)
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                renderDevice.Activate(ref iid, 1, IntPtr.Zero, out var obj);
                _renderVolume = obj as IAudioEndpointVolume;

                if (_renderVolume != null)
                {
                    _renderVolume.GetMasterVolumeLevelScalar(out float level);
                    _renderVolume.GetMute(out bool mute);
                    CurrentVolume = level * 100f;
                    IsMuted = mute;

                    _renderCallback = new AudioEndpointVolumeCallback(this);
                    _renderVolume.RegisterControlChangeNotify(_renderCallback);
                }
            }

            // Default audio recording device (eCapture, eCommunications / eMultimedia)
            if (enumerator.GetDefaultAudioEndpoint(1, 1, out var captureDevice) == 0 && captureDevice != null)
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                captureDevice.Activate(ref iid, 1, IntPtr.Zero, out var obj);
                _captureVolume = obj as IAudioEndpointVolume;

                if (_captureVolume != null)
                {
                    _captureVolume.GetMute(out bool micMute);
                    IsMicMuted = micMute;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioService] Init failed: {ex.Message}");
        }
    }

    public void SetVolume(float volume0to100)
    {
        if (_renderVolume != null)
        {
            float scalar = Math.Clamp(volume0to100 / 100f, 0f, 1f);
            _renderVolume.SetMasterVolumeLevelScalar(scalar, Guid.Empty);
            CurrentVolume = volume0to100;
        }
    }

    public void ToggleMute()
    {
        if (_renderVolume != null)
        {
            _renderVolume.GetMute(out bool mute);
            _renderVolume.SetMute(!mute, Guid.Empty);
            IsMuted = !mute;
            VolumeChanged?.Invoke(CurrentVolume, IsMuted);
        }
    }

    public void ToggleMicMute()
    {
        if (_captureVolume != null)
        {
            _captureVolume.GetMute(out bool mute);
            _captureVolume.SetMute(!mute, Guid.Empty);
            IsMicMuted = !mute;
            MicMuteChanged?.Invoke(IsMicMuted);
        }
    }

    internal void OnNotifyVolume(float level, bool mute)
    {
        CurrentVolume = level * 100f;
        IsMuted = mute;
        VolumeChanged?.Invoke(CurrentVolume, IsMuted);
    }

    public void Dispose()
    {
        try
        {
            if (_renderVolume != null && _renderCallback != null)
            {
                _renderVolume.UnregisterControlChangeNotify(_renderCallback);
            }
        }
        catch { }
    }

    // COM Interop definitions
    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();
        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig]
        int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig]
        int GetChannelCount(out int pnChannelCount);
        [PreserveSig]
        int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
        [PreserveSig]
        int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
        [PreserveSig]
        int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig]
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
        [PreserveSig]
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, Guid pguidEventContext);
        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    [Guid("657804FA-D6AD-4496-8A60-352752AF4F4E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
        void OnNotify(IntPtr pNotify);
    }

    private class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
    {
        private readonly AudioService _owner;
        public AudioEndpointVolumeCallback(AudioService owner) => _owner = owner;

        public void OnNotify(IntPtr pNotify)
        {
            try
            {
                // struct AUDIO_VOLUME_NOTIFICATION_DATA: Guid guidEventContext, BOOL bMuted, float fMasterVolume...
                bool bMuted = Marshal.ReadInt32(pNotify, 16) != 0;
                byte[] raw = new byte[4];
                Marshal.Copy(pNotify + 20, raw, 0, 4);
                float fMasterVolume = BitConverter.ToSingle(raw, 0);
                _owner.OnNotifyVolume(fMasterVolume, bMuted);
            }
            catch { }
        }
    }
}
