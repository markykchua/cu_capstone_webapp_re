using Microsoft.UI;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;

namespace EndpointExplorer.Controls;

public sealed partial class FlowElementViewer : UserControl, INotifyPropertyChanged
{
    private bool _isRequestExpanded = false;
    private bool _isResponseExpanded = false;
    private bool _isCurrentElement = false;

    public FlowElementViewer()
    {
        this.InitializeComponent();
        this.DataContext = this;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #region Dependency Properties

    public static readonly DependencyProperty FlowElementProperty =
        DependencyProperty.Register(
            nameof(FlowElement),
            typeof(FlowElement),
            typeof(FlowElementViewer),
            new PropertyMetadata(null, OnFlowElementChanged));

    public FlowElement FlowElement
    {
        get => (FlowElement)GetValue(FlowElementProperty);
        set => SetValue(FlowElementProperty, value);
    }

    #endregion

    #region Event Handlers

    private static void OnFlowElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FlowElementViewer viewer)
        {
            viewer.UpdateViewerData();
        }
    }

    private void ToggleRequestExpand_Click(object sender, RoutedEventArgs e)
    {
        IsRequestExpanded = !IsRequestExpanded;
    }

    private void ToggleResponseExpand_Click(object sender, RoutedEventArgs e)
    {
        IsResponseExpanded = !IsResponseExpanded;
    }

    #endregion

    #region Properties

    public bool IsRequestExpanded
    {
        get => _isRequestExpanded;
        set
        {
            if (_isRequestExpanded != value)
            {
                _isRequestExpanded = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(RequestToggleIcon));
                NotifyPropertyChanged(nameof(IsRequestCollapsed));
            }
        }
    }

    public bool IsResponseExpanded
    {
        get => _isResponseExpanded;
        set
        {
            if (_isResponseExpanded != value)
            {
                _isResponseExpanded = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(ResponseToggleIcon));
                NotifyPropertyChanged(nameof(IsResponseCollapsed));
            }
        }
    }

    public bool IsCurrentElement
    {
        get => _isCurrentElement;
        set
        {
            if (_isCurrentElement != value)
            {
                _isCurrentElement = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(RequestColor));
                NotifyPropertyChanged(nameof(ResponseStatusColor));
            }
        }
    }

    public bool IsRequestCollapsed => !IsRequestExpanded;
    public bool IsResponseCollapsed => !IsResponseExpanded;

    public string RequestToggleIcon => IsRequestExpanded ? "▲" : "▼";
    public string ResponseToggleIcon => IsResponseExpanded ? "▲" : "▼";

    public JToken RequestToken { get; private set; }
    public JToken ResponseToken { get; private set; }

    public string RequestMethod { get; private set; }
    public string RequestEndpoint { get; private set; }
    public string RequestHost { get; private set; }
    public string ResponseStatus { get; private set; }
    public string ResponseStatusName { get; private set; }
    public Color ResponseStatusColor => GetStatusColor(ResponseStatus);
    public Color RequestColor => IsCurrentElement ? GetMutedColor(Colors.Gray) : Colors.Gray;

    #endregion

    private void UpdateViewerData()
    {
        if (FlowElement?.Value is JObject jObject)
        {
            // Extract Request and Response objects
            if (jObject.TryGetValue("Request", out JToken requestToken) && requestToken is JObject request)
            {
                RequestToken = request;
                RequestMethod = request.Value<string>("Method") ?? "GET";
                RequestHost = new Uri(request.Value<string>("Url") ?? "").Host;
                RequestEndpoint = new Uri(request.Value<string>("Url") ?? "").AbsolutePath;
                NotifyPropertyChanged(nameof(RequestToken));
                NotifyPropertyChanged(nameof(RequestMethod));
                NotifyPropertyChanged(nameof(RequestEndpoint));
            }

            if (jObject.TryGetValue("Response", out JToken responseToken) && responseToken is JObject response)
            {
                ResponseToken = response;
                ResponseStatus = response.Value<string>("Status") ?? "200";
                ResponseStatusName = GetStatusName(ResponseStatus);
                NotifyPropertyChanged(nameof(ResponseToken));
                NotifyPropertyChanged(nameof(ResponseStatus));
                NotifyPropertyChanged(nameof(ResponseStatusName));
            }
        }
    }

    private string GetStatusName(string statusCode)
    {
        return statusCode switch
        {
            "200" => "OK",
            "201" => "Created",
            "204" => "No Content",
            "400" => "Bad Request",
            "401" => "Unauthorized",
            "403" => "Forbidden",
            "404" => "Not Found",
            "500" => "Internal Server Error",
            _ => ""
        };
    }

    private Color GetStatusColor(string statusCode)
    {
        var color = statusCode switch
        {
            "200" => Colors.Green,
            "201" => Colors.Green,
            "204" => Colors.Green,
            "400" => Colors.Red,
            "401" => Colors.Red,
            "403" => Colors.Red,
            "404" => Colors.Red,
            "500" => Colors.Red,
            _ => Colors.Black
        };
        if (IsCurrentElement)
        {
            return GetMutedColor(color);
        }
        return color;
    }

    private Color GetMutedColor(Color color)
    {
        return Color.FromArgb(
            (byte)(color.A * 0.5),
            (byte)(color.R * 1),
            (byte)(color.G * 1),
            (byte)(color.B * 1)
        );
    }
}