namespace ZentwikThemeDemo.Views;

/// <summary>
/// 进程页（占位）。
///
/// <remarks>
/// 骨架复用 <see cref="PlaceholderView"/>：标题 + 一张空卡片 + [功能开发中]。
/// 颜色全部来自主题资源的 DynamicResource，切深/浅色时自动跟随，
/// 不会出现孤儿控件。
///
/// 【为什么是纯 C# 类而不是带 XAML 的 UserControl】
/// WPF 不允许把「用 XAML 定义的 UserControl」当作派生类的 XAML 根
/// （编译期报 MC6017）。骨架留在基类的 XAML 里，
/// 派生类只负责设标题，视觉部分不会分叉。
/// </remarks>
public class ProcessView : PlaceholderView
{
    public ProcessView()
    {
        Title = "进程";
    }
}
