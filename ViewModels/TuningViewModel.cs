using System.Collections.ObjectModel;
using System.Globalization;

namespace ZentwikThemeDemo.ViewModels;

/// <summary>
/// 调校页视图模型。
///
/// <remarks>
/// 【电源策略是本页最容易写错的地方】
/// 这一块的字段名、值域、语义全部对齐 Zentwik.Core 的契约
/// （<c>PowerPolicySnapshot</c> / <c>PowerPolicyService</c>），
/// 逐项对应关系如下：
///
/// | 本页属性 | 系统设置项 | 值域 | 含义 |
/// |---|---|---|---|
/// | <see cref="Epp"/> | Processor energy performance preference policy | 0–100 | 0 = 性能优先，100 = 能效优先 |
/// | <see cref="BoostMode"/> | Processor performance boost mode | 0–4 | 0 关闭 / 1 开启 / 2 激进 / 3 高效开启 / 4 高效激进 |
/// | <see cref="MaxProcessorState"/> | Maximum processor state | 50–100 | 最大处理器状态百分比 |
/// | <see cref="Aspm"/> | Link State Power Management | 0–2 | 0 关闭 / 1 中等节能 / 2 最大节能 |
/// | <see cref="IdleDisable"/> | Processor idle disable | 布尔 | **true = 禁用空闲** |
///
/// 【为什么每一项都带 Supported / Error】
/// 这五项不是每台机器都有（台式机常常没有 ASPM，部分主板 BIOS 不暴露 Boost 档位）。
/// 只回一个值的话，「读到 0」与「读不到」在界面上长得一模一样 ——
/// 用户会看到一个 EPP = 0 的滑块，拖一下才发现根本写不进去。
/// 所以每项自带 Supported：界面据此决定画控件还是画「本机不支持」，
/// Error 则是这一项读不到的原因（含 Win32 错误码）。
///
/// 【为什么 IdleDisable 不翻成「启用 C-State」】
/// 系统里这一项的名字是 Processor idle disable，值 1 表示**禁用**空闲。
/// 接口照抄系统语义（idleDisable = true 就是禁用），而不是翻成正向的「已启用」——
/// 一旦翻转，调用方每次读这段代码都要在脑子里做一次取反，
/// 而取反错一次就是「想让 CPU 降频却把它锁在了最高频」。
/// 界面标签因此直接写「空闲禁用」，让语义与取值同向。
/// </remarks>
public class TuningViewModel : ViewModelBase
{
    // ==================== 硬件概览 ====================

    private string _cpuName = "AMD Ryzen 9 9955HX";
    public string CpuName
    {
        get => _cpuName;
        set => SetProperty(ref _cpuName, value);
    }

    private int _cpuCoreCount = 16;
    public int CpuCoreCount
    {
        get => _cpuCoreCount;
        set => SetProperty(ref _cpuCoreCount, value);
    }

    private string _smuStatusText = "SMU 已联通";
    public string SmuStatusText
    {
        get => _smuStatusText;
        set => SetProperty(ref _smuStatusText, value);
    }

    private bool _smuAvailable = true;
    public bool SmuAvailable
    {
        get => _smuAvailable;
        set => SetProperty(ref _smuAvailable, value);
    }

    // ==================== 方案 ====================

    /// <summary>
    /// 可选方案名。选中项即「当前正在编辑哪一份方案」。
    /// </summary>
    /// <remarks>
    /// 【为什么是 ProfileOptionItem 而不是 string】
    /// 分段控件的每一项都要能表达「自己是不是当前选中项」，
    /// 绑一个字符串做不到 —— 选中态得由列表项自己持有。
    /// 用一个只带两件事的小类型（名字 + 选中态）就够了。
    /// </remarks>
    public ObservableCollection<ProfileOptionItem> ProfileOptions { get; } = new();

    private int _profileIndex;

