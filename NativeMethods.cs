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

    // ============================================================
    //  系统托盘（Shell_NotifyIcon）
    // ============================================================

    /// <summary>添加图标到托盘。</summary>
    public const uint NIM_ADD = 0x00000000;

    /// <summary>修改托盘图标。</summary>
    public const uint NIM_MODIFY = 0x00000001;

    /// <summary>从托盘移除图标。</summary>
    public const uint NIM_DELETE = 0x00000002;

    /// <summary>结构体包含回调消息。</summary>
    public const uint NIF_MESSAGE = 0x00000001;

    /// <summary>结构体包含图标句柄。</summary>
    public const uint NIF_ICON = 0x00000002;

    /// <summary>结构体包含提示文本。</summary>
    public const uint NIF_TIP = 0x00000004;

    /// <summary>菜单项：普通字符串。</summary>
    public const uint MF_STRING = 0x00000000;

    /// <summary>菜单项：分隔线。</summary>
    public const uint MF_SEPARATOR = 0x00000800;

    /// <summary>弹出菜单：右键对齐。</summary>
    public const uint TPM_RIGHTBUTTON = 0x0002;

    /// <summary>弹出菜单：直接返回被选中的命令 ID（不发送 WM_COMMAND）。</summary>
    public const uint TPM_RETURNCMD = 0x0100;

    /// <summary>弹出菜单：不发送 WM_COMMAND 通知（配合 TPM_RETURNCMD）。</summary>
    public const uint TPM_NONOTIFY = 0x0080;

    /// <summary>LoadImage 的类型：图标。</summary>
    public const uint IMAGE_ICON = 1;

    /// <summary>LoadImage 的标志：从文件加载。</summary>
    public const uint LR_LOADFROMFILE = 0x00000010;

    /// <summary>LoadImage 的标志：使用系统默认尺寸。</summary>
    public const uint LR_DEFAULTSIZE = 0x00000040;

    /// <summary>NOTIFYICONDATA —— 托盘图标描述结构。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public int dwState;
        public int dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadImage(
        IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AppendMenu(
        IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint TrackPopupMenu(
        IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>屏幕坐标点。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }
}
