using System.Windows.Controls;
using System.Windows.Input;
using ZentwikThemeDemo.ViewModels;

namespace ZentwikThemeDemo.Views;

/// <summary>
/// 配置方案模态大窗。
/// </summary>
/// <remarks>
/// 【为什么 DataContext 在这里注入】
/// 方案状态必须是<b>同一个实例</b>：页面顶部的方案条显示「当前方案」，
/// 大窗里切换后，页面上的方案条要立刻跟着变。
/// 若在 XAML 里写 <c>&lt;vm:ProfileSession /&gt;</c>，会 new 出第二个实例，
/// 两边就各说各话。这里显式绑到 <see cref="ProfileSession.Instance"/>。
/// </remarks>
public partial class ProfileOverlay : UserControl
{
    public ProfileOverlay()
    {
        InitializeComponent();

        DataContext = ProfileSession.Instance;
    }

    /// <summary>
    /// 重命名输入框的回车 / ESC 处理。
    /// </summary>
    /// <remarks>
    /// 回车提交、ESC 取消 —— 与系统对话框的习惯一致，
    /// 否则用户输入完还得去点那个很小的对勾按钮。
    /// </remarks>
    private void OnRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ProfileSession session)
        {
            return;
        }

        // 输入框的 DataContext 就是它所在的 ProfileItem
        if ((sender as TextBox)?.DataContext is not ProfileItem item)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                session.CommitRenameCommand.Execute(item);
                e.Handled = true;
                break;

            case Key.Escape:
                session.CancelRenameCommand.Execute(item);
                e.Handled = true;
                break;
        }
    }
}
