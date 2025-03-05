using System.Collections.ObjectModel;
using EndpointExplorer.Controls;
using Windows.Foundation;
using Windows.Storage.Pickers;

namespace EndpointExplorer;

public sealed partial class MainPage : Page
{
    public ObservableCollection<InteractiveJsonViewer> FlowElements { get; } = new ObservableCollection<InteractiveJsonViewer>();
    private StorageFile CurrentFlowFile = null; // need to display somewhere

    public MainPage()
    {
        this.InitializeComponent();
        HorizontalItems.ItemsSource = FlowElements;
        FlowElementScrollPanel.ViewChanged += (s, e) =>
        {
            var scrollViewer = s as ScrollViewer;
            var viewportWidth = scrollViewer.ViewportWidth;
            var horizontalOffset = scrollViewer.HorizontalOffset;
            var extendedViewportStart = horizontalOffset - (viewportWidth * 0.5);
            var extendedViewportEnd = horizontalOffset + (viewportWidth * 1.5);

            foreach (var element in FlowElements)
            {
                var elementPosition = FlowElements.IndexOf(element) * 230;
                element.IsInView = elementPosition >= extendedViewportStart && elementPosition <= extendedViewportEnd;
            }
        };
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

        LoadFlowElementsIncrementally();
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
        LoadFlowElementsIncrementally();
    }

    private void LoadFlowElementsIncrementally()
    {
        if (Flow == null) return;

        // Clear existing elements
        FlowElements.Clear();

        // Get the dispatcher
        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        foreach (var elem in Flow.FlowElements)
        {
            FlowElements.Add(new InteractiveJsonViewer() { });
        }

        dispatcherQueue.TryEnqueue(async () =>
        {
            for (int i = 0; i < Flow.FlowElements.Count; i++)
            {
                var elem = Flow.FlowElements[i];
                var viewer = FlowElements[i];
                viewer.FlowElement = elem;
                await Task.Delay(500);
            }
        });
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

    #region Empty Methods
    private void OnPlay(object sender, RoutedEventArgs e) { }
    private void OnRestart(object sender, RoutedEventArgs e) { }
    private void OnStepForward(object sender, RoutedEventArgs e) { }
    private void OnStop(object sender, RoutedEventArgs e) { }
    #endregion
}