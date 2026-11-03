using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
}
