using System.Globalization;

namespace ZentwikThemeDemo.ViewModels;

/// <summary>
/// 电源页视图模型（第三页）。
///
/// <remarks>
/// 【字段严格对齐 Zentwik.Core 的 PowerPolicySnapshot】
/// 快照里每项都是「值 + Supported + Error」三件套：
///   · <c>Epp</c>              int?  —— Processor energy performance preference policy，0–100
///   · <c>BoostMode</c>        int?  —— Processor performance boost mode，0–4
///   · <c>MaxProcessorState</c> int? —— Maximum processor state，50–100(%)
///   · <c>IdleDisable</c>      bool? —— Processor idle disable，**true = 禁用空闲**
///   · <c>Aspm</c>             int?  —— Link State Power Management，0–2
///   · <c>ActiveSchemeGuid</c> Guid  —— 当前活动电源方案
///   · <c>ActiveSchemeName</c> string —— 方案显示名（读不到时为「未知」）
///
/// 【为什么值在 Core 是可空类型】
/// 可空 = 「读不到」；非可空 = 「读到 0」。
/// 两者在界面上必须长得不一样：读不到就显示「本机不支持」，
/// 而不是一个看起来正常、拖动却没反应的滑块。
///
/// 【为什么 IdleDisable 不翻成正向语义】
/// 系统项名就是 Processor idle disable，写 1 = 禁用空闲。
/// 界面标签直接写「空闲禁用」，取值同向 —— 翻正后每次读代码都要取反，
/// 取反错一次就是「想降频却把它锁在了最高频」。
/// </remarks>
public class PowerViewModel : ViewModelBase
{
    // ==================== 电源方案 ====================

    private Guid _activeSchemeGuid;
    private string _activeSchemeName = "平衡";

    /// <summary>当前活动的 Windows 电源方案 Id。</summary>
    public Guid ActiveSchemeGuid
    {
        get => _activeSchemeGuid;
        set => SetProperty(ref _activeSchemeGuid, value);
    }

    /// <summary>当前活动的 Windows 电源方案显示名。</summary>
    public string ActiveSchemeName
    {
        get => _activeSchemeName;
        set => SetProperty(ref _activeSchemeName, value);
    }

    // ==================== 能效偏好（EPP）====================

    private int _epp = 50;
    private bool _eppSupported = true;
    private string _eppError = string.Empty;

    /// <summary>能效偏好，0–100。0 = 性能优先，100 = 能效优先。</summary>
    public int Epp
    {
        get => _epp;
        set => SetProperty(ref _epp, value);
    }

    /// <summary>EPP 这一项本机是否可读可写。</summary>
    public bool EppSupported
    {
        get => _eppSupported;
        set => SetProperty(ref _eppSupported, value);
    }

    /// <summary>EPP 不可用的原因（含 Win32 错误码）。</summary>
    public string EppError
    {
        get => _eppError;
        set => SetProperty(ref _eppError, value);
    }

    // ==================== 升频模式（Boost）====================

    private int _boostMode = 1;
    private bool _boostModeSupported = true;
    private string _boostModeError = string.Empty;

    /// <summary>升频模式，0–4（关闭 / 开启 / 激进 / 高效开启 / 高效激进）。</summary>
    public int BoostMode
    {
        get => _boostMode;
        set => SetProperty(ref _boostMode, value);
    }

    /// <summary>Boost 这一项本机是否可读可写。</summary>
    public bool BoostModeSupported
    {
        get => _boostModeSupported;
        set => SetProperty(ref _boostModeSupported, value);
    }

    /// <summary>Boost 不可用的原因。</summary>
    public string BoostModeError
    {
        get => _boostModeError;
        set => SetProperty(ref _boostModeError, value);
    }

    // ==================== 最大处理器状态 ====================

    private int _maxProcessorState = 100;
    private bool _maxProcessorStateSupported = true;
    private string _maxProcessorStateError = string.Empty;

