using System.Collections.ObjectModel;
using EndpointExplorer.Controls;
using Windows.Storage.Pickers;
using Newtonsoft.Json;
using Windows.Storage.Provider;

namespace EndpointExplorer;

public sealed partial class MainPage : Page
{
    public ObservableCollection<FlowElementViewer> FlowElements { get; } = new ObservableCollection<FlowElementViewer>();
    public ObservableCollection<LogMessage> LogMessages { get; } = new ObservableCollection<LogMessage>();

    FlowElementViewer CurrentElement => FlowElements.FirstOrDefault(e => e.IsCurrentElement);
    private StorageFile CurrentFlowFile = null;

    // pagination
    private int _currentPage = 0;
    private int _elementsPerPage = 5;
    private ObservableCollection<FlowElementViewer> _displayedElements = new ObservableCollection<FlowElementViewer>();
    private Orchestrator _orchestrator;
    private UserFlow _originalFlow; // stores loaded/saved state

    public MainPage()
    {
        this.InitializeComponent();
        PagedItems.ItemsSource = _displayedElements;
        UpdatePaginationUI();
    }

    private UserFlow _flow; // working copy
    public UserFlow Flow
    {
        get => _flow;
        private set
        {
            _flow = value; // set working copy
            if (_flow != null)
            {
                _orchestrator = new Orchestrator(_flow);
            }
            else
            {
                _orchestrator = null;
            }
        }
    }

    private UserFlow DeepCloneFlow(UserFlow sourceFlow)
    {
        if (sourceFlow == null) return null;
        try
        {
            return JToken.FromObject(sourceFlow).DeepClone().ToObject<UserFlow>();
        }
        catch (Exception ex)
        {
            Log($"Error during flow deep copy: {ex.Message}", LogType.Error);
            return null;
        }
    }
    private void TogglePane(object sender, RoutedEventArgs e)
    {
        LeftPanel.IsPaneOpen = !LeftPanel.IsPaneOpen;
        ToggleButtonIcon.Symbol = LeftPanel.IsPaneOpen ? Symbol.Back : Symbol.Forward;
    }

    private async void OnImportFromHAR(object sender, RoutedEventArgs e)
    {
        (StorageFile file, string fileContents) = await GetFromFilePicker(".har");
        if (string.IsNullOrEmpty(fileContents))
        {
            return;
        }
        CurrentFlowFile = null; // imported but not saved
        try
        {
            JObject har = JToken.Parse(fileContents) as JObject;
            _originalFlow = UserFlow.FromHar(har);
            _originalFlow.FindRelations();

            Flow = DeepCloneFlow(_originalFlow);

            if (Flow != null)
            {
                LoadFlowElements();
                Log($"Flow imported from HAR file: {file.Name}", LogType.Success);
            }
            else
            {
                Log("Failed to create working copy of flow from HAR.", LogType.Error);
                _originalFlow = null;
            }
        }
        catch (Exception ex)
        {
            Log($"Error importing HAR: {ex.Message}", LogType.Error);
            _originalFlow = null;
            Flow = null;
            FlowElements.Clear();
            _displayedElements.Clear();
            UpdatePaginationUI();
        }
    }

    private async void OnLoadFlow(object sender, RoutedEventArgs e)
    {
        (StorageFile file, string fileContents) = await GetFromFilePicker(".json");
        if (string.IsNullOrEmpty(fileContents))
        {
            return;
        }

        try
        {
            JObject loaded = JToken.Parse(fileContents) as JObject;
            _originalFlow = loaded.ToObject<UserFlow>();
            _originalFlow.FindRelations();

            // create working copy
            Flow = DeepCloneFlow(_originalFlow);

            if (Flow != null)
            {
                CurrentFlowFile = file;
                LoadFlowElements();
                Log($"Flow loaded from {file.Name}", LogType.Success);
            }
            else
            {
                Log("Failed to create working copy of loaded flow.", LogType.Error);
                _originalFlow = null;
                CurrentFlowFile = null;
            }
        }
        catch (Exception ex)
        {
            Log($"Error loading flow: {ex.Message}", LogType.Error);
            _originalFlow = null;
            Flow = null;
            CurrentFlowFile = null;
            FlowElements.Clear();
            _displayedElements.Clear();
            UpdatePaginationUI();
        }
    }

