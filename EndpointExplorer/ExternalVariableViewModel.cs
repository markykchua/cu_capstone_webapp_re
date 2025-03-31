using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EndpointExplorer;

public class ExternalVariableViewModel : INotifyPropertyChanged
{
    private string _key;
    private JToken _value;

    public event PropertyChangedEventHandler PropertyChanged;

    public ExternalVariableViewModel(string key, JToken value)
    {
        _key = key;
        _value = value;
    }

    public string Key => _key;

    public string DisplayValue
    {
        get
        {
            if (_value == null || _value.Type == JTokenType.Null) return "[null]";
            string display = _value.ToString(Formatting.None);
            const int maxLength = 50;
            if (display.Length > maxLength)
            {
                return display.Substring(0, maxLength) + "...";
            }
            return display;
        }
    }

    public string TooltipValue
    {
        get
        {
            if (_value == null || _value.Type == JTokenType.Null) return "[null]";
            if (_value.Type == JTokenType.Object || _value.Type == JTokenType.Array)
            {
                return _value.ToString(Formatting.Indented);
            }
            return _value.ToString(Formatting.None);
        }
    }

    public JToken OriginalValue => _value;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
