using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EndpointExplorer.Controls;

public class ExportViewModel : INotifyPropertyChanged
{
    private readonly KeyValuePair<string, Export> _exportData;

    public event PropertyChangedEventHandler PropertyChanged;

    public ExportViewModel(KeyValuePair<string, Export> exportData)
    {
        _exportData = exportData;
    }

    public string Key => _exportData.Key;
    public string JsonPath => _exportData.Value?.JsonPath ?? "";
    public string Regex => _exportData.Value?.Regex ?? "";
    public KeyValuePair<string, Export> OriginalData => _exportData;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}