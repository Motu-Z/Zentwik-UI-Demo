using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Windows;
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

            // 可选：--accent=system|custom 指定色彩模式
            string? accentArg = e.Args.FirstOrDefault(a => a.StartsWith("--accent="));
            if (accentArg is not null)
            {
                AppThemeService.Instance.SetAccentMode(
                    accentArg["--accent=".Length..].Equals("system", StringComparison.OrdinalIgnoreCase)
                        ? AccentSource.System
                        : AccentSource.Custom);
            }

            window.Dispatcher.InvokeAsync(() =>
            {
                ScreenshotHelper.Capture(window, outPath);
                Shutdown(0);
            });
        }
    }
}
