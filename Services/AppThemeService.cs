using System.Windows;
using System.Windows.Media;

namespace ZentwikThemeDemo.Services;

/// <summary>应用主题模式。</summary>
public enum AppTheme
{
    Light,
    Dark,
    System,
}

/// <summary>强调色来源模式。</summary>
public enum AccentSource
{
    /// <summary>跟随系统强调色。</summary>
    System,

    /// <summary>用户通过彩虹滑块自定义。</summary>
    Custom,
}

/// <summary>
/// 应用级主题引擎 —— 全局唯一状态源。
/// </summary>
/// <remarks>
/// 【为什么状态必须收敛到这里】
/// 之前主题模式 / 色彩模式 / 强调色各存一份：一份在 AppThemeService，
/// 一份在 SettingsViewModel 的 bool 字段里。
/// ViewModel 每次随页面重建都会把 bool 重置为初值，于是：
///   · 从别的页面切回「杂项」→ 按钮回到默认态，但服务里的真实状态没变
///     （页面还是浅色，按钮却显示「深色」）
///   · 用户手动选「深色」→ 服务里的 Mode 可能根本没被写进去
///     （RadioButton 的 IsChecked 回写有静默丢失的路径）
///     → 系统主题一变，应用又跟着系统走
/// 现在 Mode / AccentMode / AccentColor 只存在这里一份，
/// ViewModel 的单选状态改成对这里的**投影**（getter 读服务），
/// setter 才写回服务。按钮显示什么，永远等于服务里是什么。
///
/// 【为什么改色是「替换实例」而不是「改 .Color」】
/// 实测：ResourceDictionary 会对放入的 Freezable 自动冻结（省内存），
/// 冻结后设置 .Color 会抛「无法在冻结的画刷上设置属性」。
/// 而「用新画刷实例替换字典项」既能避开冻结，
/// 又能触发 DynamicResource 绑定立即刷新（实测：红→绿→黄全部生效）。
///
/// 【字典替换的实现】
/// Application.Resources.MergedDictionaries 按顺序查找，索引靠前的优先。
/// 我们把主题字典放在索引 0，切换时用 Replace 原地换掉这一项，
/// 其余字典（Accent / Controls）索引不变，优先级关系保持稳定。
/// </remarks>
public sealed class AppThemeService
{
    private static readonly Lazy<AppThemeService> _instance = new(() => new AppThemeService());
    public static AppThemeService Instance => _instance.Value;

    /// <summary>主题字典在 MergedDictionaries 中的固定槽位。</summary>
    private const int ThemeSlot = 0;

    /// <summary>
    /// 强调色画刷字典（独立实例，不与主题字典混在一起）。
    /// </summary>
    private readonly ResourceDictionary _accentDictionary = new();

    /// <summary>当前强调色画刷（供 ViewModel / 代码直接使用）。</summary>
    public SolidColorBrush AccentBrush { get; private set; } = new(Color.FromRgb(0x60, 0xCD, 0xFF));

    /// <summary>当前品牌色（twik）画刷。</summary>
    public SolidColorBrush BrandAccentBrush { get; private set; } = new(Color.FromRgb(0xC6, 0xDA, 0xFF));

    /// <summary>品牌色的中间调（应用图标第二层用）。</summary>
    public SolidColorBrush BrandAccentMidBrush { get; private set; } = new(Color.FromRgb(0x9A, 0xB4, 0xE0));

    /// <summary>品牌色的高光（应用图标第三层用）。</summary>
    public SolidColorBrush BrandAccentLightBrush { get; private set; } = new(Color.FromRgb(0xE2, 0xEC, 0xFF));

    /// <summary>压在强调色上的前景色画刷。</summary>
    public SolidColorBrush AccentForegroundBrush { get; private set; } = new(Color.FromRgb(0x06, 0x12, 0x1A));

    private AppThemeService() { }

    /// <summary>当前主题模式（用户选择：浅色 / 深色 / 系统）。</summary>
    public AppTheme Mode { get; private set; } = AppTheme.Dark;

    /// <summary>当前实际生效的主题（System 模式下已解析为 Light / Dark）。</summary>
    public bool IsDark { get; private set; } = true;

    /// <summary>当前强调色来源。</summary>
    public AccentSource AccentMode { get; private set; } = AccentSource.Custom;

    /// <summary>当前强调色。</summary>
    public Color AccentColor { get; private set; } = Color.FromRgb(0x60, 0xCD, 0xFF);

