using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;
using Microsoft.UI.Dispatching;
using System.Diagnostics;

namespace EndpointExplorer.Controls;

public sealed partial class FlowElementViewer : UserControl, INotifyPropertyChanged
{
    // --- Fields ---
    private bool _isRequestExpanded = false;
    private bool _isResponseExpanded = false;
    private bool _isCurrentElement = false;
    private JToken _requestToken;
    private JToken _responseToken;
    private string _requestMethod;
    private string _requestEndpoint;
    private string _requestHost;
    private string _responseStatus;
    private string _responseStatusName;

    // --- Events ---
    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler<JsonEditEventArgs> JsonEdited;
    public event EventHandler<ExportChangedEventArgs> ExportAdded;
    public event EventHandler<ExportChangedEventArgs> ExportEdited;
    public event EventHandler<ExportChangedEventArgs> ExportDeleted;

    // --- Collections ---
    public ObservableCollection<ExportViewModel> ObservableExports { get; } = new ObservableCollection<ExportViewModel>();

    // --- Constructor ---
    public FlowElementViewer()
    {
        this.InitializeComponent();
    }

    private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
    {
        if (DispatcherQueue.HasThreadAccess) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
        else
        {
            DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }
    }

    #region Dependency Properties
    public static readonly DependencyProperty FlowElementProperty =
        DependencyProperty.Register(
            nameof(FlowElement), typeof(FlowElement), typeof(FlowElementViewer),
            new PropertyMetadata(null, OnFlowElementChanged));

    public FlowElement FlowElement
    {
        get => (FlowElement)GetValue(FlowElementProperty);
        set => SetValue(FlowElementProperty, value);
    }

