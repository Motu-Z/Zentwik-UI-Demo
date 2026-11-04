using System.Collections.ObjectModel;
using System.Globalization;

namespace ZentwikThemeDemo.ViewModels;

/// <summary>
/// 进程页视图模型。
///
/// <remarks>
/// 【两张表的语义完全不同，不能混】
/// 上半「运行中进程」是**当前系统里活着的进程**（对应 Core 的
/// <c>query-processes</c> → <c>ProcessEntryPayload</c>）：
/// 字段有 pid / name / path / cpu / priority / matchedRuleName。
/// 下半「已保存规则」是**持久化的调度规则**（对应
/// <c>query-scheduling-rules</c> → <c>ScheduleRulePayload</c>）：
/// 字段有 id / processName / targetType / targetValue / priority / enabled / path。
/// 前者会随进程启停实时变，后者只随用户编辑变。
///
/// 【核心掩码只有三种目标】
/// <c>ScheduleRulePayload.targetType</c> 取值 ccd / mask / none：
///   · "ccd"  → targetValue 是 CCD 索引（"0" / "1"）
///   · "mask" → targetValue 是掩码的十进制值（ulong 字符串）
///   · "none" → targetValue 为空串
/// 对应 Core 的 <c>SchedulingTarget</c> 枚举：None / CCD0 / CCD1 / TargetMask。
/// 界面上「核心掩码」这一列展示的是**绑到了哪些核**，
/// 所以自定义掩码要显示成区间文本（"0-7,16-23"），而不是裸的十进制数 ——
/// <c>MaskFormatter.ToRangeText</c> 就是干这个的。
///
/// 【优先级档位来自 ProcessPriority.Options】
/// 高 / 高于标准 / 标准 / 低于标准 / 低。
/// 刻意不含「实时」：它的优先级高于系统输入线程，
/// 忙等的实时进程能让鼠标键盘完全失去响应 —— 代价与收益完全不成比例。
/// </remarks>
public class ProcessViewModel : ViewModelBase
{
    // ==================== 运行中进程 ====================

    /// <summary>运行中的进程列表。</summary>
    public ObservableCollection<RunningProcessItem> RunningProcesses { get; } = new();

    /// <summary>
    /// 搜索关键字。过滤的是进程名与路径（不区分大小写）。
    /// </summary>
    private string _searchText = string.Empty;

    /// <summary>搜索框文本。</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    /// <summary>过滤后的进程列表（界面上真正绑定的那个）。</summary>
    public ObservableCollection<RunningProcessItem> FilteredProcesses { get; } = new();

    // ==================== 已保存规则 ====================

    /// <summary>已保存的调度规则。</summary>
    public ObservableCollection<ScheduleRuleItem> SavedRules { get; } = new();

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

    /// <summary>刷新进程列表（对应 <c>query-processes</c>）。</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>应用规则（对应 <c>apply-process-priority</c> + <c>save-scheduling-rule</c>）。</summary>
    public RelayCommand ApplyRuleCommand { get; }

    /// <summary>管理掩码（掩码表在 Core 侧，本页只做入口）。</summary>
    public RelayCommand ManageMaskCommand { get; }

    /// <summary>启用 / 禁用一条规则（对应 <c>save-scheduling-rule</c> 的 enabled 字段）。</summary>
    public RelayCommand ToggleRuleCommand { get; }

    /// <summary>删除一条规则（对应 <c>delete-scheduling-rule</c>，按 Id 定位）。</summary>
    public RelayCommand DeleteRuleCommand { get; }

    public ProcessViewModel()
    {
        SeedRunning();
        SeedRules();

        RefreshCommand = new RelayCommand(_ =>
        {
            StatusMessage = $"已刷新运行中进程（{RunningProcesses.Count} 个）";
        });

        ApplyRuleCommand = new RelayCommand(_ =>
        {
            StatusMessage = "规则已下发到后台监听";
        });

        ManageMaskCommand = new RelayCommand(_ =>
        {
            StatusMessage = "掩码管理将在配置页提供";
        });

        ToggleRuleCommand = new RelayCommand(p =>
        {
            if (p is not ScheduleRuleItem rule)
            {
                return;
            }

            rule.Enabled = !rule.Enabled;
            StatusMessage = $"「{rule.ProcessName}」已{(rule.Enabled ? "启用" : "禁用")}";
        });

        DeleteRuleCommand = new RelayCommand(p =>
        {
            if (p is not ScheduleRuleItem rule)
            {
                return;
            }

            SavedRules.Remove(rule);
            StatusMessage = $"已删除「{rule.ProcessName}」的规则";
        });
    }

    /// <summary>按搜索关键字过滤进程列表。</summary>
    private void ApplyFilter()
    {
        FilteredProcesses.Clear();

        foreach (var item in RunningProcesses)
        {
            if (Matches(item))
            {
                FilteredProcesses.Add(item);
            }
        }
    }

