using System.Collections.ObjectModel;

namespace ZentwikThemeDemo.ViewModels;

/// <summary>
/// 配置方案的跨页共享状态。
///
/// <remarks>
/// 【为什么是静态单例】
/// 方案不是某一页的私有数据 —— 用户在「调校」页切到「极限游戏」，
/// 回头在「电源」「进程」页看到的就必须是同一份方案。
/// 若每个 ViewModel 各持一份，切页就会把用户的选择清掉，
/// 「三页都有同一个大窗」也就名存实亡。
/// 因此状态放在进程级单例里，页面只是它的观察者。
///
/// 【字段语义对齐 Zentwik.Core.ProfileSnapshot】
/// Id / Name / Note / IsActive 四个字段与 Core 的方案模型同名同义：
///   · Id 是唯一句柄（32 位无横线 GUID），改名不改 Id；
///   · Note 是列表里那行描述，留空即不显示；
///   · IsActive 表示这份方案当前生效中。
/// 界面层不自己发明 Id —— 那是 Worker 的职责。
/// </remarks>
public sealed class ProfileSession : ViewModelBase
{
    /// <summary>进程级唯一实例。</summary>
    public static ProfileSession Instance { get; } = new();

    private ProfileSession()
    {
        // 预置三份方案，覆盖三种典型场景。
        // Id 用固定字面量是为了让截图与自检可复现；真实实现由 Worker 生成 GUID。
        Profiles.Add(new ProfileItem("p-office", "日常办公", "均衡功耗，适合文档与网页", isActive: true));
        Profiles.Add(new ProfileItem("p-game", "极限游戏", "解除功耗与温度限制，满血输出"));
        Profiles.Add(new ProfileItem("p-stream", "直播推流", "多任务并行，保留核心余量"));

        // 当前方案必须从 IsActive 推导出来。
        // 只设 IsActive 而不设 Current 的话，方案条上会显示「未选择方案」——
        // 而大窗里那份却带着「使用中」标记，两处自相矛盾。
        Current = Profiles.FirstOrDefault(p => p.IsActive);

        OpenOverlayCommand = new RelayCommand(_ => IsOverlayOpen = true);
        SelectProfileCommand = new RelayCommand(p => { if (p is ProfileItem item) Select(item); });
        CloseOverlayCommand = new RelayCommand(_ => CancelOverlay());
        ApplySelectionCommand = new RelayCommand(_ => ApplySelection());
        StartRenameCommand = new RelayCommand(p => { if (p is ProfileItem item) BeginRename(item); });
        CommitRenameCommand = new RelayCommand(p => { if (p is ProfileItem item) CommitRename(item); });
        CancelRenameCommand = new RelayCommand(p => { if (p is ProfileItem item) item.IsRenaming = false; });
        AskDeleteCommand = new RelayCommand(p => { if (p is ProfileItem item) AskDelete(item); });
        ConfirmDeleteCommand = new RelayCommand(p => { if (p is ProfileItem item) DeleteProfile(item); });
        CancelDeleteCommand = new RelayCommand(p => { if (p is ProfileItem item) item.IsConfirmingDelete = false; });
        CreateProfileCommand = new RelayCommand(_ => CreateProfile());
    }

    /// <summary>全部方案。</summary>
    public ObservableCollection<ProfileItem> Profiles { get; } = new();

    // ==================== 大窗开合 ====================

    private bool _isOverlayOpen;

    /// <summary>配置方案大窗是否打开。</summary>
    public bool IsOverlayOpen
    {
        get => _isOverlayOpen;
        set
        {
            // 【为什么不用 SetProperty 的返回值来判定「刚刚关闭了」】
            // SetProperty 在值没变时返回 false —— 而「关闭」这个动作
            // 可能发生在它本来就是 false 的情况下（先取消了预备选中，
            // 再点取消关闭）。那样清理逻辑就被跳过了，
            // 下次打开大窗会看到上一次没提交完的编辑现场。
            // 所以这里先判「值确实变成了 false」，再无条件做清理。
            bool wasOpen = _isOverlayOpen;

            if (SetProperty(ref _isOverlayOpen, value) || (wasOpen && !value))
            {
                // 关闭时清掉临时的选中态与行内编辑态，
                // 否则下次打开会看到上一次没提交完的编辑现场。
                foreach (var item in Profiles)
                {
                    item.IsSelected = false;
                    item.IsRenaming = false;
                    item.IsConfirmingDelete = false;
                }
            }
        }
    }

