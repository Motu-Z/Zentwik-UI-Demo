using System.Windows.Controls;

namespace ZentwikThemeDemo.Views;

/// <summary>
/// 电源页（第三页）。
/// </summary>
/// <remarks>
/// 带 XAML 的 UserControl —— WPF 不允许把「用 XAML 定义的 UserControl」
/// 当作派生类的 XAML 根（编译期报 MC6017）。
/// </remarks>
public partial class PowerView : UserControl
{
    public PowerView()
    {
        InitializeComponent();
    }
}
