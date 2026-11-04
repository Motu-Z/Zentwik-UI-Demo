using System.Text;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using ZentwikThemeDemo.ViewModels;

namespace ZentwikThemeDemo.Services;

/// <summary>
/// 集成自检：主题切换 / 强调色改色 / 品牌色重算 / 注册表读取 / 双向绑定。
/// </summary>
/// <remarks>
/// 运行方式：dotnet run -- --itest
///
/// 与 ColorMathSelfTest 分开，因为它需要真实的 Application 与资源字典。
/// 全部用强类型调用，不用反射 —— 反射版在重构时容易静默失配。
/// </remarks>
public static class IntegrationTest
{
    private static int _failures;

    public static int Run() => Run(null);

    /// <summary>
    /// 执行集成自检。<paramref name="output"/> 非空时把结果写入该缓冲区。
    /// </summary>
    public static int Run(StringBuilder? output)
    {
        _failures = 0;
        _sink = output;

        var theme = AppThemeService.Instance;
        var resources = Application.Current.Resources;

        // ==================== 1. 主题字典热切换 ====================
        Say("=== 1. 主题字典热切换 ===");
        Check("初始 IsDark = true", theme.IsDark);
        Check("初始 WindowBg = #1C1C1C", BrushColor(resources, "WindowBgBrush") == "1C1C1C",
              BrushColor(resources, "WindowBgBrush"));

        theme.SetThemeMode(AppTheme.Light);
        Check("切浅色后 WindowBg = #F3F3F3", BrushColor(resources, "WindowBgBrush") == "F3F3F3",
              BrushColor(resources, "WindowBgBrush"));
        Check("切浅色后 CardBg = #FFFFFF", BrushColor(resources, "CardBgBrush") == "FFFFFF",
              BrushColor(resources, "CardBgBrush"));
        Check("切浅色后 TextPrimary = #000000", BrushColor(resources, "TextPrimaryBrush") == "000000",
              BrushColor(resources, "TextPrimaryBrush"));
        Check("切浅色后 SidebarBg = #EAEAEA", BrushColor(resources, "SidebarBgBrush") == "EAEAEA",
              BrushColor(resources, "SidebarBgBrush"));
        Check("切浅色后 IsDark = false", !theme.IsDark);

        theme.SetThemeMode(AppTheme.Dark);
        Check("切回深色后 WindowBg = #1C1C1C", BrushColor(resources, "WindowBgBrush") == "1C1C1C",
              BrushColor(resources, "WindowBgBrush"));

        // ==================== 2. 强调色原地改色 ====================
        Say("\n=== 2. 强调色改色（替换画刷实例，规避 Freezable 冻结）===");
        theme.SetAccentColor(Color.FromRgb(0xFF, 0x00, 0x00));
        Check("强调色 = #FF0000", BrushColor(resources, "AccentBrush") == "FF0000",
              BrushColor(resources, "AccentBrush"));
        // ResourceDictionary 会冻结放入的 Freezable —— 这是既定行为，
        // 所以改色必须靠「替换画刷实例」而非「改 .Color」。
        // 下面这行验证替换后的新画刷确实生效（即上一次改色成功）。
        Check("替换画刷实例后新值生效", BrushColor(resources, "AccentBrush") == "FF0000",
              BrushColor(resources, "AccentBrush"));

        theme.SetAccentColor(Color.FromRgb(0x00, 0xFF, 0x00));
        Check("二次改色 = #00FF00", BrushColor(resources, "AccentBrush") == "00FF00",
              BrushColor(resources, "AccentBrush"));

        // ==================== 3. 品牌色随明暗重算 ====================
        Say("\n=== 3. 品牌色（twik）随明暗重算 ===");
        theme.SetAccentColor(Color.FromRgb(0x60, 0xCD, 0xFF));
        string brandDark = BrushColor(resources, "BrandAccentBrush");
        theme.SetThemeMode(AppTheme.Light);
        string brandLight = BrushColor(resources, "BrandAccentBrush");
        Say($"    深色 brand={brandDark}  浅色 brand={brandLight}");
        Check("深色 brand = #C6DAFF", brandDark == "C6DAFF", brandDark);
        Check("浅色 brand = #0058F9", brandLight == "0058F9", brandLight);
        theme.SetThemeMode(AppTheme.Dark);

        // ==================== 4. 系统色读取（注册表） ====================
        Say("\n=== 4. 系统色读取（注册表）===");
        bool sysLight = SystemTheme.IsSystemLightTheme();
        Color sysAccent = SystemTheme.GetSystemAccentColor();
        Say($"    系统浅色={sysLight}  系统强调色={sysAccent}");
        Check("系统强调色读到注册表实际值（非默认蓝）",
              sysAccent != Color.FromRgb(0x60, 0xCD, 0xFF), sysAccent.ToString());

        theme.SetAccentMode(AccentSource.System);
        Check("切到系统色彩后强调色 = 注册表值",
              BrushColor(resources, "AccentBrush") == $"{sysAccent.R:X2}{sysAccent.G:X2}{sysAccent.B:X2}",
              BrushColor(resources, "AccentBrush"));
        theme.SetAccentMode(AccentSource.Custom);
        theme.SetAccentColor(Color.FromRgb(0x60, 0xCD, 0xFF));

        // ==================== 5. ViewModel 双向绑定语义 ====================
        Say("\n=== 5. ViewModel 双向绑定语义 ===");
        var vm = new SettingsViewModel();
        vm.OnViewLoaded();

        Check("初始 HexInput = #60CDFF", vm.HexInput == "#60CDFF", vm.HexInput);
        Check("初始 HueValue ≈ 198.87", Math.Abs(vm.HueValue - 198.87) < 0.05,
              vm.HueValue.ToString("F2"));
        Check("初始强调色 = #60CDFF", BrushColor(resources, "AccentBrush") == "60CDFF",
              BrushColor(resources, "AccentBrush"));

        // 拖动滑块 → 颜色与 HEX 同步
        vm.HueValue = 0;
        Check("滑块拖到 0 → HEX = #F25A5A", vm.HexInput == "#F25A5A", vm.HexInput);
        Check("滑块拖到 0 → 强调色 = #F25A5A",
              BrushColor(resources, "AccentBrush") == "F25A5A", BrushColor(resources, "AccentBrush"));

        // 输入非法 HEX → 保留原文 → 提交后回退
        vm.HexInput = "#12";
        Check("非法输入期间保留原文（不格式化）", vm.HexInput == "#12", vm.HexInput);
        vm.CommitHexInput();
        Check("提交非法值 → 回退到上一有效值", vm.HexInput == "#F25A5A", vm.HexInput);

        // 输入合法 HEX → 反向更新滑块与强调色
        vm.HexInput = "#FF0000";
        vm.CommitHexInput();
        Check("合法输入 → 强调色 = #FF0000",
              BrushColor(resources, "AccentBrush") == "FF0000", BrushColor(resources, "AccentBrush"));
        Check("合法输入 → 滑块反向同步 H≈0", Math.Abs(vm.HueValue) < 0.5, vm.HueValue.ToString("F2"));
        Check("合法输入 → HEX 规范化为大写", vm.HexInput == "#FF0000", vm.HexInput);

        // 无 # 前缀也能解析
        vm.HexInput = "00FF00";
        vm.CommitHexInput();
        Check("无 # 前缀也能解析", BrushColor(resources, "AccentBrush") == "00FF00",
              BrushColor(resources, "AccentBrush"));

        // 三位色值
        vm.HexInput = "#ABC";
        vm.CommitHexInput();
        Check("三位色值 #ABC 展开为 #AABBCC",
              BrushColor(resources, "AccentBrush") == "AABBCC", BrushColor(resources, "AccentBrush"));

        // ==================== 6. 状态一致性（本轮 bug 回归） ====================
        //
        // 这一节针对的是本轮修复的四个状态 bug：
        //   · 切到别的页面再切回杂项，主题/色彩按钮回到默认值
        //   · 手动选深色后，系统主题改浅色，应用也跟着变
        //   · 从个性切到系统，彩虹面板没收起
        //   · 系统强调色读成别的颜色
        Say("\n=== 6. 状态一致性回归（本轮修复的 bug）===");

        // --- 6.1 单选态是服务的投影：重建 VM 后状态必须一致 ---
        theme.SetThemeMode(AppTheme.Light);
        theme.SetAccentMode(AccentSource.Custom);
        theme.SetAccentColor(Color.FromRgb(0x60, 0xCD, 0xFF));

        var vm2 = new SettingsViewModel();
        Check("重建 VM 后仍选中浅色（Mode=Light）", vm2.IsLightSelected, $"Mode={theme.Mode}");
        Check("重建 VM 后未选中深色", !vm2.IsDarkSelected);
        Check("重建 VM 后未选中系统主题", !vm2.IsSystemSelected);
        Check("重建 VM 后仍选中个性色彩", vm2.IsCustomAccentSelected, $"AccentMode={theme.AccentMode}");
        Check("重建 VM 后未选中系统色彩", !vm2.IsSystemAccentSelected);

        // --- 6.2 手动选深色后，系统主题变化不得覆盖 ---
        vm2.IsDarkSelected = true;
        Check("手动选深色 → Mode=Dark", theme.Mode == AppTheme.Dark, $"Mode={theme.Mode}");

        // 模拟系统主题从深变浅：直接调服务里跟随系统的那个入口
        theme.SetThemeMode(AppTheme.System);
        bool sysIsLight = SystemTheme.IsSystemLightTheme();
        theme.RefreshFromSystem();
        Check("系统模式下跟随系统明暗",
              theme.IsDark == !sysIsLight, $"IsDark={theme.IsDark} 系统浅色={sysIsLight}");

        // 关键：手动选深色 → 再刷一次系统状态 → 不得被系统明暗带走
        theme.SetThemeMode(AppTheme.Dark);
        theme.RefreshFromSystem();
        Check("手动选深色后刷新系统状态 → 仍是深色",
              theme.IsDark, $"IsDark={theme.IsDark} Mode={theme.Mode}");

        // --- 6.3 彩虹面板显隐跟随色彩模式 ---
        var vm3 = new SettingsViewModel();
        theme.SetAccentMode(AccentSource.Custom);
        Check("个性模式 → 面板可见", vm3.IsColorPanelVisible);
        theme.SetAccentMode(AccentSource.System);
        Check("切到系统模式 → 面板收起", !vm3.IsColorPanelVisible);
        theme.SetAccentMode(AccentSource.Custom);
        Check("切回个性模式 → 面板展开", vm3.IsColorPanelVisible);

        // --- 6.4 系统强调色读取必须与 DWM 一致（不能读成别的颜色） ---
        Color sysAccent2 = SystemTheme.GetSystemAccentColor();
        Say($"    DWM 读到的系统强调色 = {sysAccent2}");
        Check("系统强调色不是默认蓝", sysAccent2 != Color.FromRgb(0x60, 0xCD, 0xFF), sysAccent2.ToString());

        // 与 AccentPalette 索引 3（主色组，RGBA）交叉校验
        Color? paletteMain = ReadPaletteMainAccent();
        if (paletteMain is Color pm)
        {
            Check("系统强调色与 AccentPalette 主色组一致",
                  pm.R == sysAccent2.R && pm.G == sysAccent2.G && pm.B == sysAccent2.B,
                  $"palette={pm} dwm={sysAccent2}");
        }

        // --- 6.5 切到系统色彩后，强调色应等于系统色 ---
        theme.SetAccentMode(AccentSource.System);
        Check("跟随系统色彩 → 强调色 = 系统色",
              BrushColor(resources, "AccentBrush")
                  == $"{sysAccent2.R:X2}{sysAccent2.G:X2}{sysAccent2.B:X2}",
              BrushColor(resources, "AccentBrush"));

        // 恢复默认状态，避免影响后续运行
        theme.SetAccentMode(AccentSource.Custom);
        theme.SetAccentColor(Color.FromRgb(0x60, 0xCD, 0xFF));
        theme.SetThemeMode(AppTheme.Dark);

        // ============================================================
        // 6.6 浅色主题下 AccentBrush 必须仍能解析（本轮回归）
        // ============================================================
        Say("\n=== 6.6 浅色主题的强调色解析 ===");
        theme.SetThemeMode(AppTheme.Light);
        Check("浅色主题下 AccentBrush 可解析", BrushColor(resources, "AccentBrush") != "<无>",
              BrushColor(resources, "AccentBrush"));
        Check("浅色主题下 BrandAccentBrush 可解析", BrushColor(resources, "BrandAccentBrush") != "<无>",
              BrushColor(resources, "BrandAccentBrush"));

        // 列出各字典的键，看 AccentBrush 到底在哪个字典里
        for (int i = 0; i < resources.MergedDictionaries.Count; i++)
        {
            var keys = resources.MergedDictionaries[i].Keys.Cast<object>()
                .Select(k => k.ToString()).ToList();
            Say($"    [{i}] 含 AccentBrush? {keys.Contains("AccentBrush")}   键数={keys.Count}");
        }

        theme.SetThemeMode(AppTheme.Dark);

        // ============================================================
        // 7. 视图层订阅时序回归（本轮修复的核心）
        // ============================================================
        Say("\n=== 7. 彩虹面板的通知送达 ===");

        // 【为什么要有这条断言】
        // 问题 2「切换个性后彩虹面板不展开」的根因：
        // DataContext 在 XAML 里赋值（InitializeComponent 内部），
        // DataContextChanged 事件在那时就已触发过 ——
        // 构造函数末尾才订阅，事件被永远错过 → PropertyChanged 从未订阅。
        //
        // 这里复现真实场景：新建视图 → 立刻切换色彩模式 →
        // 断言视图确实收到了 IsColorPanelVisible 的变化通知。
        var view = new Views.SettingsView();
        int notifyCount = 0;
        bool? lastVisible = null;

        if (view.DataContext is ViewModels.SettingsViewModel viewVm)
        {
            viewVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewModels.SettingsViewModel.IsColorPanelVisible))
                {
                    notifyCount++;
                    lastVisible = viewVm.IsColorPanelVisible;
                }
            };
        }
        else
        {
            Check("SettingsView 的 DataContext 是 SettingsViewModel", false);
        }

        theme.SetAccentMode(AccentSource.System);
        theme.SetAccentMode(AccentSource.Custom);

        Check("切换色彩模式时视图收到通知", notifyCount >= 2,
              $"收到 {notifyCount} 次");
        Check("最后一次通知的面板状态 = 展开", lastVisible == true,
              lastVisible?.ToString() ?? "null");

        // 恢复默认状态，避免影响后续运行
        theme.SetAccentMode(AccentSource.Custom);
        theme.SetAccentColor(Color.FromRgb(0x60, 0xCD, 0xFF));

        // ============================================================
        // 8. 图标与 ToolTip 的主题跟随
        // ============================================================
        Say("\n=== 8. 图标 / ToolTip 的主题跟随 ===");

        // 【图标为什么要单独断言】
        // ResourceDictionary 里的 DrawingImage 属于 Freezable，
        // 加载时被冻结 → 内部 DynamicResource 当场求值固化 →
        // 图标永远停在初始颜色，从来没跟随过主题。
        // 现在改成由 AppThemeService 在代码里组装，换色时整体重建。
        // 这里断言：图标实例在换色后确实换了（不是同一个对象）。
        var logoBefore = theme.BrandLogoImage;
        var logoBrushBefore = theme.BrandAccentBrush;

        theme.SetAccentColor(Color.FromRgb(0xE0, 0x30, 0x30));

        Check("换色后品牌色画刷被替换", !ReferenceEquals(logoBrushBefore, theme.BrandAccentBrush),
              $"old={logoBrushBefore.Color} new={theme.BrandAccentBrush.Color}");
        Check("换色后应用图标被重建", !ReferenceEquals(logoBefore, theme.BrandLogoImage),
              "BrandLogoImage 实例已更换");

        // 图标必须真的用上了新的调色板（不能只是换了实例）。
        // 【配色规则已变更】
        // 旧方案把三层分别映射到品牌色的主色 / 中间调 / 高光，
        // 结果「阴影」变成了另一种颜色的色块，不是阴影。
        // 现方案：主体 = 强调色（带对比度保底），阴影 = 同色相压暗。
        bool logoUsesBody = false;
        bool logoUsesShadow = false;

        if (theme.BrandLogoImage.Drawing is DrawingGroup logoGroup)
        {
            foreach (var child in logoGroup.Children)
            {
                if (child is GeometryDrawing gd &&
                    gd.Brush is SolidColorBrush sb)
                {
                    if (sb.Color == theme.CurrentLogoPalette.Body) logoUsesBody = true;
                    if (sb.Color == theme.CurrentLogoPalette.Shadow) logoUsesShadow = true;
                }
            }
        }

        Check("图标主体使用当前调色板的主体色", logoUsesBody,
              $"Body={theme.CurrentLogoPalette.Body}");
        Check("图标阴影使用当前调色板的阴影色", logoUsesShadow,
              $"Shadow={theme.CurrentLogoPalette.Shadow}");

        // ToolTip 隐式样式：必须存在、必须是我们重写过的模板（无系统小箭头）
        var tipStyle = Application.Current.TryFindResource(typeof(System.Windows.Controls.ToolTip))
                       as Style;
        Check("ToolTip 存在全局隐式样式", tipStyle is not null);

        bool tipHasArrow = false;
        bool tipHasBorder = false;
        double tipCorner = 0;

        if (tipStyle is not null)
        {
            var probe = new System.Windows.Controls.ToolTip { Content = "probe" };
            probe.Measure(new Size(320, double.PositiveInfinity));
            probe.Arrange(new Rect(0, 0, 320, probe.DesiredSize.Height));
            probe.ApplyTemplate();

            // 找模板里的 Border —— 系统默认模板的箭头就是 Path
            var border = FindVisualChild<System.Windows.Controls.Border>(probe);
            if (border is not null)
            {
                tipHasBorder = true;
                tipCorner = border.CornerRadius.TopLeft;
            }

            tipHasArrow = FindVisualChild<System.Windows.Shapes.Path>(probe) is not null;
        }

        Check("ToolTip 模板含自定义 Border", tipHasBorder);
        Check("ToolTip 圆角为 6", Math.Abs(tipCorner - 6) < 0.01, $"实际 {tipCorner}");
        Check("ToolTip 模板已移除系统小箭头", !tipHasArrow);

        // 恢复默认强调色
        theme.SetAccentColor(Color.FromRgb(0x60, 0xCD, 0xFF));

        // ============================================================
        // 9. 配置方案会话 + 三页挂载（本轮新增）
        // ============================================================
        Say("\n=== 9. 配置方案会话与三页挂载 ===");

        var session = ProfileSession.Instance;

        Check("方案会话是单例", ReferenceEquals(session, ProfileSession.Instance));
        Check("初始有 3 份方案", session.Profiles.Count == 3, $"实际 {session.Profiles.Count}");
        Check("初始当前方案非空", session.Current is not null);
        Check("当前方案名可读", session.CurrentName.Length > 0, session.CurrentName);

        // 切换：大窗里选一行 → 确定 → 生效
        var target = session.Profiles[1];
        session.SelectProfileCommand.Execute(target);
        Check("预备选中已置位", target.IsSelected);
        Check("同一时刻只有一份被选中",
              session.Profiles.Count(p => p.IsSelected) == 1);

        session.ApplySelectionCommand.Execute(null);
        Check("确定后当前方案已切换", ReferenceEquals(session.Current, target), session.CurrentName);
        Check("切换后大窗自动关闭", !session.IsOverlayOpen);
        Check("切换后旧方案不再标记为生效中", !session.Profiles[0].IsActive);

        // 取消：预备选中后取消，不应改变当前方案
        var another = session.Profiles[2];
        session.SelectProfileCommand.Execute(another);
        session.CloseOverlayCommand.Execute(null);
        Check("取消后当前方案不变", ReferenceEquals(session.Current, target), session.CurrentName);
        Check("取消后大窗关闭", !session.IsOverlayOpen);
        Check("取消后预备选中态被清空", !another.IsSelected);

        // 重命名：空名必须被拒绝
        var renaming = session.Profiles[0];
        session.StartRenameCommand.Execute(renaming);
        Check("进入重命名态", renaming.IsRenaming);

        renaming.RenameText = "   ";
        session.CommitRenameCommand.Execute(renaming);
        Check("空名被拒绝，保留原名", renaming.Name != "   " && renaming.Name.Length > 0, renaming.Name);
        Check("空名后退出重命名态", !renaming.IsRenaming);

        renaming.RenameText = "新名字";
        session.CommitRenameCommand.Execute(renaming);
        Check("重命名生效", renaming.Name == "新名字", renaming.Name);

        // 删除：需先确认
        var doomed = session.Profiles[2];
        int before = session.Profiles.Count;
        session.AskDeleteCommand.Execute(doomed);
        Check("删除进入确认态", doomed.IsConfirmingDelete);
        Check("确认前不删除", session.Profiles.Count == before);

        session.ConfirmDeleteCommand.Execute(doomed);
        Check("确认后已删除", session.Profiles.Count == before - 1, $"实际 {session.Profiles.Count}");

        // 新建：进入重命名态等待输入名字
        int beforeCreate = session.Profiles.Count;
        session.CreateProfileCommand.Execute(null);
        Check("新建后方案数 +1", session.Profiles.Count == beforeCreate + 1);
        Check("新建项处于重命名态", session.Profiles[^1].IsRenaming);

        session.Profiles[^1].RenameText = "测试方案";
        session.CommitRenameCommand.Execute(session.Profiles[^1]);
        Check("新建方案可命名", session.Profiles[^1].Name == "测试方案", session.Profiles[^1].Name);

        // 三页挂载：每页都要有方案条；大窗挂在窗口级（真正的模态要盖住整窗）
        CheckProfileChrome("调校页", new Views.TuningView());
        CheckProfileChrome("进程页", new Views.ProcessView());
        CheckProfileChrome("电源页", new Views.PowerView());

        // 大窗在窗口级：只挂一份，且跨两列盖住侧栏
        var shell = new MainWindow();
        shell.Measure(new Size(1180, 800));
        shell.Arrange(new Rect(0, 0, 1180, 800));
        shell.UpdateLayout();

        // 大窗是根 Grid 的直接子元素。
        // 不走 FindVisualChild（它是深度优先，会先钻进侧栏的整棵子树），
        // 直接枚举根 Grid 的直接子元素 —— 断言的是「挂在根上」这件事本身。
        var shellOverlay = shell.Content is Grid shellGrid
            ? shellGrid.Children.OfType<Views.ProfileOverlay>().FirstOrDefault()
            : null;

        Check("窗口级挂载了模态大窗", shellOverlay is not null);
        Check("窗口级大窗指向会话单例",
              shellOverlay is not null && ReferenceEquals(shellOverlay.DataContext, ProfileSession.Instance));

        // 大窗必须跨两列（否则遮罩只盖侧栏、内容区漏出来）
        bool spansBothColumns = false;
        if (shellOverlay is not null)
        {
            int col = System.Windows.Controls.Grid.GetColumn(shellOverlay);
            int span = System.Windows.Controls.Grid.GetColumnSpan(shellOverlay);
            spansBothColumns = col == 0 && span == 2;
        }

        Check("大窗跨两列（盖住侧栏）", spansBothColumns);

        var overlayDataContext = new Views.ProfileOverlay().DataContext;
        Check("模态大窗绑定到会话单例", ReferenceEquals(overlayDataContext, session));

        var barDataContext = new Views.ProfileBar().DataContext;
        Check("方案条绑定到会话单例", ReferenceEquals(barDataContext, session));

        // 三页的 ViewModel 各自独立，但方案状态是共享的
        Check("电源页 VM 可构造", new PowerViewModel() is not null);
        Check("进程页 VM 可构造", new ProcessViewModel() is not null);
        Check("调校页 VM 可构造", new TuningViewModel() is not null);

        // 电源策略值域（严格对齐 Zentwik.Core.PowerPolicySnapshot）
        var power = new PowerViewModel();
        Check("EPP 默认 0–100 区间内", power.Epp is >= 0 and <= 100);
        Check("Boost 默认 0–4 区间内", power.BoostMode is >= 0 and <= 4);
        Check("最大处理器状态默认 50–100 区间内", power.MaxProcessorState is >= 50 and <= 100);
        Check("ASPM 默认 0–2 区间内", power.Aspm is >= 0 and <= 2);
        Check("空闲禁用默认 false（与系统同向：true = 禁用）", !power.IdleDisable);

        // 进程页
        var proc = new ProcessViewModel();
        Check("进程页有运行中进程", proc.RunningProcesses.Count > 0, $"实际 {proc.RunningProcesses.Count}");
        Check("进程页有已保存规则", proc.SavedRules.Count > 0, $"实际 {proc.SavedRules.Count}");
        Check("未搜索时过滤结果等于全部", proc.FilteredProcesses.Count == proc.RunningProcesses.Count);

        proc.SearchText = "cs2";
        Check("搜索按进程名过滤", proc.FilteredProcesses.Count == 1, $"实际 {proc.FilteredProcesses.Count}");
        Check("搜索命中的是 cs2.exe", proc.FilteredProcesses[0].Name == "cs2.exe");

        proc.SearchText = "steamapps";
        Check("搜索也匹配路径", proc.FilteredProcesses.Count == 1, $"实际 {proc.FilteredProcesses.Count}");

        proc.SearchText = "不存在的进程名";
        Check("无命中时结果为空", proc.FilteredProcesses.Count == 0);

        proc.SearchText = string.Empty;
        Check("清空搜索后恢复全部", proc.FilteredProcesses.Count == proc.RunningProcesses.Count);

        // 规则启用/禁用与删除
        var rule = proc.SavedRules[0];
        bool wasEnabled = rule.Enabled;
        proc.ToggleRuleCommand.Execute(rule);
        Check("规则启用态已翻转", rule.Enabled != wasEnabled);
        Check("按钮文案随启用态变化",
              rule.ToggleLabel == (rule.Enabled ? "禁用" : "启用"), rule.ToggleLabel);

        int rulesBefore = proc.SavedRules.Count;
        proc.DeleteRuleCommand.Execute(rule);
        Check("规则已删除", proc.SavedRules.Count == rulesBefore - 1);

        return _failures;
    }

    private static void Say(string line)
    {
        Console.WriteLine(line);
        _sink?.AppendLine(line);
    }

    /// <summary>
    /// 校验一页是否挂好了方案条，且它指向同一个会话单例。
    /// </summary>
    /// <remarks>
    /// 【为什么要查可视树而不是查字段】
    /// 方案条是 XAML 里声明的控件，只有把它挂进可视树，
    /// 布局跑过一遍之后才能在 VisualTree 里找到。
    /// 断言的是「页面上真的有这个控件」——
    /// 漏挂一个，用户在这页就找不到切换方案的入口。
    /// </remarks>
    private static void CheckProfileChrome(string page, FrameworkElement root)
    {
        // 跑一次布局，让声明式控件实例化
        root.Measure(new Size(1200, 2000));
        root.Arrange(new Rect(0, 0, 1200, 2000));
        root.UpdateLayout();

        var bar = FindVisualChild<Views.ProfileBar>(root);

        Check($"{page}挂载了方案条", bar is not null);
        Check($"{page}方案条指向会话单例",
              bar is not null && ReferenceEquals(bar.DataContext, ProfileSession.Instance));
    }

    private static string BrushColor(ResourceDictionary resources, string key)
    {
        return resources[key] is SolidColorBrush b
            ? $"{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}"
            : "<无>";
    }

    /// <summary>
    /// 读 Explorer\Accent\AccentPalette 的主色组（索引 3，RGBA 字节序）——
    /// 用于与 DWM 读到的强调色交叉校验。
    /// </summary>
    private static Color? ReadPaletteMainAccent()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= 16)
            {
                const int MainAccentIndex = 3;
                int o = MainAccentIndex * 4;
                return Color.FromRgb(palette[o], palette[o + 1], palette[o + 2]);
            }
        }
        catch
        {
            // 读不到就跳过校验
        }
        return null;
    }

    private static void Check(string name, bool ok, string detail = "")
    {
        if (!ok) _failures++;
        string line = $"  {(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -> " + detail : "")}";
        Console.WriteLine(line);
        _sink?.AppendLine(line);
    }

    /// <summary>
    /// 在视觉树里递归查找第一个指定类型的元素。
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null) return null;

        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;

            var found = FindVisualChild<T>(child);
            if (found is not null) return found;
        }

        return null;
    }

    private static StringBuilder? _sink;
}
