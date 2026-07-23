using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace Mastemis.Client.Converters;

public sealed class SectionIndexToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is int selected && int.TryParse(parameter?.ToString(), CultureInfo.InvariantCulture, out var expected) && selected == expected
            ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