    /// <summary>主题发生变化（明暗切换）时触发。</summary>
    public event EventHandler<bool>? ThemeChanged;

    /// <summary>强调色发生变化时触发。</summary>
    public event EventHandler<Color>? AccentChanged;

    /// <summary>
    /// 主题**模式**变化时触发（浅色 ↔ 深色 ↔ 系统）。
    /// 与 <see cref="ThemeChanged"/> 区分：后者是「解析后的明暗」，
    /// 前者是「用户选了什么」—— 分段控件的选中态要跟的是后者。
    /// </summary>
    public event EventHandler<AppTheme>? ThemeModeChanged;

    /// <summary>强调色**来源**变化时触发（系统 ↔ 个性）。</summary>
    public event EventHandler<AccentSource>? AccentModeChanged;

    private bool _initialized;

    /// <summary>
    /// 初始化：挂载资源字典。
    /// </summary>
    /// <remarks>
    /// 必须在任何使用 {StaticResource ...} 引用本工程样式的窗口
    /// 执行 InitializeComponent 之前调用 —— 否则样式还没合并进
    /// Application.Resources，XAML 解析会抛「找不到资源」。
    ///
    /// 幂等：重复调用不重复合并字典（--itest 与正常启动都走这里）。
    /// </remarks>
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        var resources = Application.Current.Resources;

