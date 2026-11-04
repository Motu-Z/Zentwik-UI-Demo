using System.Collections.ObjectModel;
using System.Globalization;

namespace ZentwikThemeDemo.ViewModels;

/// <summary>
/// 调校页视图模型。
///
/// <remarks>
/// 【本页只管 SMU 硬件调校】
/// 电源策略已迁到第三页「电源」—— 它走 Windows 电源计划接口，
/// 与 SMU 是两条不同的通路，混在一页会让两个「应用」按钮语义打架。
/// 电源策略的字段与语义见 <see cref="PowerViewModel"/>。
///
/// 【配置方案不在本页】
/// 方案列表 / 切换 / 重命名 / 删除统一由
/// <see cref="ProfileSession"/>（跨页共享单例）承担，
/// 页面只放一个 ProfileBar 入口，模态大窗在 ProfileOverlay 里。
/// </remarks>/// </remarks>
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

        _baselineFMax = _fMaxMHz;
        _baselineCoreMargins = CoreMargins.Select(c => c.Margin).ToArray();

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
            StatusMessage = $"当前修改已保存到「{ProfileSession.Instance.CurrentName}」";
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
