using System.Windows.Controls;

namespace ZentwikThemeDemo.Views;

/// <summary>
/// 调校页。
///
/// <remarks>
/// 【为什么不再是占位页的派生类】
/// 占位页的骨架是「标题 + 一张空卡片」，调校页要的是完整的三卡版式，
/// 两者结构不同。而 WPF 不允许「用 XAML 定义的 UserControl」再被派生类当作
/// XAML 根（编译期报 MC6017）—— 所以调校页改为直接继承 UserControl，
/// 骨架整个写在自己的 XAML 里。其余四个占位页继续走 PlaceholderView。
///
/// 【页面只做呈现，不做判断】
/// 电源策略的值域、语义、可用性判定全在
/// <see cref="ZentwikThemeDemo.ViewModels.TuningViewModel"/> 里，
/// XAML 只是把 int 画成分段控件、把 0–100 画成滑块。
/// 这样「界面能取到的值」与「契约允许的值」天然一致 ——
/// 不需要在 XAML 里再写一遍范围校验。
/// </remarks>
public partial class TuningView : UserControl
{
    public TuningView()
    {
        InitializeComponent();
    }
}
