using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZentwikThemeDemo.Converters;

/// <summary>
/// bool → Visibility。true 时为 Visible。
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;

        // 参数可传 "invert" 反转语义
        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

/// <summary>
/// bool → Visibility。true 时为 Collapsed（用于「面板展开时隐藏占位」的反向场景）。
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        return flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Collapsed;
    }
}

/// <summary>
/// int → bool（按值相等），用于「分段控件绑定枚举值」。
/// </summary>
/// <remarks>
/// 一组 RadioButton 的 <c>IsChecked</c> 是 bool，
/// 而 Boost / ASPM 这类设置在契约里是 int（0–4 / 0–2）。
/// <c>ConverterParameter</c> 写要匹配的下标，例如 <c>ConverterParameter=2</c>。
///
/// 【ConvertBack 必须返回 DoNothing，不能返回 false】
/// 同一 GroupName 内的 RadioButton 会**自动取消**其他项的选中，
/// 于是被取消的那一项会拿 false 回写。
/// 若把 false 当成「值 = 0」，刚点中「激进」就会被兄弟项的取消动作改回「关闭」——
/// 表现为「点不中第二档」。
/// </remarks>
public sealed class IntEqualsBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int current &&
            parameter is not null &&
            int.TryParse(parameter.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int wanted))
        {
            return current == wanted;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool b || !b ||
            parameter is null ||
            !int.TryParse(parameter.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int wanted))
        {
            return Binding.DoNothing;
        }

        return wanted;
    }
}