    /// <summary>最大处理器状态，50–100（百分比）。</summary>
    public int MaxProcessorState
    {
        get => _maxProcessorState;
        set => SetProperty(ref _maxProcessorState, value);
    }

    /// <summary>最大处理器状态这一项本机是否可读可写。</summary>
    public bool MaxProcessorStateSupported
    {
        get => _maxProcessorStateSupported;
        set => SetProperty(ref _maxProcessorStateSupported, value);
    }

    /// <summary>最大处理器状态不可用的原因。</summary>
    public string MaxProcessorStateError
    {
        get => _maxProcessorStateError;
        set => SetProperty(ref _maxProcessorStateError, value);
    }

    // ==================== 空闲禁用（C-State）====================

    private bool _idleDisable;
    private bool _idleDisableSupported = true;
    private string _idleDisableError = string.Empty;

    /// <summary>
    /// 空闲禁用（C-State 总开关）。<b>true 表示禁用空闲</b>，
    /// 与系统项 Processor idle disable 的取值同向。
    /// </summary>
    public bool IdleDisable
    {
        get => _idleDisable;
        set => SetProperty(ref _idleDisable, value);
    }

    /// <summary>C-State 这一项本机是否可读可写。</summary>
    public bool IdleDisableSupported
    {
        get => _idleDisableSupported;
        set => SetProperty(ref _idleDisableSupported, value);
    }

    /// <summary>C-State 不可用的原因。</summary>
    public string IdleDisableError
    {
        get => _idleDisableError;
        set => SetProperty(ref _idleDisableError, value);
    }

    // ==================== ASPM ====================

    private int _aspm = 1;
    private bool _aspmSupported = true;
    private string _aspmError = string.Empty;

    /// <summary>ASPM（PCIe 链路电源管理），0–2。</summary>
    public int Aspm
    {
        get => _aspm;
        set => SetProperty(ref _aspm, value);
    }

    /// <summary>ASPM 这一项本机是否可读可写。</summary>
    public bool AspmSupported
    {
        get => _aspmSupported;
        set => SetProperty(ref _aspmSupported, value);
    }

    /// <summary>ASPM 不可用的原因。</summary>
    public string AspmError
    {
        get => _aspmError;
        set => SetProperty(ref _aspmError, value);
    }

    // ==================== 供电状态 ====================

    private bool _isOnBattery;

    /// <summary>当前是否电池供电。台式机恒为 false。</summary>
    public bool IsOnBattery
    {
        get => _isOnBattery;
        set
        {
            if (SetProperty(ref _isOnBattery, value))
            {
                OnPropertyChanged(nameof(SupplyLabel));
            }
        }
    }

    /// <summary>供电状态文案。</summary>
    public string SupplyLabel => IsOnBattery ? "电池供电" : "交流电";

    // ==================== 状态 ====================

    private string _statusMessage = string.Empty;

    /// <summary>页脚状态提示。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isBusy;

    /// <summary>是否正在与 Worker 通信。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    // ==================== 命令 ====================

    /// <summary>读取当前电源策略（对应 <c>query-power-policy</c>）。</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>下发电源策略（对应 <c>apply-power-policy</c>）。</summary>
    public RelayCommand ApplyCommand { get; }

    /// <summary>恢复默认（回到上次读到的值）。</summary>
    public RelayCommand RestoreDefaultsCommand { get; }

    public PowerViewModel()
    {
        RefreshCommand = new RelayCommand(_ =>
        {
            StatusMessage = "已读取当前电源策略";
        });

        ApplyCommand = new RelayCommand(_ =>
        {
            StatusMessage = "电源策略已下发";
        });

        RestoreDefaultsCommand = new RelayCommand(_ =>
        {
            Epp = 50;
            BoostMode = 1;
            MaxProcessorState = 100;
            IdleDisable = false;
            Aspm = 1;
            StatusMessage = "已恢复默认电源策略";
        });
    }
}
