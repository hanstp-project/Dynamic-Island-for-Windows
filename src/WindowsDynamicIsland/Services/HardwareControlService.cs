using System;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WindowsDynamicIsland.Services;

/// <summary>
/// Controls physical/virtual hardware properties: screen brightness and master audio volume.
/// Uses Windows Core Audio (WASAPI COM) for zero-latency volume control and WMI for monitor brightness.
/// </summary>
public static class HardwareControlService
{
    // ==========================================
    // BRIGHTNESS (WMI)
    // ==========================================

    public static int GetBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementObject mo in searcher.Get())
            {
                if (mo["CurrentBrightness"] != null)
                    return Convert.ToInt32(mo["CurrentBrightness"]);
            }
        }
        catch { }
        return 80;
    }

    public static void SetBrightness(int brightness)
    {
        brightness = Math.Clamp(brightness, 0, 100);
        Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                foreach (ManagementObject mo in searcher.Get())
                {
                    mo.InvokeMethod("WmiSetBrightness", new object[] { 1, (byte)brightness });
                    break;
                }
            }
            catch { }
        });
    }

    // ==========================================
    // VOLUME & MUTE (Windows Core Audio)
    // ==========================================

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
        int GetDevice(string pwstrId, out IMMDevice ppDevice);
        int RegisterEndpointNotificationCallback(IntPtr pClient);
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
        int GetId(out IntPtr ppstrId);
        int GetState(out int pdwState);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        int VolumeStepUp(ref Guid pguidEventContext);
        int VolumeStepDown(ref Guid pguidEventContext);
        int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    private static IAudioEndpointVolume? GetEndpointVolume()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            int hr = enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out var device);
            if (hr != 0 || device == null) return null;

            var iid = typeof(IAudioEndpointVolume).GUID;
            hr = device.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out var o);
            if (hr == 0 && o is IAudioEndpointVolume epv) return epv;
        }
        catch { }
        return null;
    }

    public static int GetVolume()
    {
        var epv = GetEndpointVolume();
        if (epv != null)
        {
            try
            {
                epv.GetMasterVolumeLevelScalar(out float level);
                return (int)Math.Round(level * 100);
            }
            catch { }
        }
        return 50;
    }

    public static void SetVolume(int volume)
    {
        volume = Math.Clamp(volume, 0, 100);
        var epv = GetEndpointVolume();
        if (epv != null)
        {
            try
            {
                var guid = Guid.Empty;
                epv.SetMasterVolumeLevelScalar(volume / 100f, ref guid);
            }
            catch { }
        }
    }

    public static bool GetMute()
    {
        var epv = GetEndpointVolume();
        if (epv != null)
        {
            try
            {
                epv.GetMute(out bool mute);
                return mute;
            }
            catch { }
        }
        return false;
    }

    public static void SetMute(bool mute)
    {
        var epv = GetEndpointVolume();
        if (epv != null)
        {
            try
            {
                var guid = Guid.Empty;
                epv.SetMute(mute, ref guid);
            }
            catch { }
        }
    }
}
