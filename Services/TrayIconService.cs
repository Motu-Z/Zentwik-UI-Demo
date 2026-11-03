using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ZentwikThemeDemo.Services;

/// <summary>
/// 系统托盘图标。
///
/// </summary>
/// <remarks>
/// 【为什么不用 NotifyIcon 之类的第三方库】
/// WPF 没有内置托盘控件，加第三方包会带来依赖与体积问题。
/// 而托盘本身只需要三样东西：
///   1. 一个能收消息的窗口（托盘回调消息要发给它）
///   2. Shell_NotifyIcon 加/删图标
///   3. 一个 Win32 弹出菜单
/// 全部是几十行 P/Invoke，自己写反而更可控。
///
/// 【消息窗口为什么不复用 MainWindow】
/// 主窗口是 WPF Window，它的 WndProc 已经在处理拖拽与最大化修正，
/// 再往里塞托盘消息会让外壳层职责变杂。
/// 这里单独建一个 1×1 的不可见 HwndSource 当消息泵，
/// 生命周期跟服务走，互不干扰。
///
/// 【托盘图标尺寸】
/// 用 16×16 —— Windows 托盘在 100% 缩放下就是 16px，
/// 高 DPI 下系统会自动缩放 PNG 帧。
/// </remarks>
public sealed class TrayIconService : IDisposable
{
    /// <summary>托盘回调消息（自定义 WM_APP+1）。</summary>
    private const int WM_TRAY = 0x8001;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    /// <summary>空消息 —— 弹出菜单关闭后的经典收尾动作。</summary>
    private const uint WM_NULL = 0x0000;

    private const int CmdShow = 1001;
    private const int CmdHide = 1002;
    private const int CmdExit = 1003;

    /// <summary>托盘图标的内部 ID。</summary>
    private const int TrayIconId = 1;

    private HwndSource? _source;
    private IntPtr _iconHandle;
    private bool _added;

    /// <summary>请求显示并激活主窗口。</summary>
    public event EventHandler? ShowRequested;

    /// <summary>请求隐藏主窗口。</summary>
    public event EventHandler? HideRequested;

    /// <summary>请求退出应用。</summary>
    public event EventHandler? ExitRequested;

    /// <summary>托盘图标是否已成功创建。</summary>
    public bool IsAvailable => _added;

    /// <summary>
    /// 创建托盘图标。
    /// </summary>
    /// <param name="iconPath">.ico 文件的完整路径。</param>
    /// <returns>成功返回 true；失败返回 false（调用方应据此关闭「最小化到托盘」）。</returns>
    public bool Initialize(string iconPath)
    {
        try
        {
            // 1×1 的不可见消息窗口
            var parameters = new HwndSourceParameters("ZentwikTrayMessageWindow")
            {
                Width = 1,
                Height = 1,
                PositionX = -32000,
                PositionY = -32000,
                WindowStyle = 0,
            };

            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);

            // 加载图标：优先 16×16，失败再退回系统默认尺寸
            _iconHandle = NativeMethods.LoadImage(
                IntPtr.Zero, iconPath,
                NativeMethods.IMAGE_ICON, 16, 16,
                NativeMethods.LR_LOADFROMFILE);

            if (_iconHandle == IntPtr.Zero)
            {
                _iconHandle = NativeMethods.LoadImage(
                    IntPtr.Zero, iconPath,
                    NativeMethods.IMAGE_ICON, 0, 0,
                    NativeMethods.LR_LOADFROMFILE | NativeMethods.LR_DEFAULTSIZE);
            }

            if (_iconHandle == IntPtr.Zero)
            {
                Debug.WriteLine("[TrayIconService] 图标加载失败，图标路径：" + iconPath);
                return false;
            }

            var data = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _source.Handle,
                uID = TrayIconId,
                uFlags = (int)(NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP),
                uCallbackMessage = WM_TRAY,
                hIcon = _iconHandle,
                szTip = "Zentwik",
            };

            _added = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data);
            if (!_added)
            {
                Debug.WriteLine("[TrayIconService] Shell_NotifyIcon(NIM_ADD) 失败");
            }

            return _added;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[TrayIconService] 初始化异常：" + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 消息窗口的窗口过程。
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_TRAY) return IntPtr.Zero;

        switch (lParam.ToInt32())
        {
            case WM_LBUTTONUP:
            case WM_LBUTTONDBLCLK:
                ShowRequested?.Invoke(this, EventArgs.Empty);
                break;

            case WM_RBUTTONUP:
                ShowContextMenu();
                break;
        }

        handled = true;
        return IntPtr.Zero;
    }

    /// <summary>
    /// 在鼠标位置弹出托盘右键菜单。
    /// </summary>
    private void ShowContextMenu()
    {
        if (_source is null) return;

        IntPtr hMenu = NativeMethods.CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, (UIntPtr)CmdShow, "显示主窗口");
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, (UIntPtr)CmdHide, "隐藏主窗口");
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, (UIntPtr)CmdExit, "退出 Zentwik");

            NativeMethods.GetCursorPos(out var pt);

            // 菜单必须在前台窗口里弹出，否则点击别处它不会自动消失
            NativeMethods.SetForegroundWindow(_source.Handle);

            int cmd = (int)NativeMethods.TrackPopupMenu(
                hMenu,
                NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY,
                pt.X, pt.Y, 0, _source.Handle, IntPtr.Zero);

            switch (cmd)
            {
                case CmdShow:
                    ShowRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case CmdHide:
                    HideRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case CmdExit:
                    ExitRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[TrayIconService] 弹出菜单异常：" + ex.Message);
        }
        finally
        {
            NativeMethods.DestroyMenu(hMenu);

            // 经典收尾：菜单关掉后给窗口发一条空消息，
            // 否则某些情况下菜单会残留在屏幕上直到下次点击。
            NativeMethods.PostMessage(_source.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        }
    }

    /// <summary>移除托盘图标并释放资源。</summary>
    public void Dispose()
    {
        if (_added && _source is not null)
        {
            var data = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _source.Handle,
                uID = TrayIconId,
            };

            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);
            _added = false;
        }

        if (_iconHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        _source?.Dispose();
        _source = null;
    }
}
