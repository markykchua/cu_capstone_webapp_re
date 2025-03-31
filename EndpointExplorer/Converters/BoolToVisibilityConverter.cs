using Microsoft.UI.Xaml.Data;
namespace EndpointExplorer.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool result = value is bool b && b;
        Visibility visibility = result ? Visibility.Visible : Visibility.Collapsed;
        return visibility;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}