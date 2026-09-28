using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SilentLens.Utils;

/// <summary>
/// P/Invoke для WM_GETMINMAXINFO — ограничивает Maximize рабочей областью,
/// чтобы панель задач Windows оставалась видна.
/// </summary>
public static class NativeMethods
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>
    /// Установить hook на WM_GETMINMAXINFO для окна.
    /// </summary>
    public static void ApplyWorkAreaConstraint(Window window)
    {
        var helper = new WindowInteropHelper(window);

        if (helper.Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (_, _) =>
            {
                var h = new WindowInteropHelper(window).Handle;
                var src = HwndSource.FromHwnd(h);
                src?.AddHook(WindowProc);
            };
            return;
        }

        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WindowProc);
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref monitorInfo);

            var rcWorkArea = monitorInfo.rcWork;
            var rcMonitorArea = monitorInfo.rcMonitor;

            // Максимальная позиция и размер — по рабочей области (без панели задач)
            mmi.ptMaxPosition.X = rcWorkArea.Left - rcMonitorArea.Left;
            mmi.ptMaxPosition.Y = rcWorkArea.Top - rcMonitorArea.Top;
            mmi.ptMaxSize.X = rcWorkArea.Right - rcWorkArea.Left;
            mmi.ptMaxSize.Y = rcWorkArea.Bottom - rcWorkArea.Top;

            // Минимальный размер окна
            mmi.ptMinTrackSize.X = 800;
            mmi.ptMinTrackSize.Y = 480;
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }
}