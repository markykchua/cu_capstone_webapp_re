namespace EndpointExplorer.Controls;
public class ExportChangedEventArgs : EventArgs
{
    public string OriginalName { get; }
    public string Name { get; }
    public string JsonPath { get; }
    public string Regex { get; }

    public ExportChangedEventArgs(string name, string jsonPath = "", string regex = "")
    {
        OriginalName = name;
        Name = name;
        JsonPath = jsonPath ?? "";
        Regex = regex ?? "";
    }

    public ExportChangedEventArgs(string originalName, string newName, string jsonPath, string regex)
    {
        OriginalName = originalName;
        Name = newName;
        JsonPath = jsonPath ?? "";
        Regex = regex ?? "";
    }
}
