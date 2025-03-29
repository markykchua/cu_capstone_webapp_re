using EndpointExplorer.Controls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Input;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;

namespace EndpointExplorer;

public sealed partial class MainPage : Page
{
    // bindings
    public ObservableCollection<FlowElementViewer> FlowElements { get; } = new ObservableCollection<FlowElementViewer>();
    public ObservableCollection<LogMessage> LogMessages { get; } = new ObservableCollection<LogMessage>();
    public ObservableCollection<ExternalVariableViewModel> ObservableExternalVariables { get; } = new ObservableCollection<ExternalVariableViewModel>();

    FlowElementViewer CurrentElement => FlowElements.FirstOrDefault(e => e.IsCurrentElement);
    private StorageFile CurrentFlowFile = null;
    private int _currentPage = 0;
    private int _elementsPerPage = 5;
    private ObservableCollection<FlowElementViewer> _displayedElements = new ObservableCollection<FlowElementViewer>();
    private Orchestrator _orchestrator;
    private UserFlow _originalFlow; // stores loaded/saved state
    private UserFlow _flow; // working copy

    public MainPage()
    {
        this.InitializeComponent();
        PagedItems.ItemsSource = _displayedElements;
        ExternalVariablesListView.ItemsSource = ObservableExternalVariables;
        UpdatePaginationUI();
    }

    public UserFlow Flow
    {
        get => _flow;
        private set
        {
            if (ReferenceEquals(_flow, value)) return;

            _flow = value;
            if (_flow != null)
            {
                _orchestrator = new Orchestrator(_flow);
                UpdateExternalVariablesView();
            }
            else
            {
                _orchestrator = null;
                UpdateExternalVariablesView();
            }
        }
    }

    // --- Deep Clone Helper ---
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
        if (string.IsNullOrEmpty(fileContents)) return;