    /// <summary>
    /// 当前选中的方案下标。
    /// </summary>
    /// <remarks>
    /// 【切换方案不算「有未保存的改动」】
    /// 方案选择器表达的是「我正在编辑哪一份」，不是「我改了什么」。
    /// 真正把页面标脏的是数值控件（CO / FMax / 电源策略）的输入路径。
    /// </remarks>
    public int ProfileIndex
    {
        get => _profileIndex;
        set
        {
            if (!SetProperty(ref _profileIndex, value))
            {
                return;
            }

            SyncProfileSelection();
            OnPropertyChanged(nameof(ProfileName));
        }
    }

    /// <summary>当前方案名（供只读展示）。</summary>
    public string ProfileName =>
        _profileIndex >= 0 && _profileIndex < ProfileOptions.Count
            ? ProfileOptions[_profileIndex].Name
            : "自定义方案";

    /// <summary>
    /// 把选中态同步到各个列表项。
    /// </summary>
    /// <remarks>
    /// 只在「被选中的那一个」置 true 时才会回调 VM（见
    /// <see cref="ProfileOptionItem.IsSelected"/>），所以这里不会形成回环。
    /// </remarks>
    private void SyncProfileSelection()
    {
        for (int i = 0; i < ProfileOptions.Count; i++)
        {
            ProfileOptions[i].IsSelected = i == _profileIndex;
        }
    }

    // ==================== 逐核 CO ====================

    /// <summary>
    /// 逐核电压偏移。核数由集合长度决定，界面不写死 16。
    /// </summary>
    public ObservableCollection<CoreMarginItem> CoreMargins { get; } = new();

    /// <summary>CO 偏移下限。</summary>
    public int CoreMarginMinimum => -50;

    /// <summary>CO 偏移上限。</summary>
    public int CoreMarginMaximum => 50;

    /// <summary>CO 偏移步进。</summary>
    public int CoreMarginStep => 1;

    // ==================== FMax（最高加速频率）====================

    private int _fMaxMHz = 4750;

