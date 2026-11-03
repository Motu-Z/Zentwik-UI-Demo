using System.Runtime.InteropServices;

namespace ZentwikThemeDemo;

/// <summary>
/// 少量 Win32 互操作：仅用于修正无边框窗口的最大化工作区。
/// </summary>
/// <remarks>
/// 结构体刻意用 WPF 自己的 System.Windows.Point / System.Windows.Rect，
/// 而不是 System.Drawing.Point —— 后者需要额外引用 System.Drawing.Common，
/// 且在 .NET 10 上跨平台裁剪时不友好。
/// </remarks>
internal static class NativeMethods
{
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public System.Windows.Rect rcMonitor;
        public System.Windows.Rect rcWork;
        public uint dwFlags;
    }
}