    // ==================== 当前方案 ====================

    private ProfileItem? _current;

    /// <summary>当前生效的方案。</summary>
    public ProfileItem? Current
    {
        get => _current;
        private set
        {
            if (SetProperty(ref _current, value))
            {
                OnPropertyChanged(nameof(CurrentName));
                OnPropertyChanged(nameof(CurrentNote));
                OnPropertyChanged(nameof(HasCurrent));
            }
        }
    }

    /// <summary>当前方案名（供标题条显示）。</summary>
    public string CurrentName => Current?.Name ?? "未选择方案";

    /// <summary>当前方案的描述（留空则不显示副标题）。</summary>
    public string CurrentNote => Current?.Note ?? string.Empty;

    /// <summary>是否已有生效方案。</summary>
    public bool HasCurrent => Current is not null;

    // ==================== 状态提示 ====================

    private string _statusMessage = string.Empty;

    /// <summary>大窗底部的状态提示。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // ==================== 命令 ====================

    /// <summary>打开大窗。</summary>
    public RelayCommand OpenOverlayCommand { get; }

    /// <summary>在列表里预备选中某个方案（尚未生效）。</summary>
    public RelayCommand SelectProfileCommand { get; }

    /// <summary>关闭大窗（放弃本次选择）。</summary>
    public RelayCommand CloseOverlayCommand { get; }

    /// <summary>确认应用选中方案。</summary>
    public RelayCommand ApplySelectionCommand { get; }

    /// <summary>进入行内重命名。</summary>
    public RelayCommand StartRenameCommand { get; }

    /// <summary>提交重命名。</summary>
    public RelayCommand CommitRenameCommand { get; }

    /// <summary>取消重命名。</summary>
    public RelayCommand CancelRenameCommand { get; }

    /// <summary>请求删除（进入确认态）。</summary>
    public RelayCommand AskDeleteCommand { get; }
    /// <summary>确认删除。</summary>
    public RelayCommand ConfirmDeleteCommand { get; }

    /// <summary>取消删除。</summary>
    public RelayCommand CancelDeleteCommand { get; }

    /// <summary>新建方案。</summary>
    public RelayCommand CreateProfileCommand { get; }

    // ==================== 行为 ====================

    /// <summary>
    /// 选中某个方案（尚未生效）。
    /// </summary>
    /// <remarks>
    /// 选中与生效是两件事：大窗里点一行只是「预备」，
    /// 底部「确定」才真正切换 —— 这样误点不会立刻改掉正在用的方案。
    /// </remarks>
    private void Select(ProfileItem item)
    {
        foreach (var other in Profiles)
        {
            other.IsSelected = ReferenceEquals(other, item);
        }
    }

    /// <summary>把预备选中的方案设为生效方案。</summary>
    private void ApplySelection()
    {
        ProfileItem? selected = Profiles.FirstOrDefault(p => p.IsSelected);

        if (selected is null)
        {
            StatusMessage = "请先选择一个方案";
            return;
        }

        foreach (var item in Profiles)
        {
            item.IsActive = ReferenceEquals(item, selected);
        }

        Current = selected;
        StatusMessage = $"已切换到「{selected.Name}」";
        IsOverlayOpen = false;
    }

    /// <summary>关闭大窗并放弃预备选择。</summary>
    private void CancelOverlay()
    {
        // 显式清掉预备选中态。
        // 不能只依赖 IsOverlayOpen 的 setter —— 大窗本来就关着时
        // （用户先取消选中、再点取消），setter 里的清理不会触发。
        foreach (var item in Profiles)
        {
            item.IsSelected = false;
        }

        IsOverlayOpen = false;
    }

