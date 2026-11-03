using System.Reflection;
using System.Windows.Media;
using ZentwikThemeDemo.Services;

namespace ZentwikThemeDemo.ViewModels;

/// <summary>
/// 通用设置页 ViewModel。
///
/// 【职责边界】
/// 视图内的状态切换与颜色计算全部在此完成，View 只做绑定与展示。
/// 具体来说：
/// · 主题模式 / 色彩模式的切换 → 改这里的状态，再驱动 AppThemeService
/// · HEX ↔ 色相 的双向映射 → 这里算
/// · 品牌色（twik）衍生 → 调 ColorMath.DeriveBrandColor
///
/// 【单选状态为什么是「投影」而不是本地字段】
/// 之前 IsLightSelected / IsDarkSelected / IsSystemSelected 是本地 bool，
/// 与 AppThemeService 里的 Mode 各存一份。两份状态必然漂移：
///   · 页面重建时本地 bool 被重置为初值 → 按钮显示「深色」而页面仍是浅色
///   · 用户点「深色」时，若该 bool 本来就是 true，SetProperty 判定「没变化」
///     直接 return，服务那边根本没收到通知 → 服务仍停留在「系统」模式
///     → 之后系统主题一变，应用又跟着系统走（用户手动选的深色被覆盖）
/// 现在 getter 直接读服务，setter 才写服务：
///   按钮显示什么 == 服务里是什么，物理上不可能不一致。
///
/// 【HEX 输入的「保真 + 防抖」策略】
/// 输入框内容原样保留（允许残缺的 "#" 或 "#12"，不强行格式化、不转大写）。
/// 只有在 LostFocus 或按下 Enter 时才尝试解析：
///   解析成功 → 更新滑块 H 值与界面强调色
///   解析失败 → 回退到上一个有效值
/// 这样用户中途删字、粘贴半截色值时不会被打断，也不会把无效颜色写进主题。
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly AppThemeService _theme = AppThemeService.Instance;

    // ---- 彩虹滑块 ----
    private double _hueValue = 198;
    private string _hexInput = "#60CDFF";
    private string _lastValidHex = "#60CDFF";

    public SettingsViewModel()
    {
        // 与服务当前状态对齐：滑块与 HEX 反映「此刻生效的强调色」，
        // 而不是写死的 #60CDFF —— 否则切到别的页面再回来会看到旧值。
        SyncFromAccent(_theme.AccentColor);

        // 服务状态变化 → 刷新投影出来的单选态与派生属性。
        //
        // 【为什么 ThemeModeChanged 与 ThemeChanged 要分开】
        // ThemeChanged 是「解析后的明暗」（系统模式下会自己变），
        // ThemeModeChanged 是「用户选了什么」（只有用户操作才变）。
        // 分段控件的选中态必须跟后者，否则系统主题一变，
        // 用户明明选了「深色」，按钮却跳到别的地方去。
        _theme.ThemeModeChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsLightSelected));
            OnPropertyChanged(nameof(IsDarkSelected));
            OnPropertyChanged(nameof(IsSystemSelected));
        };

        _theme.AccentModeChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsSystemAccentSelected));
            OnPropertyChanged(nameof(IsCustomAccentSelected));
            OnPropertyChanged(nameof(IsColorPanelVisible));

            // 模式一变，滑块 / HEX 应对齐当前生效的强调色，
            // 否则切回「个性」时显示的还是上一个模式的残留值。
            SyncFromAccent(_theme.AccentColor);
        };

        _theme.ThemeChanged += (_, _) => OnPropertyChanged(nameof(BrandAccentColor));

        _theme.AccentChanged += (_, color) =>
        {
            OnPropertyChanged(nameof(AccentColor));
            OnPropertyChanged(nameof(BrandAccentColor));

            // 跟随系统色时，系统强调色变了，滑块与 HEX 也要跟着走 ——
            // 否则界面显示的还是上一次读到的系统色，看起来就像「没反应」。
            if (_theme.AccentMode == AccentSource.System)
            {
                SyncFromAccent(color);
            }
        };
    }

    // ==================== 主题模式（投影自服务） ====================

    /// <summary>浅色单选。</summary>
    public bool IsLightSelected
    {
        get => _theme.Mode == AppTheme.Light;
        set
        {
            // 只处理「变为选中」。「变为未选中」是 RadioButton 组互斥的副作用，
            // 不是用户意图 —— 忽略它，否则三个单选会互相触发。
            if (value) _theme.SetThemeMode(AppTheme.Light);
        }
    }

    /// <summary>深色单选。</summary>
    public bool IsDarkSelected
    {
        get => _theme.Mode == AppTheme.Dark;
        set
        {
            if (value) _theme.SetThemeMode(AppTheme.Dark);
        }
    }

    /// <summary>跟随系统单选。</summary>
    public bool IsSystemSelected
    {
        get => _theme.Mode == AppTheme.System;
        set
        {
            if (value) _theme.SetThemeMode(AppTheme.System);
        }
    }

    // ==================== 色彩模式（投影自服务） ====================

    /// <summary>色彩跟随系统。</summary>
    public bool IsSystemAccentSelected
    {
        get => _theme.AccentMode == AccentSource.System;
        set
        {
            if (value) _theme.SetAccentMode(AccentSource.System);
        }
    }

    /// <summary>个性色彩。</summary>
    public bool IsCustomAccentSelected
    {
        get => _theme.AccentMode == AccentSource.Custom;
        set
        {
            if (value) _theme.SetAccentMode(AccentSource.Custom);
        }
    }

    /// <summary>
    /// 彩虹面板是否展开（仅个性模式）。
    /// </summary>
    /// <remarks>
    /// 依赖 <see cref="IsCustomAccentSelected"/> 的通知 ——
    /// 在 AccentModeChanged 里显式 RaisePropertyChanged，否则
    /// 从「个性」切到「系统」后面板不会收起。
    /// </remarks>
    public bool IsColorPanelVisible => _theme.AccentMode == AccentSource.Custom;

    // ==================== 彩虹滑块 ====================

    /// <summary>
    /// 色相（0~360）。拖动时按固定 S=85% / L=65% 生成颜色。
    /// </summary>
    /// <remarks>
    /// 方向与参考 HTML 的 applyAccentColor 一致：
    /// 滑块拖动 → 生成颜色 → 写回 HEX 输入框 → 写回强调色。
    ///
    /// 【为什么要 _initialized 标志】
    /// WPF 的 TwoWay 绑定在视图加载时会把源值回写到目标（Slider.Value），
    /// Slider 随即触发 ValueChanged → 回写 HueValue → 走进本 setter。
    /// 若不抑制，初始强调色 会被 hsl(198.87, 85, 65) 覆盖，
    /// 造成「预览色块」与「HEX 输入框」显示两种不同的颜色。
    ///
    /// 标志必须在**构造函数结束之后**才置位 —— 视图加载发生在构造之后，
    /// 用局部的 try/finally 抑制是拦不住的。
    /// </remarks>
    public double HueValue
    {
        get => _hueValue;
        set
        {
            if (!SetProperty(ref _hueValue, value)) return;

            if (!_initialized) return;

            // 固定饱和度与亮度，色相自由 —— 与参考 HTML 的 hslToHex(h, 85, 65) 一致
            var (r, g, b) = ColorMath.HslToRgb(value, 85, 65);
            var color = Color.FromRgb(r, g, b);

            _theme.SetAccentColor(color);

            // 滑块拖动 → 实时改写 HEX 输入框（这里必须用规范格式，
            // 因为用户此刻不是在编辑文本，而是在看滑块的即时结果）
            string hex = ColorMath.ToHex(color);
            _lastValidHex = hex;
            _hexInput = hex;
            OnPropertyChanged(nameof(HexInput));
        }
    }

    /// <summary>
    /// 是否已完成初始化。初始化完成前，HueValue 的赋值不产生副作用
    /// （防止 TwoWay 绑定回写覆盖初始强调色）。
    /// </summary>
    private bool _initialized;

    /// <summary>
    /// 视图加载完成后调用。此时 TwoWay 绑定已完成首次回写，
    /// 之后的 HueValue 变更才是真正的用户操作。
    /// </summary>
    public void OnViewLoaded()
    {
        _initialized = true;
    }

    /// <summary>
    /// HEX 输入框内容。原样保留用户输入，不格式化。
    /// </summary>
    public string HexInput
    {
        get => _hexInput;
        set
        {
            if (!SetProperty(ref _hexInput, value)) return;
            // 注意：此处不做解析。解析在 CommitHexInput 中进行（LostFocus / Enter）。
        }
    }

    /// <summary>
    /// 提交 HEX 输入（LostFocus 或 Enter 时调用）。
    /// 成功则更新滑块与强调色，失败则回退到上一个有效值。
    /// </summary>
    public void CommitHexInput()
    {
        if (ColorMath.TryParseHex(_hexInput, out var color))
        {
            // 解析成功 → 更新强调色，滑块反向同步 H 值。
            // 顺序很重要：先定颜色，再回写滑块 —— 反过来会覆盖刚设好的颜色。
            _theme.SetAccentColor(color);
            SyncFromAccent(color);
        }
        else
        {
            // 非法输入 → 回退到上一个有效值
            _hexInput = _lastValidHex;
            OnPropertyChanged(nameof(HexInput));
        }
    }

    /// <summary>
    /// 从当前强调色反推滑块与 HEX。
    /// </summary>
    /// <remarks>
    /// 直接写后备字段、不经过 setter —— setter 带副作用
    /// （会用 hsl(h,85,65) 覆盖颜色），而这里的语义恰恰是「颜色已定，滑块回显」。
    /// </remarks>
    private void SyncFromAccent(Color color)
    {
        var (h, _, _) = ColorMath.RgbToHsl(color.R, color.G, color.B);

        _hueValue = h;
        OnPropertyChanged(nameof(HueValue));

        _lastValidHex = ColorMath.ToHex(color);
        _hexInput = _lastValidHex;
        OnPropertyChanged(nameof(HexInput));
    }

    // ==================== 派生属性 ====================

    /// <summary>当前强调色（供预览色块绑定）。</summary>
    public Color AccentColor => _theme.AccentColor;

    /// <summary>
    /// 品牌色 twik：H+20°、S+15%、L 按主题加亮 / 压暗。
    /// </summary>
    public Color BrandAccentColor => ColorMath.DeriveBrandColor(_theme.AccentColor, _theme.IsDark);

    /// <summary>
    /// 应用版本号（显示在关于卡片上）。
    /// </summary>
    /// <remarks>
    /// 从程序集元数据读取，不硬编码 —— 改 csproj 的 Version 后
    /// 关于卡片自动跟随，不需要动 UI 代码。
    /// InformationalVersion 可能带 "+commit" 后缀，只取主版本号。
    /// </remarks>
    public string AppVersion
    {
        get
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly()
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (string.IsNullOrWhiteSpace(version)) return "v0.0.0";

            int plus = version.IndexOf('+');
            return "v" + (plus > 0 ? version[..plus] : version);
        }
    }

    /// <summary>
    /// 项目仓库地址（关于卡片上的版本号胶囊点击后打开）。
    /// </summary>
    public string RepositoryUrl => "https://github.com/Motu-Z/Zentwik";
}