    private void LoadFlowElements()
    {
        if (Flow == null) return;

        FlowElements.Clear();
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

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileOpenPicker, hwnd);

        var pickedFile = await fileOpenPicker.PickSingleFileAsync();
        if (pickedFile != null)
        {
            try
            {
                return (pickedFile, await FileIO.ReadTextAsync(pickedFile));
            }
            catch (Exception ex)
            {
                Log($"Error reading file {pickedFile.Name}: {ex.Message}", LogType.Error);
                return (null, null);
            }
        }
        return (null, null);
    }
    private async Task SaveFlowToFile(StorageFile file)
    {
        if (Flow is null)
        {
            Log("No flow data to save.", LogType.Error);
            return;
        }
        try
        {
            string flowJson = JToken.FromObject(Flow).ToString(Formatting.Indented);

            CachedFileManager.DeferUpdates(file);
            await FileIO.WriteTextAsync(file, flowJson);
            FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);

            if (status == FileUpdateStatus.Complete)
            {
                _originalFlow = DeepCloneFlow(Flow);
                CurrentFlowFile = file;
                Log($"Flow saved in {file.Name}", LogType.Success);
            }
            else
            {
                Log($"Failed to save flow to {file.Name}. Status: {status}", LogType.Error);
            }
        }
        catch (Exception ex)
        {
            Log($"Error saving flow to {file.Name}: {ex.Message}", LogType.Error);
        }
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
            await OnSaveFlowAs(sender, e);
        }
        else
        {
            await SaveFlowToFile(CurrentFlowFile);
        }
    }


    private async Task OnSaveFlowAs(object sender, RoutedEventArgs e)
    {
        if (Flow is null)
        {
            Log("No flow to save", LogType.Error);
            return;
        }
        var fileSavePicker = new FileSavePicker();
        fileSavePicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        fileSavePicker.SuggestedFileName = "flow.json";
        fileSavePicker.FileTypeChoices.Add("JSON Flow File", new List<string>() { ".json" });
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileSavePicker, hwnd);
        StorageFile saveFile = await fileSavePicker.PickSaveFileAsync();
        if (saveFile != null)
        {
            await SaveFlowToFile(saveFile);
        }
    }

    public void Log(string message, LogType logType)
    {
        SolidColorBrush color = logType switch
        {
            LogType.Info => new SolidColorBrush(Microsoft.UI.Colors.Gray),
            LogType.Success => new SolidColorBrush(Microsoft.UI.Colors.LimeGreen),
            LogType.Warning => new SolidColorBrush(Microsoft.UI.Colors.Orange),
            LogType.Error => new SolidColorBrush(Microsoft.UI.Colors.Red),
            _ => new SolidColorBrush(Microsoft.UI.Colors.Gray),
        };
        Log(message, color);
    }
    public void Log(string message, SolidColorBrush color)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            LogMessages.Add(new LogMessage { Text = message, Color = color });
            LogScrollViewer.UpdateLayout();
            LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
        });
    }
    public enum LogType { Info, Success, Warning, Error }

    #region Pagination Methods

    private void OnFlowElementEdited(object sender, JsonEditEventArgs e)
    {
        if (sender is FlowElementViewer viewer && viewer.FlowElement != null)
        {
            int flowIndex = Flow.FlowElements.IndexOf(viewer.FlowElement);
            if (flowIndex < 0)
            {
                Log($"Error: Edited element not found in the current flow.", LogType.Error);
                return;
            }

            try
            {
                JObject elementValue = Flow.FlowElements[flowIndex].Value;
                JToken targetToken = elementValue?.SelectToken(e.Path);

                if (targetToken != null)
                {
                    targetToken.Replace(e.NewValue);

                    if (e.Path.StartsWith("Request", StringComparison.OrdinalIgnoreCase))
                    {
                        Flow.FlowElements[flowIndex].UpdateRequest(elementValue["Request"] as JObject);

                        int viewerIndex = FlowElements.IndexOf(viewer);
                        if (viewerIndex >= 0)
                        {
                            var newViewer = new FlowElementViewer
                            {
                                FlowElement = Flow.FlowElements[flowIndex],
                                IsRequestExpanded = viewer.IsRequestExpanded,
                                IsResponseExpanded = viewer.IsResponseExpanded,
                                IsCurrentElement = viewer.IsCurrentElement
                            };
                            newViewer.JsonEdited += OnFlowElementEdited;

                            FlowElements[viewerIndex] = newViewer;

                            UpdateSingleDisplayedElement(newViewer, viewer);
                        }
                    }
                    else
                    {
                        // not supporting response editing currently - not sure if needed
                    }
                }
                else
                {
                    Log($"Edit failed: Path '{e.Path}' not found in element {flowIndex + 1}.", LogType.Error);
                }
            }
            catch (Exception ex)
            {
                Log($"Error applying edit to element {flowIndex + 1}: {ex.Message}", LogType.Error);
            }
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
        if (FlowElements.Count > 0)
        {
            int startIndex = _currentPage * _elementsPerPage;
            if (startIndex < FlowElements.Count)
            {
                int count = Math.Min(_elementsPerPage, FlowElements.Count - startIndex);
                for (int i = 0; i < count; i++)
                {
                    _displayedElements.Add(FlowElements[startIndex + i]);
                }
            }
            else if (_currentPage > 0)
            {
                _currentPage--;
                UpdateDisplayedElements();
                return;
            }
        }
        UpdatePaginationUI();
    }

    private void UpdateSingleDisplayedElement(FlowElementViewer newViewer, FlowElementViewer oldViewer)
    {
        // find index of old viewer in the displayed list
        int indexInDisplayed = -1;
        for (int i = 0; i < _displayedElements.Count; ++i)
        {
            if (ReferenceEquals(_displayedElements[i], oldViewer))
            {
                indexInDisplayed = i;
                break;
            }
        }

        if (indexInDisplayed != -1)
        {
            // replace old viewer
            _displayedElements[indexInDisplayed] = newViewer;
            Console.WriteLine($"Replaced viewer at displayed index {indexInDisplayed}.");
        }
        else
        {
            // probably won't happen but just in case
            int indexInFlowElements = FlowElements.IndexOf(newViewer);

            if (indexInFlowElements >= 0 && indexInFlowElements >= _currentPage * _elementsPerPage && indexInFlowElements < (_currentPage + 1) * _elementsPerPage)
            {
                Console.WriteLine($"Viewer should be visible (index {indexInFlowElements}), refreshing displayed elements.");
                UpdateDisplayedElements();
            }
            else
            {
                Console.WriteLine($"Updated viewer (index {indexInFlowElements}) is not on the current page ({_currentPage}).");
            }
        }
    }

    private void UpdatePaginationUI()
    {
        int totalPages = FlowElements.Count > 0
            ? (int)Math.Ceiling((double)FlowElements.Count / _elementsPerPage)
            : 1;
        PageIndicator.Text = $"Page {_currentPage + 1} of {totalPages}";
        PrevPageButton.IsEnabled = _currentPage > 0;
        NextPageButton.IsEnabled = _currentPage < totalPages - 1;
    }

    #endregion

    #region Playback Methods

    private void ResetFlowToOriginal()
    {
        Log("Resetting flow to original state...", LogType.Info);
        if (_originalFlow != null)
        {
            // create fresh working copy
            Flow = DeepCloneFlow(_originalFlow);
            if (Flow != null)
            {
                LoadFlowElements();
            }
            else
            {
                Log("Failed to restore flow from original state.", LogType.Error);
                FlowElements.Clear();
                _displayedElements.Clear();
                UpdatePaginationUI();
            }
        }
        else
        {
            Log("No original flow state found to reset to.", LogType.Info);
            Flow = null;
            FlowElements.Clear();
            _displayedElements.Clear();
            UpdatePaginationUI();
        }
        FlowElements.ForEach(fev => fev.IsCurrentElement = false);
        _currentPage = 0;
        UpdateDisplayedElements();
    }

    private void OnPlay(object sender, RoutedEventArgs e)
    {
        if (Flow is null)
        {
            Log("No flow to play. Please load or import a flow.", LogType.Warning);
            return;
        }
        if (FlowElements.Any())
        {
            FlowElements.First().IsCurrentElement = true;
            _currentPage = 0;
            UpdateDisplayedElements();
            Log("Playback started", LogType.Info);
        }
        else
        {
            Log("Flow has no elements to play.", LogType.Info);
        }
    }

    private void OnRestart(object sender, RoutedEventArgs e)
    {
        Log("Restarting flow...", LogType.Info);
        ResetFlowToOriginal();
        OnPlay(sender, e);
    }
    private async Task OnStepForward(object sender, RoutedEventArgs e)
    {
        var currentViewer = CurrentElement;
        if (currentViewer is null)
        {
            if (Flow != null && FlowElements.Any() && _orchestrator != null && _orchestrator.FlowElements.Any())
            {
                Log("No current element. Starting from the beginning.", LogType.Info);
                currentViewer = FlowElements.First();
                currentViewer.IsCurrentElement = true;
                if (_currentPage == 0) UpdateDisplayedElements();
            }
            else
            {
                Log("Cannot step forward: No current element or flow not ready.", LogType.Warning);
                return;
            }
        }

        if (_orchestrator == null || !_orchestrator.FlowElements.Any())
        {
            Log("Cannot step forward: Orchestrator not ready or no more steps.", LogType.Warning);
            if (currentViewer != null)
            {
                currentViewer.IsCurrentElement = false;
            }
            return;
        }

        int currentIndexInViewList = FlowElements.IndexOf(currentViewer);
        if (currentIndexInViewList < 0)
        {
            Log("Error: Current viewer not found in FlowElements list before replay.", LogType.Error);
            return;
        }
        var oldViewerInstance = currentViewer;


        try
        {
            Log($"Executing step {currentIndexInViewList + 1}: {currentViewer.FlowElement.Request.Method} {currentViewer.FlowElement.Request.Url}", LogType.Info);
            var replayedElement = await _orchestrator.PlayNext();

            Log($"Response Status: {replayedElement.Response.Status}", replayedElement.Response.Status switch
            {
                >= 500 => LogType.Error,
                >= 400 => LogType.Error,
                >= 300 => LogType.Warning,
                >= 200 => LogType.Success,
                _ => LogType.Info
            });

            var newViewerForReplayed = new FlowElementViewer
            {
                FlowElement = replayedElement,
                IsRequestExpanded = oldViewerInstance.IsRequestExpanded,
                IsResponseExpanded = oldViewerInstance.IsResponseExpanded,
                IsCurrentElement = false
            };
            newViewerForReplayed.JsonEdited += OnFlowElementEdited;

            FlowElements[currentIndexInViewList] = newViewerForReplayed;
            UpdateSingleDisplayedElement(newViewerForReplayed, oldViewerInstance);

            if (currentIndexInViewList < FlowElements.Count - 1)
            {
                var nextViewer = FlowElements[currentIndexInViewList + 1];
                nextViewer.IsCurrentElement = true;
                int nextIndexInDisplayed = -1;
                for (int i = 0; i < _displayedElements.Count; ++i)
                {
                    if (ReferenceEquals(_displayedElements[i], nextViewer))
                    {
                        nextIndexInDisplayed = i; break;
                    }
                }
                if (nextIndexInDisplayed != -1)
                {
                    UpdateDisplayedElements();
                }

                if ((currentIndexInViewList + 1) % _elementsPerPage == 0)
                {
                    OnNextPage(sender, e);
                }
            }
            else
            {
                Log("End of flow reached.", LogType.Info);
            }
        }
        catch (InvalidOperationException ex)
        {
            Log($"Playback error: {ex.Message}", LogType.Warning);
            if (oldViewerInstance != null) { oldViewerInstance.IsCurrentElement = false; }
        }
        catch (Exception ex)
        {
            Log($"Error during step execution: {ex.Message}", LogType.Error);
            if (oldViewerInstance != null) { oldViewerInstance.IsCurrentElement = false; }
        }
    }
    private void OnStop(object sender, RoutedEventArgs e)
    {
        ResetFlowToOriginal();
    }
    #endregion
}