    /// <summary>当前 FMax（MHz）。</summary>
    public int FMaxMHz
    {
        get => _fMaxMHz;
        set
        {
            if (SetProperty(ref _fMaxMHz, value))
            {
                OnPropertyChanged(nameof(FMaxText));
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// FMax 的纯文本入口。
    /// </summary>
    /// <remarks>
    /// 容错解析：空串与非法输入（含输入中间态的 "-"）直接忽略，保留上一个有效值。
    /// 越界则夹取到 <see cref="FMaxMinimum"/> / <see cref="FMaxMaximum"/>。
    /// </remarks>
    public string FMaxText
    {
        get => _fMaxMHz.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                FMaxMHz = Math.Clamp(parsed, FMaxMinimum, FMaxMaximum);
            }
        }
    }

    /// <summary>FMax 下限（MHz）。</summary>
    public int FMaxMinimum => 4450;

    /// <summary>FMax 上限（MHz）。</summary>
    public int FMaxMaximum => 5650;

    /// <summary>FMax 步进（MHz）。</summary>
    public int FMaxStepMHz => 10;

    // ==================== Windows 电源策略 ====================
    // 字段与 Zentwik.Core.PowerPolicySnapshot 一一对应，语义见类注释。

    private int _epp = 50;

    /// <summary>能效偏好（EPP），0–100。0 = 性能优先，100 = 能效优先。</summary>
    public int Epp
    {
        get => _epp;
        set
        {
            if (SetProperty(ref _epp, value))
            {
                MarkDirty();
            }
        }
    }

    private bool _eppSupported = true;

    /// <summary>EPP 这一项本机是否可读。</summary>
    public bool EppSupported
    {
        get => _eppSupported;
        set => SetProperty(ref _eppSupported, value);
    }

    private string _eppError = string.Empty;

    /// <summary>EPP 不可读的原因。</summary>
    public string EppError
    {
        get => _eppError;
        set => SetProperty(ref _eppError, value);
    }

    private int _boostMode = 1;

    /// <summary>升频模式（Boost），0–4。</summary>
    public int BoostMode
    {
        get => _boostMode;
        set
        {
            if (SetProperty(ref _boostMode, value))
            {
                MarkDirty();
            }
        }
    }

    private bool _boostModeSupported = true;

    /// <summary>Boost 这一项本机是否可读。</summary>
    public bool BoostModeSupported
    {
        get => _boostModeSupported;
        set => SetProperty(ref _boostModeSupported, value);
    }

    private string _boostModeError = string.Empty;

    /// <summary>Boost 不可读的原因。</summary>
    public string BoostModeError
    {
        get => _boostModeError;
        set => SetProperty(ref _boostModeError, value);
    }

    private int _maxProcessorState = 100;

    /// <summary>最大处理器状态，50–100（百分比）。</summary>
    public int MaxProcessorState
    {
        get => _maxProcessorState;
        set
        {
            if (SetProperty(ref _maxProcessorState, value))
            {
                MarkDirty();
            }
        }
    }

    private bool _maxProcessorStateSupported = true;

    /// <summary>最大处理器状态这一项本机是否可读。</summary>
    public bool MaxProcessorStateSupported
    {
        get => _maxProcessorStateSupported;
        set => SetProperty(ref _maxProcessorStateSupported, value);
    }

    private string _maxProcessorStateError = string.Empty;

    /// <summary>最大处理器状态不可读的原因。</summary>
    public string MaxProcessorStateError
    {
        get => _maxProcessorStateError;
        set => SetProperty(ref _maxProcessorStateError, value);
    }

    private int _aspm = 1;

    /// <summary>ASPM（PCIe 链路电源管理），0–2。</summary>
    public int Aspm
    {
        get => _aspm;
        set
        {
            if (SetProperty(ref _aspm, value))
            {
                MarkDirty();
            }
        }
    }

    private bool _aspmSupported = true;

    /// <summary>ASPM 这一项本机是否可读。</summary>
    public bool AspmSupported
    {
        get => _aspmSupported;
        set => SetProperty(ref _aspmSupported, value);
    }

    private string _aspmError = string.Empty;

    /// <summary>ASPM 不可读的原因。</summary>
    public string AspmError
    {
        get => _aspmError;
        set => SetProperty(ref _aspmError, value);
    }

    private bool _idleDisable;

    /// <summary>
    /// 空闲禁用（C-State 总开关）。<b>true 表示禁用空闲</b>，与系统设置项
    /// Processor idle disable 的取值同向（写 1 = 禁用，写 0 = 允许）。
    /// </summary>
    public bool IdleDisable
    {
        get => _idleDisable;
        set
        {
            if (SetProperty(ref _idleDisable, value))
            {
                MarkDirty();
            }
        }
    }

    private bool _idleDisableSupported = true;

    /// <summary>C-State 这一项本机是否可读。</summary>
    public bool IdleDisableSupported
    {
        get => _idleDisableSupported;
        set => SetProperty(ref _idleDisableSupported, value);
    }

    private string _idleDisableError = string.Empty;

    /// <summary>C-State 不可读的原因。</summary>
    public string IdleDisableError
    {
        get => _idleDisableError;
        set => SetProperty(ref _idleDisableError, value);
    }

    private string _activeSchemeName = "平衡";

    /// <summary>当前活动的 Windows 电源方案名。</summary>
    public string ActiveSchemeName
    {
        get => _activeSchemeName;
        set => SetProperty(ref _activeSchemeName, value);
    }

    // ==================== 状态 ====================

    private bool _isDirty;

    /// <summary>是否有未保存的改动。为 true 时才显示底部浮动保存条。</summary>
    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }

    private bool _isBusy;

    /// <summary>是否正在与 Worker 通信。为 true 时禁用操作类按钮。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string _statusMessage = "尚未与 Worker 通信";

    /// <summary>页脚状态提示（最近一次操作结果）。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // ==================== 命令 ====================