    private static void OnFlowElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FlowElementViewer viewer)
        {
            viewer.UpdateViewerData();
        }
    }
    #endregion

    #region UI State Properties (IsExpanded, IsCurrent)
    public bool IsRequestExpanded
    {
        get => _isRequestExpanded;
        set
        {
            if (_isRequestExpanded != value)
            {
                _isRequestExpanded = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(IsRequestCollapsed));
                NotifyPropertyChanged(nameof(RequestToggleIcon));
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
                NotifyPropertyChanged(nameof(IsResponseCollapsed));
                NotifyPropertyChanged(nameof(ResponseToggleIcon));
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

    #endregion

    #region Calculated Properties (for Bindings)
    public bool IsRequestCollapsed => !IsRequestExpanded;
    public bool IsResponseCollapsed => !IsResponseExpanded;
    public string RequestToggleIcon => IsRequestExpanded ? "▲" : "▼";
    public string ResponseToggleIcon => IsResponseExpanded ? "▲" : "▼";
    public JToken RequestToken
    {
        get => _requestToken; private set
        {
            if (!JToken.DeepEquals(_requestToken, value))
            {
                _requestToken = value; NotifyPropertyChanged();
            }
        }
    }
    public JToken ResponseToken
    {
        get => _responseToken; private set
        {
            if (!JToken.DeepEquals(_responseToken, value))
            {
                _responseToken = value; NotifyPropertyChanged();
            }
        }
    }
    public string RequestMethod
    {
        get => _requestMethod; private set
        {
            if (_requestMethod != value)
            {
                _requestMethod = value; NotifyPropertyChanged();
            }
        }
    }
    public string RequestEndpoint
    {
        get => _requestEndpoint; private set
        {
            if (_requestEndpoint != value)
            {
                _requestEndpoint = value; NotifyPropertyChanged();
            }
        }
    }
    public string RequestHost
    {
        get => _requestHost; private set
        {
            if (_requestHost != value)
            {
                _requestHost = value; NotifyPropertyChanged();
            }
        }
    }
    public string ResponseStatus
    {
        get => _responseStatus; private set
        {
            if (_responseStatus != value)
            {
                _responseStatus = value; NotifyPropertyChanged();
            }
        }
    }
    public string ResponseStatusName
    {
        get => _responseStatusName; private set
        {
            if (_responseStatusName != value)
            {
                _responseStatusName = value; NotifyPropertyChanged();
            }
        }
    }
    public Color ResponseStatusColor => GetStatusColor(_responseStatus);
    public Color RequestColor => IsCurrentElement ? GetMutedColor(Colors.Gray) : Colors.Gray;
    public Visibility ExportsVisibility => ObservableExports.Any() ? Visibility.Visible : Visibility.Collapsed;
    #endregion

    private void UpdateViewerData()
    {
        JToken tempRequestToken = null, tempResponseToken = null;
        string tempRequestMethod = null, tempRequestHost = null, tempRequestEndpoint = null;
        string tempResponseStatus = null, tempResponseStatusName = null;

        if (FlowElement?.Value is JObject jObject)
        {
            if (jObject.TryGetValue("Request", out JToken reqToken) && reqToken is JObject request)
            {
                tempRequestToken = request;
                tempRequestMethod = request.Value<string>("Method") ?? "GET";
                string url = request.Value<string>("Url");
                if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    tempRequestHost = uri.Host; tempRequestEndpoint = uri.AbsolutePath;
                }
                else
                {
                    tempRequestHost = "Invalid URL"; tempRequestEndpoint = url ?? "";
                }
            }
            if (jObject.TryGetValue("Response", out JToken respToken) && respToken is JObject response)
            {
                tempResponseToken = response;
                tempResponseStatus = response.Value<string>("Status") ?? "N/A";
                tempResponseStatusName = GetStatusName(tempResponseStatus);
            }
        }

        RequestToken = tempRequestToken;
        RequestMethod = tempRequestMethod;
        RequestHost = tempRequestHost;
        RequestEndpoint = tempRequestEndpoint;
        ResponseToken = tempResponseToken;
        ResponseStatus = tempResponseStatus;
        ResponseStatusName = tempResponseStatusName;

        NotifyPropertyChanged(nameof(RequestColor));
        NotifyPropertyChanged(nameof(ResponseStatusColor));

        UpdateObservableExports();
    }


    private void UpdateObservableExports()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ObservableExports.Clear();
            if (FlowElement?.Exports != null)
            {
                foreach (var kvp in FlowElement.Exports.OrderBy(kv => kv.Key))
                {
                    ObservableExports.Add(new ExportViewModel(kvp));
                }
            }
            NotifyPropertyChanged(nameof(ExportsVisibility));
        });
    }

    #region Event Handlers (Toggles, JsonEdit)
    private void ToggleRequestExpand_Click(object sender, RoutedEventArgs e) { IsRequestExpanded = !IsRequestExpanded; }
    private void ToggleResponseExpand_Click(object sender, RoutedEventArgs e) { IsResponseExpanded = !IsResponseExpanded; }
    private void OnJsonEdited(object sender, JsonEditEventArgs e) { JsonEdited?.Invoke(this, e); }
    #endregion

    #region Export Context Menu Handlers
    private ExportViewModel _lastRightTappedExportViewModel = null;

    private void ExportsListView_ItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        _lastRightTappedExportViewModel = (e.OriginalSource as FrameworkElement)?.DataContext as ExportViewModel;

        try
        {
            ExportsMenu.XamlRoot = (sender as UIElement)?.XamlRoot ?? this.XamlRoot;
            if (ExportsMenu.XamlRoot != null)
            {
                ExportsMenu.ShowAt(sender as UIElement, e.GetPosition(sender as UIElement));
            }
            else
            {
                Debug.WriteLine("Error: Cannot show flyout, XamlRoot is null.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing flyout in ItemRightTapped: {ex}");
        }
        e.Handled = true;
    }

    private void ExportsBorder_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var sourceDataContext = (e.OriginalSource as FrameworkElement)?.DataContext;
        if (sourceDataContext == null || ReferenceEquals(e.OriginalSource, sender))
        {
            _lastRightTappedExportViewModel = null;
        }
        try
        {
            ExportsMenu.XamlRoot = (sender as UIElement)?.XamlRoot ?? this.XamlRoot;
            if (ExportsMenu.XamlRoot != null)
            {
                ExportsMenu.ShowAt(sender as UIElement, e.GetPosition(sender as UIElement));
            }
            else
            {
                Debug.WriteLine("Error: Cannot show flyout, XamlRoot is null.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing flyout in BorderRightTapped: {ex}");
        }
        e.Handled = true;
    }

    private void ExportsContextMenu_Opening(object sender, object e)
    {
        var tappedViewModel = _lastRightTappedExportViewModel;

        AddExportMenuItem.Tag = null;
        EditExportMenuItem.Tag = tappedViewModel;
        DeleteExportMenuItem.Tag = tappedViewModel;
        EditExportMenuItem.IsEnabled = tappedViewModel != null;
        DeleteExportMenuItem.IsEnabled = tappedViewModel != null;

        _lastRightTappedExportViewModel = null;
    }

    private async void AddExport_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox
        {
            PlaceholderText = "Export Name (e.g., userId)"
        };
        var pathBox = new TextBox
        {
            PlaceholderText = "JSON Path (e.g., $.Response.Body.id)"
        };
        var regexBox = new TextBox
        {
            PlaceholderText = "Regex Filter (Optional, e.g., \\d+)"
        };
        var stackPanel = new StackPanel
        {
            Spacing = 10
        };
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Name:",
            FontWeight = FontWeights.SemiBold
        });
        stackPanel.Children.Add(nameBox);
        stackPanel.Children.Add(new TextBlock
        {
            Text = "JSON Path:",
            FontWeight = FontWeights.SemiBold
        });
        stackPanel.Children.Add(pathBox);
        stackPanel.Children.Add(new TextBlock
        {
            Text = "Regex Filter (Optional):",
            FontWeight = FontWeights.SemiBold
        });
        stackPanel.Children.Add(regexBox);
        var dialog = new ContentDialog
        {
            Title = "Add Export Variable",
            Content = stackPanel,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };
        try
        {
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string name = nameBox.Text.Trim();
                string path = pathBox.Text.Trim();
                string regex = regexBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
                {
                    Debug.WriteLine("Export name and path cannot be empty."); return;
                }
                if (ObservableExports.Any(vm => vm.Key.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    Debug.WriteLine($"Export name '{name}' already exists for this element."); return;
                }
                var newExportKvp = new KeyValuePair<string, Export>(name, new Export(path, regex));
                var newVm = new ExportViewModel(newExportKvp); ObservableExports.Add(newVm);
                var sortedList = ObservableExports.OrderBy(vm => vm.Key).ToList();
                ObservableExports.Clear();
                sortedList.ForEach(item => ObservableExports.Add(item));
                NotifyPropertyChanged(nameof(ExportsVisibility));
                ExportAdded?.Invoke(this, new ExportChangedEventArgs(name, path, regex));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing add export dialog: {ex.Message}");
        }
    }
    private async void EditExport_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ExportViewModel vm)
        {
            string originalName = vm.Key;
            string currentPath = vm.JsonPath;
            string currentRegex = vm.Regex;
            var nameBox = new TextBox
            {
                Text = originalName
            };
            var pathBox = new TextBox
            {
                Text = currentPath
            };
            var regexBox = new TextBox
            {
                Text = currentRegex
            };
            var stackPanel = new StackPanel
            {
                Spacing = 10
            };
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Name:",
                FontWeight = FontWeights.SemiBold
            });
            stackPanel.Children.Add(nameBox);
            stackPanel.Children.Add(new TextBlock
            {
                Text = "JSON Path:",
                FontWeight = FontWeights.SemiBold
            });
            stackPanel.Children.Add(pathBox);
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Regex Filter (Optional):",
                FontWeight = FontWeights.SemiBold
            });
            stackPanel.Children.Add(regexBox);
            var dialog = new ContentDialog
            {
                Title = "Edit Export Variable",
                Content = stackPanel,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };
            try
            {
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    string newName = nameBox.Text.Trim();
                    string newPath = pathBox.Text.Trim();
                    string newRegex = regexBox.Text.Trim();
                    if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(newPath))
                    {
                        Debug.WriteLine("Export name and path cannot be empty."); return;
                    }
                    if (!newName.Equals(originalName, StringComparison.OrdinalIgnoreCase) && ObservableExports.Any(item => item.Key.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                    {
                        Debug.WriteLine($"Export name '{newName}' already exists for this element."); return;
                    }
                    var itemToUpdate = ObservableExports.FirstOrDefault(item => item.Key.Equals(originalName, StringComparison.OrdinalIgnoreCase));
                    if (itemToUpdate != null)
                    {
                        ObservableExports.Remove(itemToUpdate);
                        var updatedKvp = new KeyValuePair<string, Export>(newName, new Export(newPath, newRegex));
                        var updatedVm = new ExportViewModel(updatedKvp); ObservableExports.Add(updatedVm);
                        var sortedList = ObservableExports.OrderBy(item => item.Key).ToList();
                        ObservableExports.Clear();
                        sortedList.ForEach(item => ObservableExports.Add(item));
                    }
                    ExportEdited?.Invoke(this, new ExportChangedEventArgs(originalName, newName, newPath, newRegex));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing edit export dialog: {ex.Message}");
            }
        }
    }
    private void DeleteExport_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ExportViewModel vm)
        {
            var itemToRemove = ObservableExports.FirstOrDefault(item => item.Key.Equals(vm.Key, StringComparison.OrdinalIgnoreCase));
            if (itemToRemove != null)
            {
                ObservableExports.Remove(itemToRemove);
                NotifyPropertyChanged(nameof(ExportsVisibility));
                ExportDeleted?.Invoke(this, new ExportChangedEventArgs(vm.Key));
            }
        }
    }
    public void RefreshExports() { UpdateObservableExports(); }
    #endregion

    #region Status/Color Helpers (Unchanged)
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
        }; return IsCurrentElement ? GetMutedColor(color) : color;
    }
    private Color GetMutedColor(Color color)
    {
        return Color.FromArgb((byte)(color.A * 0.5), (byte)(color.R * 1), (byte)(color.G * 1), (byte)(color.B * 1));
    }
    #endregion
}