using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace WindowsDynamicIsland.Interop;

internal readonly record struct MonitorInfo(IntPtr Handle, NativeMethods.RECT Bounds, NativeMethods.RECT WorkArea, bool IsPrimary, double DpiScale);

/// <summary>Monitor enumeration + per-monitor DPI, and a few input helpers.</summary>
internal static class MonitorHelper
{
    /// <summary>All monitors ordered left→right, top→bottom ("display 1..N").</summary>
    public static List<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        NativeMethods.MonitorEnumProc callback = (IntPtr h, IntPtr hdc, ref NativeMethods.RECT r, IntPtr data) =>
        {
            var info = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>(), szDevice = "" };
            if (NativeMethods.GetMonitorInfo(h, ref info))
                list.Add(new MonitorInfo(h, info.rcMonitor, info.rcWork,
                    (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0, GetScale(h)));
            return true;
        };
        try { NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero); }
        catch (Exception) { /* fall through to fallback */ }
        GC.KeepAlive(callback);
        return list.OrderBy(m => m.Bounds.Left).ThenBy(m => m.Bounds.Top).ToList();
    }

    /// <param name="index">0 = primary, 1..N = display number (falls back to primary).</param>
    public static MonitorInfo Select(int index)
    {
        var all = GetMonitors();
        if (index >= 1 && index <= all.Count) return all[index - 1];
        foreach (var m in all) if (m.IsPrimary) return m;
        if (all.Count > 0) return all[0];

        var rect = new NativeMethods.RECT
        {
            Right = (int)SystemParameters.PrimaryScreenWidth,
            Bottom = (int)SystemParameters.PrimaryScreenHeight,
        };
        return new MonitorInfo(IntPtr.Zero, rect, rect, true, 1.0);
    }

    public static double GetScale(IntPtr monitor)
    {
        try
        {
            if (NativeMethods.GetDpiForMonitor(monitor, 0, out uint x, out _) == 0 && x > 0) return x / 96.0;
        }
        catch (Exception) { }
        return 1.0;
    }

    /// <summary>Opens Windows clipboard history (Win+V) in the app that currently has focus.</summary>
    public static void OpenClipboardHistory()
    {
        var inputs = new[]
        {
            Key(NativeMethods.VK_LWIN, false),
            Key(NativeMethods.VK_V, false),
            Key(NativeMethods.VK_V, true),
            Key(NativeMethods.VK_LWIN, true),
        };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT Key(ushort vk, bool up) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = up ? NativeMethods.KEYEVENTF_KEYUP : 0 },
        },
    };
}
