using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EndpointExplorer.Converters;
public class BoolToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (bool)value ? new GridLength(250) : new GridLength(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}