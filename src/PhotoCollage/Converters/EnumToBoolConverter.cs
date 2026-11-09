using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace DuoTuXiuXiu.Converters;

/// <summary>
/// 把一个值与 ConverterParameter 做相等比较，用于把枚举 / 数字 / 字符串绑定到 RadioButton.IsChecked。
/// ConvertBack 在选中时写回对应值，取消选中时不做任何事（保证单选组行为）。
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null) return BindingOperations.DoNothing;

        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var text = parameter.ToString();

        if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return i;
        if (type == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d;
        if (type == typeof(string)) return text;
        if (type.IsEnum && text is not null) return Enum.Parse(type, text);

        return BindingOperations.DoNothing;
    }
}
