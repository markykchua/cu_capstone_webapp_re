using System.Collections.ObjectModel;
using System.Drawing;
using System.Threading.Tasks;
using EndpointExplorer.Controls;
using Windows.Foundation;
using Windows.Storage.Pickers;

namespace EndpointExplorer;

public sealed partial class MainPage : Page
{
    public ObservableCollection<FlowElementViewer> FlowElements { get; } = new ObservableCollection<FlowElementViewer>();
    public ObservableCollection<LogMessage> LogMessages { get; } = new ObservableCollection<LogMessage>();

    FlowElementViewer CurrentElement => FlowElements.FirstOrDefault(e => e.IsCurrentElement);
    private StorageFile CurrentFlowFile = null; // need to display somewhere

    // Pagination properties
    private int _currentPage = 0;
    private int _elementsPerPage = 5;
    private ObservableCollection<FlowElementViewer> _displayedElements = new ObservableCollection<FlowElementViewer>();
    private Orchestrator _orchestrator;

    public MainPage()
    {
        this.InitializeComponent();
        // Change this line to use the new paged control
        PagedItems.ItemsSource = _displayedElements;
        UpdatePaginationUI();
    }

    public Thickness PanelMargin { get; private set; }
    public Symbol PanelIcon { get; private set; }
    private UserFlow _flow;
    public UserFlow Flow
    {
        get => _flow;
        private set
        {
            _flow = value;
            Flow.FindRelations();
            _orchestrator = new Orchestrator(Flow);
        }
    }

    private void TogglePane(object sender, RoutedEventArgs e)
    {
        LeftPanel.IsPaneOpen = !LeftPanel.IsPaneOpen;
        ToggleButtonIcon.Symbol = LeftPanel.IsPaneOpen ? Symbol.Back : Symbol.Forward;
    }

    private async void OnImportFromHAR(object sender, RoutedEventArgs e)
    {
        (StorageFile _, string fileContents) = await GetFromFilePicker(".har");
        if (string.IsNullOrEmpty(fileContents))
        {
            return;
        }
        CurrentFlowFile = null;
        JObject har = JToken.Parse(fileContents) as JObject;
        Flow = UserFlow.FromHar(har);

        LoadFlowElements();
        Log("Flow imported from HAR file", LogType.Success);
    }

    private async void OnLoadFlow(object sender, RoutedEventArgs e)
    {
        (StorageFile file, string fileContents) = await GetFromFilePicker(".json");
        if (string.IsNullOrEmpty(fileContents))
        {
            return;
        }
        CurrentFlowFile = file;
        JObject loaded = JToken.Parse(fileContents) as JObject;
        Flow = loaded.ToObject<UserFlow>();
        LoadFlowElements();
        Log("Flow loaded", LogType.Success);
    }

    private void LoadFlowElements()
    {
        if (Flow == null) return;

        // Clear existing elements
        FlowElements.Clear();

        // Reset pagination
        _currentPage = 0;

        for (int i = 0; i < Flow.FlowElements.Count; i++)
        {
            var elem = Flow.FlowElements[i];
            var viewer = new FlowElementViewer();
            viewer.FlowElement = elem;
            viewer.JsonEdited += OnFlowElementEdited;
            FlowElements.Add(viewer);
        }
        UpdateDisplayedElements();
    }

    private async Task<(StorageFile, string)> GetFromFilePicker(string fileExtension)
    {
        var fileOpenPicker = new FileOpenPicker();
        fileOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileOpenPicker.FileTypeFilter.Add(fileExtension);

        // For Uno.WinUI-based apps
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileOpenPicker, hwnd);

