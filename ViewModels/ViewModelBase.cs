using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ZentwikThemeDemo.ViewModels;

/// <summary>
/// 轻量 MVVM 基类。
/// </summary>
/// <remarks>
/// 不引入 CommunityToolkit.Mvvm：这个 Demo 只用到 INotifyPropertyChanged 与
/// 一个简单的 RelayCommand，为此拉进一个外部包不划算，
/// 而且离线的 NuGet 源未必有该包。真实项目里建议用 CommunityToolkit.Mvvm。
/// </remarks>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// 简单命令实现。
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);
}
