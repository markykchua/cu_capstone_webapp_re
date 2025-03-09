using System.Collections.ObjectModel;
using EndpointExplorer.Controls;
using Windows.Foundation;
using Windows.Storage.Pickers;

namespace EndpointExplorer;

public sealed partial class MainPage : Page
{
    public ObservableCollection<FlowElementViewer> FlowElements { get; } = new ObservableCollection<FlowElementViewer>();
    private StorageFile CurrentFlowFile = null; // need to display somewhere

    // Pagination properties
    private int _currentPage = 0;
    private int _elementsPerPage = 5;
    private ObservableCollection<FlowElementViewer> _displayedElements = new ObservableCollection<FlowElementViewer>();

    public MainPage()
    {
        this.InitializeComponent();
        // Change this line to use the new paged control
        PagedItems.ItemsSource = _displayedElements;
        UpdatePaginationUI();
    }

    public Thickness PanelMargin { get; private set; }
    public Symbol PanelIcon { get; private set; }
    public UserFlow Flow = null;

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
    }

    private void LoadFlowElements()
    {
        if (Flow == null) return;
        Console.WriteLine($"Elements per page: {_elementsPerPage}");

        // Clear existing elements
        FlowElements.Clear();

        // Reset pagination
        _currentPage = 0;

        for (int i = 0; i < Flow.FlowElements.Count; i++)
        {
            var elem = Flow.FlowElements[i];
            var viewer = new FlowElementViewer();
            viewer.FlowElement = elem;
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
            //popup error? tbd
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
        }
    }

    private async void OnSaveFlowAs(object sender, RoutedEventArgs e)
    {
        if (Flow is null)
        {
            //popup error? tbd
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
    }

    #region Pagination Methods

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
        // Clear current display
        _displayedElements.Clear();

        // Calculate elements to display for current page
        int startIndex = _currentPage * _elementsPerPage;
        int count = Math.Min(_elementsPerPage, FlowElements.Count - startIndex);

        // Add elements for current page
        _displayedElements.AddRange(FlowElements.Skip(startIndex).Take(count));

        // Update UI
        UpdatePaginationUI();
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
    private void OnPlay(object sender, RoutedEventArgs e) { }
    private void OnRestart(object sender, RoutedEventArgs e) { }
    private void OnStepForward(object sender, RoutedEventArgs e) { }
    private void OnStop(object sender, RoutedEventArgs e) { }
    #endregion
}