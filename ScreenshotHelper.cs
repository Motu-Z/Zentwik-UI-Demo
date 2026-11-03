using System.IO;
using System.Windows;
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
}
