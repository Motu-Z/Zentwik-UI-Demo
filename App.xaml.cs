using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZentwikThemeDemo.Services;

namespace ZentwikThemeDemo;

/// <summary>
/// 应用入口。
/// </summary>
public partial class App : Application
{
    // WinExe 没有自带控制台。自检模式需要把结果写到调用方能读到的地方，
    // 所以既尝试附加父进程控制台，也落一份到临时目录的文件 ——
    // CI runner 里 stdout 常被重定向，文件更可靠。
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ============================================================
        // 全局禁用原生虚线焦点框
        //
        // WPF 的默认焦点视觉是系统主题画的一圈点划线矩形。
        // 逐个控件设 FocusVisualStyle="{x:Null}" 解决不了 ——
        // x:Null 表示「没有自定义焦点视觉」，WPF 会回退到系统默认，虚线照旧。
        // 这里装一个全局类处理器，把任何漏设的控件的系统默认焦点样式
        // 替换成空模板，从根上保证「应用内没有虚线」。
        // ============================================================
        FocusVisualKiller.Initialize();

        // ---- 托盘探针（诊断用）----
        // --trayprobe: 只创建托盘图标，把结果打印到控制台后退出。
        // 用来在没有可视化环境的机器上确认 Shell_NotifyIcon 是否真的成功。
        if (e.Args.Contains("--trayprobe"))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);

            AppThemeService.Instance.Initialize();

            var probePalette = LogoPaletteFactory.BuildForTray(
                darkTaskbar: !SystemTheme.IsSystemLightTheme());

            using var probeTray = new TrayIconService();
            bool probeOk = probeTray.Initialize(
                AppThemeService.Instance.BuildLogoDrawing,
                probePalette.Body,
                probePalette.Shadow);

            Console.WriteLine($"trayprobe: available  = {probeOk}");
            Console.WriteLine($"trayprobe: pixelSize  = {probeTray.RenderedPixelSize}");
            Console.WriteLine($"trayprobe: body       = {probePalette.Body}");
            Console.WriteLine($"trayprobe: shadow     = {probePalette.Shadow}");
            Console.Out.Flush();

            Shutdown(0);
            return;
        }

        // ---- 命令行自检模式 ----
        // --selftest: 颜色算法断言（纯函数，不需要窗口）
        // --itest   : 集成断言（需要真实 Application 与资源字典）
        if (e.Args.Contains("--selftest") || e.Args.Contains("--itest"))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);

            var sb = new StringBuilder();
            int failures = 0;

            if (e.Args.Contains("--selftest"))
            {
                failures += ColorMathSelfTest.Run(sb);
            }

            if (e.Args.Contains("--itest"))
            {
                // 集成测试需要资源字典已就位
                AppThemeService.Instance.Initialize();
                failures += IntegrationTest.Run(sb);
            }

            sb.AppendLine(failures == 0 ? ">>> 全部通过" : $">>> {failures} 项失败");

            Console.Write(sb.ToString());
            Console.Out.Flush();

            string report = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zentwik-selftest.txt");
            System.IO.File.WriteAllText(report, sb.ToString());

            Shutdown(failures == 0 ? 0 : 1);
            return;
        }

        // 关闭时退出进程（避免后台线程残留）
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        // ============================================================
        // 系统托盘
        //
        // 截图模式不接托盘 —— 否则每次离屏渲染都会往系统托盘
        // 塞一个图标出来，污染用户环境。
        // ============================================================
        bool isScreenshotMode = e.Args.Contains("--screenshot");
        if (!isScreenshotMode)
        {
            SetupTray(window);
        }

        // ---- 离屏截图模式 ----
        // 用 WPF 自己的 RenderTargetBitmap 渲染，不依赖屏幕抓取：
        // 不受窗口遮挡、桌面缩放、多显示器影响，结果可复现。
        int shotIndex = Array.FindIndex(e.Args, a => a == "--screenshot");
        if (shotIndex >= 0)
        {
            string outPath = shotIndex + 1 < e.Args.Length
                ? e.Args[shotIndex + 1]
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zentwik-shot.png");

            // 主题可通过 --theme=light|dark 指定
            string? themeArg = e.Args.FirstOrDefault(a => a.StartsWith("--theme="));
            if (themeArg is not null)
            {
                var mode = themeArg["--theme=".Length..].Equals("light", StringComparison.OrdinalIgnoreCase)
                    ? AppTheme.Light : AppTheme.Dark;
                AppThemeService.Instance.SetThemeMode(mode);
            }

            // 可选：--page=Misc|Tuning|Process|Config|Behavior|Logs 指定要渲染的页面
            string? pageArg = e.Args.FirstOrDefault(a => a.StartsWith("--page="));
            if (pageArg is not null)
            {
                window.NavigateTo(pageArg["--page=".Length..]);
            }

            // 可选：--size=1180x1400 指定离屏渲染尺寸。
            // 页面比窗口高时（例如调校页的三卡版式），
            // 只按窗口尺寸拍会拍不到折叠线以下的内容。
            string? sizeArg = e.Args.FirstOrDefault(a => a.StartsWith("--size="));
            if (sizeArg is not null)
            {
                string[] parts = sizeArg["--size=".Length..].Split('x', 'X');

                if (parts.Length == 2 &&
                    double.TryParse(parts[0], out double shotWidth) &&
                    double.TryParse(parts[1], out double shotHeight))
                {
                    window.Width = shotWidth;
                    window.Height = shotHeight;
                }
            }

            // 可选：--accent=system|custom 指定色彩模式
            string? accentArg = e.Args.FirstOrDefault(a => a.StartsWith("--accent="));
            if (accentArg is not null)
            {
                AppThemeService.Instance.SetAccentMode(
                    accentArg["--accent=".Length..].Equals("system", StringComparison.OrdinalIgnoreCase)
                        ? AccentSource.System
                        : AccentSource.Custom);
            }

            // 可选：--scroll=bottom 把页面滚到底后再拍（拍折叠线以下的内容）
            bool scrollToBottom = e.Args.Contains("--scroll=bottom");

            window.Dispatcher.InvokeAsync(() =>
            {
                if (scrollToBottom)
                {
                    // 必须先跑一次布局：ScrollViewer 的 Extent（内容总高）
                    // 要等布局算完才有值，否则 ScrollToEnd 是个空操作。
                    window.UpdateLayout();
                    window.ScrollPageToEnd();
                    window.UpdateLayout();
                }

                // --profile : 打开配置方案模态大窗后再拍
                // 大窗是 Grid 叠层（在主视觉树里），能直接拍到 ——
                // 这与 ToolTip / 下拉框的 Popup 不同。
                if (e.Args.Contains("--profile"))
                {
                    ViewModels.ProfileSession.Instance.IsOverlayOpen = true;
                    window.UpdateLayout();
                }

                // --tooltip : 额外叠加一个 ToolTip 预览（Popup 拍不到，只能这样验）
                if (e.Args.Contains("--tooltip"))
                {
                    ScreenshotHelper.CaptureWithTooltip(window, outPath);
                }
                else
                {
                    ScreenshotHelper.Capture(window, outPath);
                }

                // --traymenu : 额外输出托盘菜单与托盘图标的离屏渲染
                if (e.Args.Contains("--traymenu"))
                {
                    ScreenshotHelper.CaptureTrayMenu(outPath);
                    ScreenshotHelper.CaptureTrayIcon(outPath);
                }

                Shutdown(0);
            });
        }
    }

    /// <summary>
    /// 托盘图标实例 —— 必须持有引用，否则会被 GC 回收，
    /// 消息窗口随之销毁，托盘图标也会消失。
    /// </summary>
    private TrayIconService? _tray;

    /// <summary>
    /// 托盘菜单宿主 —— 同样必须持有引用，
    /// 否则 0x1 的宿主窗口被回收，Popup 就没有 owner 而无法显示。
    /// </summary>
    private TrayMenuPresenter? _trayMenu;

    /// <summary>
    /// 创建托盘图标并接上主窗口的显示 / 退出。
    /// </summary>
    private void SetupTray(MainWindow window)
    {
        var theme = AppThemeService.Instance;

        var tray = new TrayIconService();

        // 托盘图标用中性灰，**不跟随强调色** ——
        // 任务栏背景由系统决定，用户挑的强调色在那里没有意义。
        // 深色任务栏需要亮图标才看得见，浅色任务栏需要暗图标。
        var trayPalette = LogoPaletteFactory.BuildForTray(
            darkTaskbar: !SystemTheme.IsSystemLightTheme());

        if (!tray.Initialize(theme.BuildLogoDrawing, trayPalette.Body, trayPalette.Shadow))
        {
            // 托盘不可用 → 关闭按钮必须正常退出，
            // 否则用户会把应用「关掉」却再也打不开。
            Debug.WriteLine("[App] 托盘图标创建失败，关闭窗口将直接退出。");
            return;
        }

        _tray = tray;

        void ShowMainWindow()
        {
            window.Show();
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
            window.Activate();
        }

        tray.ShowRequested += (_, _) => ShowMainWindow();

        // ---- 托盘右键菜单（WPF 自绘）----
        // 菜单挂在独立的 0x1 宿主窗口上，
        // 所以主窗口收进托盘（被隐藏）时菜单照样能弹出来。
        var menuPresenter = new TrayMenuPresenter();
        _trayMenu = menuPresenter;

        menuPresenter.ShowRequested += (_, _) => ShowMainWindow();
        menuPresenter.ExitRequested += (_, _) => window.ExitApplication();

        tray.MenuRequested += (_, screenPoint) => menuPresenter.Open(screenPoint);

        // 托盘可用后，关闭按钮改为收进托盘
        window.MinimizeToTrayEnabled = true;

        // 系统任务栏明暗变化 → 托盘图标跟着换配色。
        // 【为什么不是订阅 ThemeChanged】
        // ThemeChanged 表示「应用内明暗」；而任务栏明暗是系统决定的，
        // 用户把应用设成深色、系统任务栏是浅色时，托盘仍需要深色图标。
        theme.SystemThemeChanged += (_, _) =>
        {
            var palette = LogoPaletteFactory.BuildForTray(
                darkTaskbar: !SystemTheme.IsSystemLightTheme());
            tray.UpdateColors(palette.Body, palette.Shadow);
        };

        Exit += (_, _) =>
        {
            _tray?.Dispose();
            _trayMenu?.Dispose();
        };
    }
}