    /// <summary>判断进程是否命中当前搜索关键字。</summary>
    private bool Matches(RunningProcessItem item)
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            return true;
        }

        return item.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || item.Path.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>预置一份运行中进程。</summary>
    private void SeedRunning()
    {
        RunningProcesses.Add(new RunningProcessItem(
            8124, "cs2.exe", @"D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe",
            4.2, "高", "CCD0"));

        RunningProcesses.Add(new RunningProcessItem(
            4210, "steam.exe", @"C:\Program Files (x86)\Steam\steam.exe",
            0.8, "标准", null));

        RunningProcesses.Add(new RunningProcessItem(
            6332, "obs64.exe", @"C:\Program Files\obs-studio\bin\64bit\obs64.exe",
            2.1, "高于标准", null));

        RunningProcesses.Add(new RunningProcessItem(
            1908, "chrome.exe", @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            6.4, "标准", null));

        RunningProcesses.Add(new RunningProcessItem(
            7745, "agent.exe", @"C:\ProgramData\Zentwik\worker\agent.exe",
            0.3, "低于标准", null));

        ApplyFilter();
    }

    /// <summary>预置一份已保存规则。</summary>
    private void SeedRules()
    {
        SavedRules.Add(new ScheduleRuleItem(
            "r-1", "cs2.exe", @"D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive",
            "CCD0", "高", enabled: true));

        SavedRules.Add(new ScheduleRuleItem(
            "r-2", "obs64.exe", @"C:\Program Files\obs-studio",
            "0-7,16-23", "高于标准", enabled: true));

        SavedRules.Add(new ScheduleRuleItem(
            "r-3", "steam.exe", @"C:\Program Files (x86)\Steam",
            "不调度", "低于标准", enabled: false));
    }
}

/// <summary>
/// 一条运行中的进程。
/// </summary>
/// <remarks>
/// 字段对齐 Core 的 <c>ProcessEntryPayload</c>：pid / name / path / cpu /
/// priority / matchedRuleName。
/// </remarks>
public class RunningProcessItem : ViewModelBase
{
    /// <summary>进程 Id。</summary>
    public int Pid { get; }

    /// <summary>进程名（含 .exe）。</summary>
    public string Name { get; }

    /// <summary>可执行文件完整路径。</summary>
    public string Path { get; }

    /// <summary>CPU 占用百分比（采样值）。</summary>
    public double Cpu { get; }

    /// <summary>显示用的占用文本，如「4.2%」。</summary>
    public string CpuText => Cpu.ToString("0.0", CultureInfo.InvariantCulture) + "%";

    /// <summary>当前优先级（显示文案）。</summary>
    public string Priority { get; }

    /// <summary>命中的规则名；未命中为 null。</summary>
    public string? MatchedRuleName { get; }

    /// <summary>绑定的核心掩码显示名；未绑定为 null。</summary>
    public string? MaskLabel { get; }

    public RunningProcessItem(int pid, string name, string path, double cpu, string priority, string? maskLabel)
    {
        Pid = pid;
        Name = name;
        Path = path;
        Cpu = cpu;
        Priority = priority;
        MaskLabel = maskLabel;
    }
}

/// <summary>
/// 一条已保存的调度规则。
/// </summary>
/// <remarks>
/// 字段对齐 Core 的 <c>ScheduleRulePayload</c>：
/// id / processName / targetType / targetValue / priority / enabled / path。
/// 界面上不显示 targetType + targetValue 两个原始字段，
/// 而是合成一个人能读的「核心掩码」列。
/// </remarks>
public class ScheduleRuleItem : ViewModelBase
{
    private bool _enabled;

    /// <summary>规则 Id。删除与更新都按它定位，不按进程名。</summary>
    public string Id { get; }

    /// <summary>进程名（含 .exe）。</summary>
    public string ProcessName { get; }

    /// <summary>规则来源路径（仅供回显与排障）。</summary>
    public string Path { get; }

    /// <summary>绑定的核心掩码显示名，如「CCD0」「0-7,16-23」「不调度」。</summary>
    public string MaskLabel { get; }

    /// <summary>优先级档位（显示文案）。</summary>
    public string Priority { get; }

    /// <summary>规则是否启用。关掉的规则仍保留，但后台不会按它调度。</summary>
    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    /// <summary>启用按钮的文案（启用与禁用是一对互斥动作，只显示当前可执行的那个）。</summary>
    public string ToggleLabel => Enabled ? "禁用" : "启用";

    public ScheduleRuleItem(
        string id,
        string processName,
        string path,
        string maskLabel,
        string priority,
        bool enabled)
    {
        Id = id;
        ProcessName = processName;
        Path = path;
        MaskLabel = maskLabel;
        Priority = priority;
        _enabled = enabled;
    }
}