        // 主题字典放索引 0（最高优先级），Accent / Controls 依次在后。
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("Themes/Tokens.Dark.xaml", UriKind.Relative)
        });
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("Themes/Accent.xaml", UriKind.Relative)
        });
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("Themes/Controls.xaml", UriKind.Relative)
        });

        // 强调色画刷在代码里创建并单独成典。
        //
        // 【为什么必须用「替换实例」而不是「改 .Color」】
        // 实测：ResourceDictionary 会对放入的 Freezable 自动冻结，
        // 冻结后设置 .Color 抛 InvalidOperationException，
        // 而强调色必须随滑块反复改色。
        // 「用新实例替换字典项」既避开冻结，又能触发 DynamicResource 立即刷新。
        _accentDictionary["AccentBrush"] = AccentBrush;
        _accentDictionary["AccentForegroundBrush"] = AccentForegroundBrush;
        _accentDictionary["BrandAccentBrush"] = BrandAccentBrush;
        _accentDictionary["BrandAccentMidBrush"] = BrandAccentMidBrush;
        _accentDictionary["BrandAccentLightBrush"] = BrandAccentLightBrush;
        resources.MergedDictionaries.Add(_accentDictionary);

        // 图标矢量资源（品牌色的三层画刷在 _accentDictionary 里，
        // 这里只提供几何与画刷结构）
        resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("Themes/BrandLogo.xaml", UriKind.Relative)
        });

        IsDark = true;
    }

    /// <summary>
    /// 把系统主题监听挂到指定窗口上。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Initialize"/> 分开：监听需要窗口句柄，
    /// 而字典合并必须早于窗口的 InitializeComponent。
    ///
    /// 必须在窗口的 SourceInitialized 之后调用 —— 构造函数阶段
    /// WindowInteropHelper.Handle 仍是 0，拿不到 HwndSource。
    /// </remarks>
    public void AttachWindow(Window hostWindow)
    {
        // 系统主题变化监听：注册表改动后 Windows 会广播 WM_SETTINGCHANGE，
        // 由窗口钩子转发到这里。
        SystemEventsBridge.RegisterThemeChanged(hostWindow, RefreshFromSystem);
    }

    /// <summary>
    /// 重新读取系统状态并应用 —— 由 WM_SETTINGCHANGE 触发。
    /// </summary>
    /// <remarks>
    /// 【明暗与强调色必须各自独立判断】
    /// 之前写成 if / else-if，结果「主题=系统 且 色彩=系统」时
    /// 系统强调色永远不会被刷新 —— 用户改系统强调色，应用毫无反应。
    /// 现在两件事各判各的，互不排斥。
    ///
    /// 【为什么手动选过深色就不再跟随】
    /// 只有 Mode == System 才应用系统明暗。用户显式选了深色/浅色，
    /// Mode 就是 Dark/Light，此时系统怎么改都不动 —— 这正是期望行为。
    /// </remarks>
    public void RefreshFromSystem()
    {
        if (Mode == AppTheme.System)
        {
            ApplyResolvedTheme(SystemTheme.IsSystemLightTheme() ? AppTheme.Light : AppTheme.Dark);
        }

        if (AccentMode == AccentSource.System)
        {
            SetAccentColor(SystemTheme.GetSystemAccentColor());
        }
    }

    /// <summary>
    /// 切换主题模式。
    /// </summary>
    public void SetThemeMode(AppTheme mode)
    {
        if (Mode == mode)
        {
            // 模式没变也要重新解析一次：
            // 模式是 System 时，系统明暗可能已经变了。
            if (mode == AppTheme.System)
            {
                ApplyResolvedTheme(SystemTheme.IsSystemLightTheme() ? AppTheme.Light : AppTheme.Dark);
            }
            return;
        }

        Mode = mode;

        ApplyResolvedTheme(mode == AppTheme.System
            ? (SystemTheme.IsSystemLightTheme() ? AppTheme.Light : AppTheme.Dark)
            : mode);

        ThemeModeChanged?.Invoke(this, mode);
    }

    /// <summary>
    /// 应用已解析出的具体主题（Light / Dark）。
    /// </summary>
    private void ApplyResolvedTheme(AppTheme resolved)
    {
        bool newIsDark = resolved == AppTheme.Dark;
        if (newIsDark == IsDark) return;

        IsDark = newIsDark;

        var resources = Application.Current.Resources;
        resources.MergedDictionaries[ThemeSlot] = new ResourceDictionary
        {
            Source = new Uri(newIsDark ? "Themes/Tokens.Dark.xaml" : "Themes/Tokens.Light.xaml",
                             UriKind.Relative)
        };

        // 明暗切换会改变品牌色的 L 档（深色加亮 / 浅色压暗）→ 重算一次
        RefreshBrandAccent();

        ThemeChanged?.Invoke(this, IsDark);
    }

    /// <summary>
    /// 切换强调色来源。
    /// </summary>
    public void SetAccentMode(AccentSource source)
    {
        if (AccentMode == source)
        {
            // 模式没变也同步一次颜色：切到「系统」后系统色可能已变。
            if (source == AccentSource.System)
            {
                SetAccentColor(SystemTheme.GetSystemAccentColor());
            }
            return;
        }

        AccentMode = source;

        if (source == AccentSource.System)
        {
            SetAccentColor(SystemTheme.GetSystemAccentColor());
        }

        AccentModeChanged?.Invoke(this, source);
    }

    /// <summary>
    /// 设置强调色并原地改写 AccentBrush / BrandAccentBrush。
    /// </summary>
    public void SetAccentColor(Color color)
    {
        if (AccentColor == color && _accentDictionary.Contains("AccentBrush"))
        {
            // 同色不重刷：避免每次系统事件都重建画刷导致无谓的重绘。
            return;
        }

        AccentColor = color;

        // 替换画刷实例（而非改 .Color）—— 原因见 _accentDictionary 的说明
        AccentBrush = new SolidColorBrush(color);
        _accentDictionary["AccentBrush"] = AccentBrush;

        // 浅色强调色上用深色字，深色强调色上用浅色字（按亮度判断）
        AccentForegroundBrush = new SolidColorBrush(
            IsLight(color) ? Color.FromRgb(0x06, 0x12, 0x1A) : Colors.White);
        _accentDictionary["AccentForegroundBrush"] = AccentForegroundBrush;

        RefreshBrandAccent();
        AccentChanged?.Invoke(this, color);
    }

    /// <summary>
    /// 重算品牌色（twik）并写入资源字典。
    /// </summary>
    public void RefreshBrandAccent()
    {
        var brand = ColorMath.DeriveBrandColor(AccentColor, IsDark);
        var (main, mid, light) = ColorMath.DeriveBrandPalette(brand);

        BrandAccentBrush = new SolidColorBrush(main);
        _accentDictionary["BrandAccentBrush"] = BrandAccentBrush;

        BrandAccentMidBrush = new SolidColorBrush(mid);
        _accentDictionary["BrandAccentMidBrush"] = BrandAccentMidBrush;

        BrandAccentLightBrush = new SolidColorBrush(light);
        _accentDictionary["BrandAccentLightBrush"] = BrandAccentLightBrush;
    }

    /// <summary>
    /// 按相对亮度判断颜色是「浅」还是「深」。
    /// </summary>
    private static bool IsLight(Color c)
    {
        double luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        return luminance > 0.6;
    }
}
