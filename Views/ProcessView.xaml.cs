using System.Windows.Controls;

namespace ZentwikThemeDemo.Views;

/// <summary>
/// 进程页（第二页）。
/// </summary>
/// <remarks>
/// 已经是带 XAML 的 UserControl，不再是 <see cref="PlaceholderView"/> 的派生类 ——
/// WPF 不允许把「用 XAML 定义的 UserControl」当作派生类的 XAML 根（编译期报 MC6017）。
/// </remarks>
public partial class ProcessView : UserControl
{
    public ProcessView()
    {
        InitializeComponent();
    }
}