    /// <summary>同步：从硬件读回当前状态（对应 <c>query-smu-snapshot</c> + <c>query-power-policy</c>）。</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>应用：把当前值下发到硬件与电源策略（对应 <c>apply-tuning</c> + <c>apply-power-policy</c>）。</summary>
    public RelayCommand ApplyCommand { get; }

    /// <summary>保存方案：只写方案文件，不动硬件。</summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>放弃改动：把界面拉回上次读到的状态。</summary>
    public RelayCommand DiscardCommand { get; }

    /// <summary>FMax 上调一档。</summary>
    public RelayCommand StepFMaxUpCommand { get; }

    /// <summary>FMax 下调一档。</summary>
    public RelayCommand StepFMaxDownCommand { get; }

    // ==================== 基线（供「放弃」回滚）====================

    private readonly int _baselineEpp;
    private readonly int _baselineBoostMode;
    private readonly int _baselineMaxProcessorState;
    private readonly int _baselineAspm;
    private readonly bool _baselineIdleDisable;
    private readonly int _baselineFMax;
    private readonly int[] _baselineCoreMargins;

    /// <summary>
    /// 回滚期间抑制置脏。
    /// </summary>
    /// <remarks>
    /// 「放弃」本质上是一次批量赋值，若不抑制，
    /// 每个属性在赋值时都会把页面标脏 —— 于是用户一点「放弃」，
    /// 保存条反而又冒出来了。
    /// </remarks>
    private bool _suppressDirty;

    public TuningViewModel()
    {
        // 方案列表：每一项自己持有选中态，勾选后回调 VM。
        string[] profiles = { "日常办公", "极限游戏" };

        for (int i = 0; i < profiles.Length; i++)
        {
            var option = new ProfileOptionItem(profiles[i], i);
            option.SelectedChanged += _ => ProfileIndex = option.Index;
            ProfileOptions.Add(option);
        }

        // 逐核 CO：预置一份看起来像真实调校档的值
        // （不是全 0 —— 全 0 看不出等宽数字在窄框里会不会挤）。
        int[] seed = { -18, -22, -17, -25, -26, -24, -29, -33, -13, -20, -13, -20, -11, -17, -14, -17 };

        for (int i = 0; i < seed.Length; i++)
        {
            var item = new CoreMarginItem(i, seed[i], CoreMarginMinimum, CoreMarginMaximum, CoreMarginStep);
            CoreMargins.Add(item);

            // 任何一核的偏移变化都把页面标成「未保存」。
            // 订阅放在这里而不是 View 里 —— 页面不需要知道「脏」是怎么算出来的。
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CoreMarginItem.Margin))
                {
                    MarkDirty();
                }
            };
        }

        _baselineEpp = _epp;
        _baselineBoostMode = _boostMode;
        _baselineMaxProcessorState = _maxProcessorState;
        _baselineAspm = _aspm;
        _baselineIdleDisable = _idleDisable;
        _baselineFMax = _fMaxMHz;
        _baselineCoreMargins = CoreMargins.Select(c => c.Margin).ToArray();

        SyncProfileSelection();

        RefreshCommand = new RelayCommand(_ =>
        {
            StatusMessage = "已从硬件读取当前状态";
            DiscardToBaseline();
        });

        ApplyCommand = new RelayCommand(_ =>
        {
            StatusMessage = "硬件参数与电源策略已成功下发";
            IsDirty = false;
        });

        SaveCommand = new RelayCommand(_ =>
        {
            StatusMessage = $"当前修改已保存到「{ProfileName}」";
            IsDirty = false;
        });

        DiscardCommand = new RelayCommand(_ =>
        {
            StatusMessage = "已放弃未保存的修改";
            DiscardToBaseline();
        });

        StepFMaxUpCommand = new RelayCommand(_ =>
            FMaxMHz = Math.Min(FMaxMaximum, FMaxMHz + FMaxStepMHz));

        StepFMaxDownCommand = new RelayCommand(_ =>
            FMaxMHz = Math.Max(FMaxMinimum, FMaxMHz - FMaxStepMHz));
    }

    /// <summary>
    /// 标记页面有未保存改动。
    /// </summary>
    private void MarkDirty()
    {
        if (_suppressDirty)
        {
            return;
        }

        IsDirty = true;
    }

    /// <summary>
    /// 把全部可编辑项拉回基线。
    /// </summary>
    private void DiscardToBaseline()
    {
        _suppressDirty = true;

        try
        {
            Epp = _baselineEpp;
            BoostMode = _baselineBoostMode;
            MaxProcessorState = _baselineMaxProcessorState;
            Aspm = _baselineAspm;
            IdleDisable = _baselineIdleDisable;
            FMaxMHz = _baselineFMax;

            for (int i = 0; i < CoreMargins.Count && i < _baselineCoreMargins.Length; i++)
            {
                CoreMargins[i].Margin = _baselineCoreMargins[i];
            }

            IsDirty = false;
        }
        finally
        {
            _suppressDirty = false;
        }
    }
}