        CurrentFlowFile = null;
        try
        {
            JObject har = JToken.Parse(fileContents) as JObject;
            _originalFlow = UserFlow.FromHar(har);
            if (_originalFlow == null) throw new InvalidOperationException("Failed to parse HAR content into UserFlow.");
            _originalFlow.FindRelations();

            Flow = DeepCloneFlow(_originalFlow); // create working copy

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
        if (string.IsNullOrEmpty(fileContents)) return;

        try
        {
            JObject loaded = JToken.Parse(fileContents) as JObject;
            _originalFlow = loaded.ToObject<UserFlow>();
            if (_originalFlow == null) throw new InvalidOperationException("Failed to parse JSON content into UserFlow.");
            _originalFlow.FindRelations();

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

    private async Task OnSaveFlow(object sender, RoutedEventArgs e)
    {
        if (Flow is null) { Log("No flow to save", LogType.Error); return; }
        if (CurrentFlowFile is null) { await OnSaveFlowAs(sender, e); }
        else { await SaveFlowToFile(CurrentFlowFile); }
    }

    private async Task OnSaveFlowAs(object sender, RoutedEventArgs e)
    {
        if (Flow is null) { Log("No flow to save", LogType.Error); return; }

        var fileSavePicker = new FileSavePicker();
        fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary; // Changed default location
        fileSavePicker.SuggestedFileName = "flow.json";
        fileSavePicker.FileTypeChoices.Add("JSON Flow File", new List<string>() { ".json" });

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileSavePicker, hwnd);

        StorageFile saveFile = await fileSavePicker.PickSaveFileAsync();
        if (saveFile != null) { await SaveFlowToFile(saveFile); }
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
        UpdateExternalVariablesView();
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
            try { return (pickedFile, await FileIO.ReadTextAsync(pickedFile)); }
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
        if (Flow is null) { Log("No flow data to save.", LogType.Error); return; }

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
                UpdateExternalVariablesView();
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

    // --- Logging ---
    public enum LogType { Info, Success, Warning, Error }
    public void Log(string message, LogType logType)
    {
        // Use Microsoft.UI.Colors for WinUI 3
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
                        // not handled
                        Log($"Edited {e.Path} to {e.NewValue}", LogType.Info);
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
            _displayedElements[indexInDisplayed] = newViewer;
            Debug.WriteLine($"Replaced viewer at displayed index {indexInDisplayed}.");
        }
        else
        {
            int indexInFlowElements = FlowElements.IndexOf(newViewer);
            if (indexInFlowElements >= 0 && indexInFlowElements >= _currentPage * _elementsPerPage && indexInFlowElements < (_currentPage + 1) * _elementsPerPage)
            {
                Debug.WriteLine($"Viewer should be visible (index {indexInFlowElements}), refreshing displayed elements.");
                UpdateDisplayedElements();
            }
            else
            {
                Debug.WriteLine($"Updated viewer (index {indexInFlowElements}) is not on the current page ({_currentPage}).");
            }
        }
    }

    private void UpdatePaginationUI()
    {
        int totalPages = FlowElements.Count > 0
            ? (int)Math.Ceiling((double)FlowElements.Count / _elementsPerPage)
            : 1;
        if (PageIndicator != null) PageIndicator.Text = $"Page {_currentPage + 1} of {totalPages}";
        if (PrevPageButton != null) PrevPageButton.IsEnabled = _currentPage > 0;
        if (NextPageButton != null) NextPageButton.IsEnabled = _currentPage < totalPages - 1;
    }

    #endregion

    #region Playback Methods

    private void ResetFlowToOriginal()
    {
        Log("Resetting flow to original state...", LogType.Info);
        if (_originalFlow != null)
        {
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
                ObservableExternalVariables.Clear();
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
        if (Flow is null) { Log("No flow to play. Please load or import a flow.", LogType.Warning); return; }

        if (FlowElements.Any())
        {
            FlowElements.ForEach(fev => fev.IsCurrentElement = false);

            FlowElements.First().IsCurrentElement = true;
            _currentPage = 0;
            UpdateDisplayedElements();
            Log("Playback started", LogType.Info);
        }
        else { Log("Flow has no elements to play.", LogType.Info); }
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
            else { Log("Cannot step forward: No current element or flow not ready.", LogType.Warning); return; }
        }

        if (_orchestrator == null || !_orchestrator.FlowElements.Any())
        {
            Log("Cannot step forward: Orchestrator not ready or no more steps.", LogType.Warning);
            if (currentViewer != null) currentViewer.IsCurrentElement = false;
            return;
        }

        int currentIndexInViewList = FlowElements.IndexOf(currentViewer);
        if (currentIndexInViewList < 0) { Log("Error: Current viewer not found in FlowElements list before replay.", LogType.Error); return; }
        var oldViewerInstance = currentViewer;

        try
        {
            Log($"Executing step {currentIndexInViewList + 1}: {currentViewer.FlowElement.Request.Method} {currentViewer.FlowElement.Request.Url}", LogType.Info);
            var replayedElement = await _orchestrator.PlayNext();
            Log($"Response Status: {replayedElement.Response.Status}", replayedElement.Response.Status switch
            { >= 500 => LogType.Error, >= 400 => LogType.Error, >= 300 => LogType.Warning, >= 200 => LogType.Success, _ => LogType.Info });

            // --- Replace Viewer Instance ---
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
            UpdateExternalVariablesView();

            if (currentIndexInViewList < FlowElements.Count - 1)
            {
                var nextViewer = FlowElements[currentIndexInViewList + 1];
                nextViewer.IsCurrentElement = true;

                int nextIndexInDisplayed = -1;
                for (int i = 0; i < _displayedElements.Count; ++i) { if (ReferenceEquals(_displayedElements[i], nextViewer)) { nextIndexInDisplayed = i; break; } }
                if (nextIndexInDisplayed != -1) { UpdateDisplayedElements(); }

                if ((currentIndexInViewList + 1) % _elementsPerPage == 0) { OnNextPage(sender, e); }
            }
            else { Log("End of flow reached.", LogType.Info); }
        }
        catch (InvalidOperationException ex)
        {
            Log($"Playback error: {ex.Message}", LogType.Warning);
            if (oldViewerInstance != null) oldViewerInstance.IsCurrentElement = false;
        }
        catch (Exception ex)
        {
            Log($"Error during step execution: {ex.Message}", LogType.Error);
            if (oldViewerInstance != null) oldViewerInstance.IsCurrentElement = false;
        }
    }

    private void OnStop(object sender, RoutedEventArgs e)
    {
        ResetFlowToOriginal();
    }
    #endregion

    #region External Variables (Left Panel)

    private void UpdateExternalVariablesView()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                ObservableExternalVariables.Clear();
                if (Flow?.ExternalVariables != null)
                {
                    var sortedVariables = Flow.ExternalVariables.OrderBy(kv => kv.Key);
                    foreach (var kvp in sortedVariables)
                    {
                        ObservableExternalVariables.Add(new ExternalVariableViewModel(kvp.Key, kvp.Value));
                    }
                }
            }
            catch (Exception ex) { Log($"Error updating external variables view: {ex.Message}", LogType.Error); }
        });
    }

    private void ExternalVariablesListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var listView = sender as ListView;
        var tappedViewModel = (e.OriginalSource as FrameworkElement)?.DataContext as ExternalVariableViewModel;

        AddVariableMenuItem.Tag = null;
        EditVariableMenuItem.Tag = tappedViewModel;
        DeleteVariableMenuItem.Tag = tappedViewModel;

        EditVariableMenuItem.IsEnabled = tappedViewModel != null;
        DeleteVariableMenuItem.IsEnabled = tappedViewModel != null;

        ExternalVariablesContextMenu.ShowAt(listView, e.GetPosition(listView));
        e.Handled = true;
    }

    private async void AddVariable_Click(object sender, RoutedEventArgs e)
    {
        if (Flow is null) { Log("Cannot add variable: No flow loaded.", LogType.Warning); return; }

        var nameBox = new TextBox { PlaceholderText = "Variable Name" };
        var valueBox = new TextBox { PlaceholderText = "Variable Value (JSON or string)", AcceptsReturn = true, Height = 100, TextWrapping = TextWrapping.Wrap }; // Added TextWrapping
        var stackPanel = new StackPanel { Spacing = 10 };
        stackPanel.Children.Add(new TextBlock { Text = "Name:", FontWeight = FontWeights.SemiBold });
        stackPanel.Children.Add(nameBox);
        stackPanel.Children.Add(new TextBlock { Text = "Value:", FontWeight = FontWeights.SemiBold });
        stackPanel.Children.Add(valueBox);

        var dialog = new ContentDialog
        {
            Title = "Add External Variable",
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
                string valueStr = valueBox.Text;

                if (string.IsNullOrWhiteSpace(name)) { Log("Variable name cannot be empty.", LogType.Warning); return; }
                if (Flow.ExternalVariables.ContainsKey(name)) { Log($"Variable '{name}' already exists.", LogType.Warning); return; }

                JToken valueToken;
                try { valueToken = JToken.Parse(valueStr); }
                catch (JsonReaderException) { valueToken = valueStr; }
                Flow.ExternalVariables.Add(name, valueToken);
                UpdateExternalVariablesView();
                Log($"Added external variable '{name}'.", LogType.Info);
            }
        }
        catch (Exception ex) { Log($"Error showing add variable dialog: {ex.Message}", LogType.Error); }
    }

    private async void EditVariable_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ExternalVariableViewModel vm && vm != null)
        {
            if (Flow is null) return;

            string originalName = vm.Key;
            string editText = vm.TooltipValue;
            var nameBox = new TextBox { Text = originalName };
            var valueBox = new TextBox { Text = editText, AcceptsReturn = true, Height = 150, TextWrapping = TextWrapping.Wrap };
            var stackPanel = new StackPanel { Spacing = 10 };
            stackPanel.Children.Add(new TextBlock { Text = "Name:", FontWeight = FontWeights.SemiBold });
            stackPanel.Children.Add(nameBox);
            stackPanel.Children.Add(new TextBlock { Text = "Value (JSON or string):", FontWeight = FontWeights.SemiBold });
            stackPanel.Children.Add(valueBox);

            var dialog = new ContentDialog
            {
                Title = "Edit External Variable",
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
                    string newValueStr = valueBox.Text;

                    if (string.IsNullOrWhiteSpace(newName)) { Log("Variable name cannot be empty.", LogType.Warning); return; }
                    if (newName != originalName && Flow.ExternalVariables.ContainsKey(newName)) { Log($"Cannot rename variable: Name '{newName}' already exists.", LogType.Warning); return; }

                    JToken newValueToken;
                    try { newValueToken = JToken.Parse(newValueStr); }
                    catch (JsonReaderException) { newValueToken = newValueStr; }

                    if (newName != originalName) { Flow.ExternalVariables.Remove(originalName); }
                    Flow.ExternalVariables[newName] = newValueToken;

                    UpdateExternalVariablesView();
                    Log($"Updated external variable '{newName}'.", LogType.Info);
                }
            }
            catch (Exception ex) { Log($"Error showing edit variable dialog: {ex.Message}", LogType.Error); }
        }
    }

    private void DeleteVariable_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ExternalVariableViewModel vm && vm != null)
        {
            if (Flow is null) return;
            if (Flow.ExternalVariables.Remove(vm.Key))
            {
                UpdateExternalVariablesView();
                Log($"Deleted external variable '{vm.Key}'.", LogType.Info);
            }
            else { Log($"Failed to delete variable '{vm.Key}' (not found).", LogType.Warning); }
        }
    }
    #endregion

}