    /// <summary>进入行内重命名。</summary>
    private void BeginRename(ProfileItem item)
    {
        // 同一时刻只允许一行在编辑 —— 两个输入框抢焦点会打架。
        foreach (var other in Profiles)
        {
            other.IsRenaming = false;
        }

        item.IsRenaming = true;
        item.RenameText = item.Name;
    }

    /// <summary>
    /// 进入「确认删除」态。
    /// </summary>
    /// <remarks>
    /// 必须与重命名态互斥：两者都显示行内控件，
    /// 同时为 true 时一行里会挤下输入框 + 确认框两套东西。
    /// </remarks>
    private void AskDelete(ProfileItem item)
    {
        foreach (var other in Profiles)
        {
            other.IsRenaming = false;
            other.IsConfirmingDelete = false;
        }

        item.IsConfirmingDelete = true;
    }

    /// <summary>
    /// 提交重命名。
    /// </summary>
    /// <remarks>
    /// 空名与纯空白一律拒绝并保留原名 —— 与 Worker 侧「Trim 后非空」的规则一致。
    /// </remarks>
    private void CommitRename(ProfileItem item)
    {
        string name = item.RenameText.Trim();

        if (name.Length == 0)
        {
            StatusMessage = "方案名不能为空";
            item.IsRenaming = false;
            return;
        }

        item.Name = name;
        item.IsRenaming = false;
        StatusMessage = $"已重命名为「{name}」";
    }

    /// <summary>删除方案。</summary>
    private void DeleteProfile(ProfileItem item)
    {
        item.IsConfirmingDelete = false;

        Profiles.Remove(item);

        // 删掉的正好是当前生效方案 → 当前方案置空。
        // 不自动挑一个顶上：自动切换等于替用户做了一次方案变更。
        if (ReferenceEquals(Current, item))
        {
            Current = null;
        }

        StatusMessage = $"已删除「{item.Name}」";
    }

    /// <summary>新建一份空白方案。</summary>
    private void CreateProfile()
    {
        var item = new ProfileItem(
            "p-" + Guid.NewGuid().ToString("N")[..8],
            "新方案",
            string.Empty);

        Profiles.Add(item);
        Select(item);

        BeginRename(item);
        StatusMessage = "已新建方案，请输入名称";
    }
}

/// <summary>
/// 一份配置方案。
/// </summary>
/// <remarks>
/// 字段与 <c>Zentwik.Core.ProfileSnapshot</c> 对齐：Id / Name / Note / IsActive。
/// 额外的 IsSelected / IsRenaming / IsConfirmingDelete 是纯界面状态，
/// 不落盘，也不参与方案的身份判定。
/// </remarks>
public sealed class ProfileItem : ViewModelBase
{
    private string _name;
    private bool _isSelected;
    private bool _isRenaming;
    private bool _isConfirmingDelete;
    private string _renameText = string.Empty;

    /// <summary>方案 Id。改名与删除都按它定位，不按名字。</summary>
    public string Id { get; }

    /// <summary>方案名。</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(NameInitial));
            }
        }
    }

    /// <summary>方案名首字符，用于画头像圆点。</summary>
    public string NameInitial => Name.Length > 0 ? Name[..1] : "?";

    /// <summary>列表里的那行描述。留空即不显示。</summary>
    public string Note { get; }

    /// <summary>这份方案是否当前生效。</summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private bool _isActive;

    /// <summary>在大窗里被预备选中（尚未生效）。</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>是否处于行内重命名态。</summary>
    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetProperty(ref _isRenaming, value);
    }

    /// <summary>重命名输入框的文本。</summary>
    public string RenameText
    {
        get => _renameText;
        set => SetProperty(ref _renameText, value);
    }

    /// <summary>是否处于「确认删除」态。</summary>
    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        set => SetProperty(ref _isConfirmingDelete, value);
    }

    public ProfileItem(string id, string name, string note, bool isActive = false)
    {
        Id = id;
        _name = name;
        Note = note;
        _isActive = isActive;
    }
}