/// <summary>
/// 一个可选方案。
/// </summary>
/// <remarks>
/// 【为什么只在「变为选中」时才回调】
/// 同一组单选按钮里，点中新项会自动取消旧项。
/// 若取消也回调，VM 就会把 ProfileIndex 改回旧项 —— 于是点不中第二项。
/// 只在变为选中时回调，方向才是单向的。
/// </remarks>
public class ProfileOptionItem : ViewModelBase
{
    private bool _isSelected;

    /// <summary>方案名。</summary>
    public string Name { get; }

    /// <summary>在列表中的下标。</summary>
    public int Index { get; }

    /// <summary>是否为当前选中的方案。</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value)
            {
                SelectedChanged?.Invoke(this);
            }
        }
    }

    /// <summary>本项被选中的通知。</summary>
    public event Action<ProfileOptionItem>? SelectedChanged;

    public ProfileOptionItem(string name, int index)
    {
        Name = name;
        Index = index;
    }
}

/// <summary>
/// 单个核心的 CO 偏移项。
///
/// <remarks>
/// 【为什么单独一个类】
/// 逐核格子如果由代码 new 出 16 组控件，核数一变就得重写一遍。
/// 改成绑定一个集合后，核数变了网格自己重排。
///
/// 【为什么输入框走容错解析】
/// 打字中间态（"-"、"-"、空串）都不是合法整数。
/// 直接绑 int 会让非法输入被静默丢弃后又被源值覆盖，用户看着像「打不进去」。
/// 这里把文本入口单独拆出来：非法输入一律忽略，保留上一个有效值。
/// </remarks>
public class CoreMarginItem : ViewModelBase
{
    private readonly int _min;
    private readonly int _max;
    private readonly int _step;
    private int _margin;

    /// <summary>核心序号（从 0 起）。</summary>
    public int Index { get; }

    /// <summary>界面显示名，如「核心 1」。</summary>
    public string DisplayName => $"核心 {Index + 1}";

    /// <summary>该核的 CO 偏移值。</summary>
    public int Margin
    {
        get => _margin;
        set
        {
            if (SetProperty(ref _margin, value))
            {
                OnPropertyChanged(nameof(EditText));
            }
        }
    }

    /// <summary>
    /// 纯文本入口（绑定到 TextBox）。非法输入直接忽略，越界则夹取。
    /// </summary>
    public string EditText
    {
        get => _margin.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                Margin = Math.Clamp(parsed, _min, _max);
            }
        }
    }

    /// <summary>上调一档。</summary>
    public RelayCommand IncrementCommand { get; }

    /// <summary>下调一档。</summary>
    public RelayCommand DecrementCommand { get; }

    public CoreMarginItem(int index, int margin, int min, int max, int step)
    {
        Index = index;
        _margin = margin;
        _min = min;
        _max = max;
        _step = step;

        IncrementCommand = new RelayCommand(_ => Margin = Math.Min(_max, Margin + _step));
        DecrementCommand = new RelayCommand(_ => Margin = Math.Max(_min, Margin - _step));
    }
}