        var pickedFile = await fileOpenPicker.PickSingleFileAsync();
        if (pickedFile != null)
        {
            return (pickedFile, await FileIO.ReadTextAsync(pickedFile));
        }
        return (null, null);
    }

    private async Task OnSaveFlow(object sender, RoutedEventArgs e)
    {
        if (Flow is null)
        {
            Log("No flow to save", LogType.Error);
            return;
        }
        if (CurrentFlowFile is null)
        {
            OnSaveFlowAs(sender, e);
        }
        else
        {
            CachedFileManager.DeferUpdates(CurrentFlowFile);
            await FileIO.WriteTextAsync(CurrentFlowFile, JToken.FromObject(Flow).ToString());
            await CachedFileManager.CompleteUpdatesAsync(CurrentFlowFile);

            Log($"Flow saved in {CurrentFlowFile.Name}", LogType.Success);
        }
    }

    private async void OnSaveFlowAs(object sender, RoutedEventArgs e)
    {
        if (Flow is null)
        {
            Log("No flow to save", LogType.Error);
            return;
        }
        var fileSavePicker = new FileSavePicker();
        fileSavePicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        fileSavePicker.SuggestedFileName = "flow.json";
        fileSavePicker.FileTypeChoices.Add("json", new List<string>() { ".json" });
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileSavePicker, hwnd);
        StorageFile saveFile = await fileSavePicker.PickSaveFileAsync();
        if (saveFile != null)
        {
            CachedFileManager.DeferUpdates(saveFile);
            await FileIO.WriteTextAsync(saveFile, JToken.FromObject(Flow).ToString());
            await CachedFileManager.CompleteUpdatesAsync(saveFile);
            CurrentFlowFile = saveFile;
        }
        Log($"Flow saved as {CurrentFlowFile.Name}", LogType.Success);
    }

    public void Log(string message, LogType logType)
    {
        SolidColorBrush color = logType switch
        {
            LogType.Info => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
            LogType.Success => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0)),
            LogType.Warning => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0)),
            LogType.Error => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0)),
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)),
        };

        Log(message, color);
    }
    public void Log(string message, SolidColorBrush color)
    {
        LogMessages.Add(new LogMessage { Text = message, Color = color });

        LogScrollViewer.ChangeView(null, LogScrollViewer.ExtentHeight, null);
    }

    public enum LogType
    {
        Info,
        Success,
        Warning,
        Error
    }

    #region Pagination Methods

    private void OnFlowElementEdited(object sender, JsonEditEventArgs e)
    {
        if (sender is FlowElementViewer viewer)
        {
            int index = FlowElements.IndexOf(viewer);
            JObject updated = FlowElements[index].FlowElement.Value;
            updated.SelectToken(e.Path).Replace(e.NewValue);
            if (e.Path.StartsWith("request", StringComparison.OrdinalIgnoreCase))
            {
                FlowElements[index].FlowElement.UpdateRequest(updated["Request"] as JObject);
                FlowElements[index] = new FlowElementViewer { FlowElement = FlowElements[index].FlowElement, IsRequestExpanded = viewer.IsRequestExpanded, IsResponseExpanded = viewer.IsResponseExpanded };
                FlowElements[index].JsonEdited += OnFlowElementEdited;
                UpdateSingleDisplayedElement(FlowElements[index]);
            }
            else
            {
                Console.WriteLine("Why are you trying to edit this?");
            }
            Log($"Edited {e.Path} to {e.NewValue}", LogType.Info);
        }
    }

    private void OnNextPage(object sender, RoutedEventArgs e)
    {
        if (FlowElements.Count == 0) return;

        int totalPages = (int)Math.Ceiling((double)FlowElements.Count / _elementsPerPage);
        if (_currentPage < totalPages - 1)
        {
            _currentPage++;
            UpdateDisplayedElements();
        }
    }

    private void OnPreviousPage(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 0)
        {
            _currentPage--;
            UpdateDisplayedElements();
        }
    }

    private void UpdateDisplayedElements()
    {
        _displayedElements.Clear();
        int startIndex = _currentPage * _elementsPerPage;
        int count = Math.Min(_elementsPerPage, FlowElements.Count - startIndex);
        _displayedElements.AddRange(FlowElements.Skip(startIndex).Take(count));
        UpdatePaginationUI();
    }

    private void UpdateSingleDisplayedElement(FlowElementViewer element)
    {
        int index = FlowElements.IndexOf(element);
        if (index >= _currentPage * _elementsPerPage && index < (_currentPage + 1) * _elementsPerPage)
        {
            _displayedElements[index - _currentPage * _elementsPerPage] = element;
        }
    }

    private void UpdatePaginationUI()
    {
        int totalPages = FlowElements.Count > 0
            ? (int)Math.Ceiling((double)FlowElements.Count / _elementsPerPage)
            : 1;

        // Update page indicator
        PageIndicator.Text = $"Page {_currentPage + 1} of {totalPages}";

        // Update button states
        PrevPageButton.IsEnabled = _currentPage > 0;
        NextPageButton.IsEnabled = _currentPage < totalPages - 1;
    }

    #endregion

    #region Empty Methods
    private void OnPlay(object sender, RoutedEventArgs e)
    {
        if (Flow is null)
        {
            Log("No flow to play, please load flow before starting playback", LogType.Warning);
            return;
        }
        FlowElements.First().IsCurrentElement = true;
        Log("Playback started", LogType.Info);
    }
    private void OnRestart(object sender, RoutedEventArgs e)
    {
        OnStop(sender, e);
        OnPlay(sender, e);
    }
    private async Task OnStepForward(object sender, RoutedEventArgs e)
    {
        if (CurrentElement is null)
        {
            Log("No current element to step forward from", LogType.Warning);
            return;
        }
        int currentIndex = FlowElements.IndexOf(CurrentElement);
        var replayed = await _orchestrator.PlayNext();
        Log($"Replayed {replayed.Request.Method} {replayed.Request.Url} and got response with status {replayed.Response.Status}", replayed.Response.Status switch
        {
            >= 500 => LogType.Warning,
            >= 400 => LogType.Error,
            >= 300 => LogType.Warning,
            >= 200 => LogType.Success,
            _ => LogType.Info
        });
        FlowElements[currentIndex] = new FlowElementViewer { FlowElement = replayed, IsRequestExpanded = CurrentElement.IsRequestExpanded, IsResponseExpanded = CurrentElement.IsResponseExpanded };
        Flow.FlowElements[currentIndex] = replayed;
        if (currentIndex < FlowElements.Count - 1)
        {
            FlowElements[currentIndex + 1].IsCurrentElement = true;
            UpdateSingleDisplayedElement(FlowElements[currentIndex]);
        }
        if (currentIndex < FlowElements.Count && (currentIndex + 1) % _elementsPerPage == 0)
        {
            OnNextPage(sender, e);
        }
    }
    private void OnStop(object sender, RoutedEventArgs e)
    {
        Log("Playback stopped", LogType.Info);
        _currentPage = 0;
        FlowElements.ForEach(e => e.IsCurrentElement = false);
        UpdateDisplayedElements();
    }
    #endregion
}