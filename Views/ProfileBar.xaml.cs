using System.Windows.Controls;

namespace ZentwikThemeDemo.Views;

/// <summary>
/// 页面顶部的方案条（调校 / 进程 / 电源三页共用）。
/// </summary>
/// <remarks>
/// 【DataContext 为什么在代码里注入】
/// 方案状态必须是<b>同一个实例</b>（<see cref="ViewModels.ProfileSession.Instance"/>）：
/// 方案条显示「当前方案」，大窗里切换后，方案条要立刻跟着变。
/// 若在 XAML 里写 <c>&lt;vm:ProfileSession /&gt;</c>，会 new 出第二个实例，
/// 三页各有一份、彼此不通 —— 那就退回到「每个页面自己管一份方案」的老问题。
/// </remarks>
public partial class ProfileBar : UserControl
{
    public ProfileBar()
    {
        InitializeComponent();

        DataContext = ViewModels.ProfileSession.Instance;
    }
}
