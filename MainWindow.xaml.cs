using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ZentwikThemeDemo.Services;
using ZentwikThemeDemo.ViewModels;
using ZentwikThemeDemo.Views;

namespace ZentwikThemeDemo;

/// <summary>
/// 应用外壳窗口。
/// </summary>
/// <remarks>
/// 【架构定位】
/// 外壳层刻意保持 Code-Behind 模式，只负责三件事：
///   1. 窗口拖拽与窗口控制按钮（最小化 / 最大化 / 关闭）
///   2. 左侧导航栏的页面路由切换（ContentControl 换 UserControl）
///   3. 承载窗口级事件（WM_GETMINMAXINFO 修正最大化工作区）
///
/// 业务逻辑一律在 ViewModel 里（见 SettingsViewModel）。
/// 之所以外壳不引入 MVVM：导航路由本质是「界面区域切换」，
/// 引入 ViewModel 只会把 ContentControl.Content 的赋值搬个家，
/// 反而多一层无信息量的间接。
/// </remarks>
public partial class MainWindow : Window
{
    private const int WM_GETMINMAXINFO = 0x0024;

    public MainWindow()
    {
        // 资源字典必须先于 InitializeComponent 就位：
        // MainWindow.xaml 里用 {StaticResource NavItemStyle} 等样式，
        // 解析 XAML 时就会去 Application.Resources 里找。
        // 而字典是在 AppThemeService.Initialize 里合并的，
        // 若 InitializeComponent 先跑，样式尚未合并 → 运行时找不到资源。
        AppThemeService.Instance.Initialize();

        InitializeComponent();

        // 版本号来自程序集元数据，不在 XAML / C# 里硬编码
        VersionText.Text = "v" + GetVersion();

        // 默认路由到「杂项」页
        Navigate("Misc");
    }

    /// <summary>
    /// 读取程序集版本号。
    /// </summary>
    private static string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(version)) return "0.0.0";

        // InformationalVersion 可能带 "+commit" 后缀，去掉只留主版本号
        int plus = version.IndexOf('+');
        return plus > 0 ? version[..plus] : version;
    }

    // ==================== 导航路由（Code-Behind） ====================

    /// <summary>
    /// 已创建的视图实例缓存。
    /// </summary>
    /// <remarks>
    /// 【为什么必须缓存，而不是每次 new 一个】
    /// 每次 new 都会重建 ViewModel，而 ViewModel 的状态会随构造重置。
    /// 用户在「杂项」页选了浅色，切到别的页面再切回来 ——
    /// 新建的 ViewModel 回到初始态，按钮显示与实际主题就对不上了。
    ///
    /// 缓存实例后，View 与 ViewModel 都只创建一次，
    /// 切页只是把同一个对象重新挂到 ContentControl 上，
    /// 状态天然保持。
    ///
    /// 【为什么缓存不带来内存问题】
    /// 六个页面各一个实例，且都是轻量占位页 / 设置页，
    /// 总占用远小于一次图片解码。
    /// </remarks>
    private readonly Dictionary<string, FrameworkElement> _viewCache = new();

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag })
        {
            Navigate(tag);
        }
    }

    /// <summary>
    /// 切换业务视图。
    /// </summary>
    private void Navigate(string tag)
    {
        // 初始 IsChecked 会在 XAML 加载阶段触发 Checked，
        // 那时 ContentHost 尚未创建（它在 XAML 里位于导航项之后）。
        // 这种早期触发直接忽略 —— 构造函数末尾会再做一次正式路由。
        if (ContentHost is null) return;

        if (!_viewCache.TryGetValue(tag, out var view))
        {
            view = CreateView(tag);
            _viewCache[tag] = view;
        }

        ContentHost.Content = view;
    }

    /// <summary>
    /// 以编程方式切换到指定页面（供离屏截图等外部入口使用）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Navigate"/> 的区别：同时同步侧栏的选中态，
    /// 让截图里侧栏高亮与内容页一致。
    /// </remarks>
    public void NavigateTo(string tag)
    {
        Navigate(tag);

        // 同步侧栏选中项（不触发 Checked → 避免递归路由）
        foreach (var radio in NavHost.Children.OfType<RadioButton>())
        {
            if (radio.Tag as string == tag)
            {
                radio.IsChecked = true;
            }
        }
    }

    /// <summary>
    /// 创建业务视图。
    /// </summary>
    private static FrameworkElement CreateView(string tag) => tag switch
    {
        "Misc"     => new SettingsView(),
        "Tuning"   => new TuningView(),
        "Process"  => new ProcessView(),
        "Config"   => new ConfigView(),
        "Behavior" => new BehaviorView(),
        "Logs"     => new LogsView(),
        _ => new PlaceholderView { Title = tag },
    };

    // ==================== 窗口控制（Code-Behind） ====================

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击标题区切换最大化
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // 窗口处于不可拖动状态（如正在最大化/还原动画中），忽略
            }
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaxIcon.Data = System.Windows.Media.Geometry.Parse(
                "M1.5 0.5 H8.5 A1,1 0 0 1 9.5 1.5 V8.5 A1,1 0 0 1 8.5 9.5 H1.5 A1,1 0 0 1 0.5 8.5 V1.5 A1,1 0 0 1 1.5 0.5 Z");
        }
        else
        {
            WindowState = WindowState.Maximized;
            // 最大化图标换成「还原」形态（两个叠放的方框）
            MaxIcon.Data = System.Windows.Media.Geometry.Parse(
                "M2.5 0.5 H9.5 V7.5 M0.5 2.5 H7.5 V9.5 H0.5 Z");
        }
    }

    // ==================== 最大化工作区修正 ====================

    /// <summary>
    /// 无边框窗口最大化时默认会盖住任务栏 —— 因为工作区被当成了整个屏幕。
    /// 这里拦截 WM_GETMINMAXINFO，把最大化尺寸改为「显示器工作区」而非「显示器整屏」。
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        }

        // 窗口句柄此时才存在 —— 挂系统主题监听
        AppThemeService.Instance.AttachWindow(this);
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };

            if (NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                mmi.ptMaxPosition.X = info.rcWork.Left - info.rcMonitor.Left;
                mmi.ptMaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top;
                mmi.ptMaxSize.X = info.rcWork.Right - info.rcWork.Left;
                mmi.ptMaxSize.Y = info.rcWork.Bottom - info.rcWork.Top;

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public System.Windows.Point ptReserved;
        public System.Windows.Point ptMaxSize;
        public System.Windows.Point ptMaxPosition;
        public System.Windows.Point ptMinTrackSize;
        public System.Windows.Point ptMaxTrackSize;
    }
}
