using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZentwikThemeDemo.Services;

namespace ZentwikThemeDemo;

/// <summary>
/// 离屏截图工具。
/// </summary>
/// <remarks>
/// 用 WPF 自己的 RenderTargetBitmap 渲染，而不是抓屏幕：
/// 抓屏会受窗口遮挡、桌面缩放、多显示器影响，结果不可复现；
/// RenderTargetBitmap 渲染的是控件树，结果只取决于布局与资源。
///
/// 【为什么不能直接渲染 window】
/// RenderTargetBitmap 渲染 Window 会得到空白 —— Window 不是普通的
/// 布局容器，它的背景由系统合成。所以这里取 window.Content 作为渲染目标，
/// 并把 Window 自身的 Background 垫在下面，保证截图包含窗口底色。
/// </remarks>
internal static class ScreenshotHelper
{
    public static void Capture(Window window, string outputPath)
    {
        // 等待布局完成，否则可能截到还没排版的内容
        window.UpdateLayout();

        int width = (int)Math.Ceiling(window.ActualWidth);
        int height = (int)Math.Ceiling(window.ActualHeight);

        if (width <= 0 || height <= 0)
        {
            width = (int)window.Width;
            height = (int)window.Height;
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

        // 垫一层窗口底色（Window 的 Background 不在 Content 树里）
        var canvas = new DrawingVisual();
        using (var dc = canvas.RenderOpen())
        {
            dc.DrawRectangle(window.Background ?? Brushes.Transparent, null,
                            new Rect(0, 0, width, height));
        }
        bitmap.Render(canvas);

        // 再渲染窗口内容
        if (window.Content is UIElement content)
        {
            bitmap.Render(content);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    /// <summary>
    /// 离屏截图 + 叠加一个 ToolTip 预览。
    /// </summary>
    /// <remarks>
    /// 【为什么要单独做一个方法】
    /// ToolTip 真实运行时挂在 Popup 里 —— Popup 是独立的顶层窗口，
    /// 不在视觉树中，RenderTargetBitmap 根本拍不到它。
    /// 所以这里手工实例化一个 ToolTip，Measure/Arrange 让它套用模板，
    /// 再单独渲染并贴到截图的左下角，用来校验全局 ToolTip 样式的实际观感。
    /// </remarks>
    public static void CaptureWithTooltip(Window window, string outputPath)
    {
        window.UpdateLayout();

        int width = (int)Math.Ceiling(window.ActualWidth);
        int height = (int)Math.Ceiling(window.ActualHeight);

        if (width <= 0 || height <= 0)
        {
            width = (int)window.Width;
            height = (int)window.Height;
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

        var canvas = new DrawingVisual();
        using (var dc = canvas.RenderOpen())
        {
            dc.DrawRectangle(window.Background ?? Brushes.Transparent, null,
                            new Rect(0, 0, width, height));
        }
        bitmap.Render(canvas);

        if (window.Content is UIElement content)
        {
            bitmap.Render(content);
        }

        // ---- ToolTip 预览 ----
        // 给一个有限宽度：Measure(∞) 时 ToolTip 的 DesiredSize.Width 可能是 0，
        // 文字会被压成一条竖条。
        var tip = new ToolTip { Content = "在浏览器中打开项目仓库" };
        tip.Measure(new Size(320, double.PositiveInfinity));
        tip.Arrange(new Rect(0, 0, 320, tip.DesiredSize.Height));
        tip.UpdateLayout();

        int tipW = (int)Math.Ceiling(tip.ActualWidth);
        int tipH = (int)Math.Ceiling(tip.ActualHeight);

        if (tipW > 0 && tipH > 0)
        {
            var tipBitmap = new RenderTargetBitmap(tipW, tipH, 96, 96, PixelFormats.Pbgra32);
            tipBitmap.Render(tip);

            // 单独存一份，方便排查「到底渲染出来没有」
            var tipEncoder = new PngBitmapEncoder();
            tipEncoder.Frames.Add(BitmapFrame.Create(tipBitmap));
            string tipPath = Path.ChangeExtension(outputPath, null) + ".tooltip.png";
            using (var ts = File.Create(tipPath))
            {
                tipEncoder.Save(ts);
            }

            var tipVisual = new DrawingVisual();
            using (var dc = tipVisual.RenderOpen())
            {
                dc.DrawImage(tipBitmap,
                    new Rect(24, height - tipH - 24, tipW, tipH));
            }
            bitmap.Render(tipVisual);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    /// <summary>
    /// 离屏渲染托盘右键菜单，单独输出成 PNG。
    /// </summary>
    /// <remarks>
    /// 【为什么要单独渲染】
    /// 托盘菜单是 WPF 自绘的 Popup —— Popup 是独立的顶层窗口，
    /// 不在视觉树中，RenderTargetBitmap 拍不到它。
    /// 所以这里实例化一份 TrayMenu，手工 Measure/Arrange 里面的面板，
    /// 再单独渲染，用来校验菜单的实际观感（深浅两套主题）。
    ///
    /// 输出路径为 <paramref name="outputPath"/> + ".traymenu.png"。
    /// </remarks>
    public static void CaptureTrayMenu(string outputPath)
    {
        var menu = new Views.TrayMenu();

        // 面板此刻是 Popup 的 Child（逻辑父级是 Popup）。
        // 先把 Popup.Child 置空把它「摘」出来 —— 一个元素只能有一个父级，
        // 不摘出来就没法放进下面的 Canvas。
        var popup = menu.Popup;
        var panel = popup.Child as FrameworkElement;
        popup.Child = null;

        if (panel is null) return;

        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        panel.Arrange(new Rect(panel.DesiredSize));
        panel.UpdateLayout();

        // 【为什么套一层 wrapper】
        // 面板带 Margin=10，是给 DropShadowEffect 的阴影预留的空间；
        // 而 ActualWidth 不含 Margin（176 vs Desired 196），
        // 直接按 ActualWidth 建位图会把右侧和下侧的阴影裁掉。
        // 所以用一个与 DesiredSize 等大的 Canvas 当画布，
        // 把面板按 Margin 摆进去，整体渲染。
        var canvasHost = new Canvas
        {
            Width = panel.DesiredSize.Width,
            Height = panel.DesiredSize.Height,
            Background = Brushes.Transparent,
        };

        Canvas.SetLeft(panel, panel.Margin.Left);
        Canvas.SetTop(panel, panel.Margin.Top);
        canvasHost.Children.Add(panel);

        canvasHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        canvasHost.Arrange(new Rect(canvasHost.DesiredSize));
        canvasHost.UpdateLayout();

        int w = (int)Math.Ceiling(canvasHost.ActualWidth);
        int h = (int)Math.Ceiling(canvasHost.ActualHeight);

        if (w <= 0 || h <= 0) return;

        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvasHost);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        string path = Path.ChangeExtension(outputPath, null) + ".traymenu.png";

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>
    /// 离屏渲染托盘图标预览。
    /// </summary>
    /// <remarks>
    /// 两行：深色任务栏底 + 亮色图标 / 浅色任务栏底 + 暗色图标。
    /// 每行放 16 / 22 / 32 三个**物理像素**尺寸 ——
    /// 16 对应 100% 缩放，22 对应 140%（本机），32 对应 200%。
    /// 矢量渲染，三个尺寸都是 1:1 落点，不该出现任何缩放模糊。
    /// </remarks>
    public static void CaptureTrayIcon(string outputPath)
    {
        var theme = AppThemeService.Instance;
        int[] sizes = { 16, 22, 32 };

        const int pad = 16;
        const int cell = 56;
        const int rowH = 56;

        int width = pad * 2 + cell * sizes.Length;
        int height = pad * 2 + rowH * 2;

        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.Transparent,
        };

        for (int row = 0; row < 2; row++)
        {
            bool darkTaskbar = row == 0;
            var palette = LogoPaletteFactory.BuildForTray(darkTaskbar);

            // 模拟任务栏条
            var bar = new Border
            {
                Background = new SolidColorBrush(darkTaskbar
                    ? Color.FromRgb(0x20, 0x20, 0x20)
                    : Color.FromRgb(0xEC, 0xEC, 0xEC)),
                Width = width - pad * 2,
                Height = 40,
            };
            Canvas.SetLeft(bar, pad);
            Canvas.SetTop(bar, pad + row * rowH);
            canvas.Children.Add(bar);

            var drawing = theme.BuildLogoDrawing(palette.Body, palette.Shadow);

            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                var image = new Image
                {
                    Source = TrayIconRenderer.RenderDrawing(drawing, size),
                    Width = size,
                    Height = size,
                };
                Canvas.SetLeft(image, pad + 24 + i * cell);
                Canvas.SetTop(image, pad + row * rowH + (40 - size) / 2);
                canvas.Children.Add(image);
            }
        }

        canvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        canvas.Arrange(new Rect(canvas.DesiredSize));
        canvas.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(canvas.ActualWidth),
            (int)Math.Ceiling(canvas.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        string path = Path.ChangeExtension(outputPath, null) + ".trayicon.png";

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
