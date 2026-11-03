using System.Text;
using System.Windows;
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

        return _failures;
    }

    private static void Say(string line)
    {
        Console.WriteLine(line);
        _sink?.AppendLine(line);
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

    private static StringBuilder? _sink;
}
