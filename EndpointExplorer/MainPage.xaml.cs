using EndpointExplorer.Controls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Input;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using System.IO;

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

    private void OnExportAdded(object sender, ExportChangedEventArgs e)
    {
        if (sender is FlowElementViewer viewer && viewer.FlowElement != null)
        {
            var flowElement = Flow?.FlowElements.FirstOrDefault(fe => fe == viewer.FlowElement);
            if (flowElement != null)
            {
                if (!flowElement.Exports.ContainsKey(e.Name))
                {
                    flowElement.Exports.Add(e.Name, new Export(e.JsonPath, e.Regex));
                    Log($"Added export '{e.Name}' to element {Flow.FlowElements.IndexOf(flowElement) + 1}", LogType.Info);
                }
                else
                {
                    Log($"Export '{e.Name}' already exists for element {Flow.FlowElements.IndexOf(flowElement) + 1}.", LogType.Warning);
                }
            }
            else { Log($"Error adding export: FlowElement not found for viewer.", LogType.Error); }
        }
    }

    private void OnExportEdited(object sender, ExportChangedEventArgs e)
    {
        if (sender is FlowElementViewer viewer && viewer.FlowElement != null)
        {
            var flowElement = Flow?.FlowElements.FirstOrDefault(fe => fe == viewer.FlowElement);
            if (flowElement != null)
            {
                if (flowElement.Exports.ContainsKey(e.OriginalName))
                {
                    if (e.OriginalName != e.Name)
                    {
                        if (flowElement.Exports.ContainsKey(e.Name))
                        {
                            Log($"Cannot rename export: Name '{e.Name}' already exists for element {Flow.FlowElements.IndexOf(flowElement) + 1}.", LogType.Warning);
                            viewer.RefreshExports();
                            return;
                        }
                        flowElement.Exports.Remove(e.OriginalName);
                        flowElement.Exports.Add(e.Name, new Export(e.JsonPath, e.Regex));
                        Log($"Renamed export '{e.OriginalName}' to '{e.Name}' and updated details on element {Flow.FlowElements.IndexOf(flowElement) + 1}", LogType.Info);
                    }
                    else
                    {
                        flowElement.Exports[e.Name] = new Export(e.JsonPath, e.Regex);
                        Log($"Edited export '{e.Name}' on element {Flow.FlowElements.IndexOf(flowElement) + 1}", LogType.Info);
                    }
                }
                else
                {
                    Log($"Cannot edit export: Original name '{e.OriginalName}' not found for element {Flow.FlowElements.IndexOf(flowElement) + 1}.", LogType.Warning);
                    viewer.RefreshExports();
                }
            }
            else { Log($"Error editing export: FlowElement not found for viewer.", LogType.Error); }
        }
    }

    private void OnExportDeleted(object sender, ExportChangedEventArgs e)
    {
        if (sender is FlowElementViewer viewer && viewer.FlowElement != null)
        {
            var flowElement = Flow?.FlowElements.FirstOrDefault(fe => fe == viewer.FlowElement);
            if (flowElement != null)
            {
                if (flowElement.Exports.Remove(e.Name))
                {
                    Log($"Deleted export '{e.Name}' from element {Flow.FlowElements.IndexOf(flowElement) + 1}", LogType.Info);
                }
                else
                {
                    Log($"Could not delete export '{e.Name}' from element {Flow.FlowElements.IndexOf(flowElement) + 1} (not found in model).", LogType.Warning);
                    // Refresh viewer just in case its state was wrong
                    viewer.RefreshExports();
                }
            }
            else { Log($"Error deleting export: FlowElement not found for viewer.", LogType.Error); }
        }
    }
    #endregion

    #region Export Menu Handlers

    private async void OnExportToOpenAPI(object sender, RoutedEventArgs e)
    {
        if (Flow is null || !Flow.FlowElements.Any())
        {
            Log("No flow loaded or flow is empty. Cannot generate OpenAPI spec.", LogType.Warning);
            return;
        }

        Log("Generating OpenAPI specification...", LogType.Info);
        string openApiSpecJson;
        try
        {
            openApiSpecJson = Utils.GenerateOpenApiSpec(Flow);
            if (string.IsNullOrEmpty(openApiSpecJson))
            {
                Log("OpenAPI generation returned empty result.", LogType.Warning);
                return;
            }
        }
        catch (Exception ex)
        {
            Log($"Error generating OpenAPI spec: {ex.Message}", LogType.Error);
            Debug.WriteLine($"OpenAPI Generation Error: {ex}");
            return;
        }

        var fileSavePicker = new FileSavePicker();
        fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileSavePicker.SuggestedFileName = "openapi_spec.json";
        fileSavePicker.FileTypeChoices.Add("OpenAPI JSON", new List<string>() { ".json" });

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileSavePicker, hwnd);

        StorageFile saveFile = await fileSavePicker.PickSaveFileAsync();
        if (saveFile != null)
        {
            Log($"Attempting to save OpenAPI spec to {saveFile.Name}...", LogType.Info);
            try
            {
                CachedFileManager.DeferUpdates(saveFile);
                await FileIO.WriteTextAsync(saveFile, openApiSpecJson);
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(saveFile);

                if (status == FileUpdateStatus.Complete)
                {
                    Log($"OpenAPI specification saved successfully as {saveFile.Name}", LogType.Success);
                }
                else
                {
                    Log($"Failed to finalize saving OpenAPI spec to {saveFile.Name}. Status: {status}", LogType.Error);
                }
            }
            catch (Exception ex)
            {
                Log($"Error writing OpenAPI spec to file {saveFile.Name}: {ex.Message}", LogType.Error);
                Debug.WriteLine($"OpenAPI File Save Error: {ex}");
            }
        }
        else
        {
            Log("Export to OpenAPI cancelled.", LogType.Info);
        }
    }


    private async void OnExportToPython(object sender, RoutedEventArgs e)
    {
        Log("Exporting flow to Python script...", LogType.Info);

        var fileSavePicker = new FileSavePicker();
        fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        fileSavePicker.SuggestedFileName = "PythonExport.py";
        fileSavePicker.FileTypeChoices.Add("Python File", new List<string>() { ".py" });

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileSavePicker, hwnd);

        StorageFile saveFile = await fileSavePicker.PickSaveFileAsync();
        if (saveFile != null)
        {
            Log($"Attempting to save Python script to {saveFile.Name}...", LogType.Info);
            try
            {
                CachedFileManager.DeferUpdates(saveFile);

                // Python code to be saved
                string pythonCode = @"
import requests
from bs4 import BeautifulSoup
import json 
import urllib.parse 
# Session to persist cookies
session = requests.Session()

# get the CSRF token from the login page
def get_csrf_token(base_url):
    login_url = f""{base_url}""

    response = session.get(login_url)
    print(f""GET {login_url} - Response: {response.status_code}"")
    
    soup = BeautifulSoup(response.text, 'html.parser')
    
    # Extract the CSRF token
    csrf_token = soup.find('input', {'name': 'csrfmiddlewaretoken'})
    if csrf_token:
        print(f""Extracted CSRF Token: {csrf_token['value']}"")
        return csrf_token['value']
    else:
        print(""CSRF token not found. Check the login page HTML."")
        exit()

# login function
def login(base_url, identifier, password, method):
    login_url = f""{base_url}""
    
    # Get the CSRF token
    csrf_token = get_csrf_token(base_url)
    
    # Decode the identifier and password
    decoded_identifier = urllib.parse.unquote_plus(identifier)
    decoded_password = urllib.parse.unquote_plus(password)
    
    # Determine if the identifier is an email or username
    if ""@"" in decoded_identifier:
        login_payload = {
            'email': decoded_identifier,  # Use the identifier as email
            'password': decoded_password,
            'csrfmiddlewaretoken': csrf_token  
        }
    else:
        login_payload = {
            'username': decoded_identifier,  # Use the identifier as username
            'password': decoded_password,
            'csrfmiddlewaretoken': csrf_token 
        }
    
    # Set headers, including the Referer header
    headers = {
        'Referer': login_url  # Include the Referer header
    }

    # Send login request
    if method == ""GET"":
        login_response = session.get(login_url, data=login_payload, headers=headers)
    elif method == ""POST"":
        login_response = session.post(login_url, data=login_payload, headers=headers)
    
    print(login_payload)
    if ""Logged in as"" in login_response.text:
        print(""Login successful!"")
    else:
        print(""Login failed. Check your credentials."")
        exit()

# Function to parse the JSON file
def parse_request_file(file_path):
    with open(file_path, 'r', encoding='utf-8') as file:
        data = json.load(file)

    # Extract FlowElements and ExternalVariables
    flow_elements = data.get(""FlowElements"", [])
    external_variables = data.get(""ExternalVariables"", {})

    # Log the extracted variables for debugging
    print(""\\n=== External Variables ==="")
    for key, value in external_variables.items():
        print(f""{key}: {value}"")

    return flow_elements, external_variables

# Function to replay requests from the parsed data
def replay_requests(file_path):
    flow_elements, external_variables = parse_request_file(file_path)

    for element in flow_elements:
        request = element.get(""Request"", {})
        expected_response = element.get(""Response"", {})  # Expected response from JSON

        print(f""\\n=== Request ==="")
        print(request)

        method = request.get(""Method"")
        if method == 0:
            method = ""GET""
        elif method == 1:
            method = ""POST""
        url = request.get(""Url"")
        headers = request.get(""Headers"", {})
        headers = {key: value for key, value in headers.items() if not key.startswith("":"")}
        body = request.get(""Body"", None)

        # Skip requests with ""googleads"" in the URL
        if url and ""googleads"" in url:
            print(f""Skipping request with URL containing 'googleads': {url}"")
            continue

        print(f""\\nReplaying Request: {method} {url}"")
        print(f""Headers: {headers}"")
        print(f""Body: {body}"")

        if method == ""GET"":
            response_obj = session.get(url, headers=headers)
        elif method == ""POST"":
            response_obj = session.post(url, headers=headers, data=body)
        else:
            print(f""Unsupported method: {method}"")
            continue

        actual_response = {
            ""StatusCode"": response_obj.status_code,
            ""Body"": response_obj.text[:500]  # Limit to first 500 characters for readability
        }

        # Display comparison of expected and actual responses
        print(""\\n=== Response Comparison ==="")
        print(f""Expected Status Code: {expected_response.get('StatusCode')}"")
        print(f""Actual Status Code: {actual_response['StatusCode']}"")
        print(f""Expected Body (first 500 chars): {expected_response.get('Body', '')[:500]}"")
        print(f""Actual Body (first 500 chars): {actual_response['Body']}"")

    # Function to replay requests step by step
def replay_requests_step_by_step(file_path):
    flow_elements, external_variables = parse_request_file(file_path)

    for i, element in enumerate(flow_elements):
        request = element.get(""Request"", {})
        expected_response = element.get(""Response"", {})  # Expected response from JSON

        method = request.get(""Method"")
        if method == 0:
            method = ""GET""
        elif method == 1:
            method = ""POST""
        url = request.get(""Url"")
        headers = request.get(""Headers"", {})
        headers = {key: value for key, value in headers.items() if not key.startswith("":"")}
        body = request.get(""Body"", None)

        print(f""\nStep {i + 1}: Replaying Request: {method} {url}"")
        print(f""Headers: {headers}"")
        print(f""Body: {body}"")

        if url and (""login"" in url or ""signin"" in url):  # Ensure url is not None
            if ""extracted_email_"" in external_variables and ""extracted_password_"" in external_variables:
                login(
                    url,
                    external_variables[""extracted_email_""],
                    external_variables[""extracted_password_""],
                    method,
                )
            else:
                print(""Missing required external variables for login."")
            continue

        if method == ""GET"":
            response_obj = session.get(url, headers=headers)
        elif method == ""POST"":
            response_obj = session.post(url, headers=headers, data=body)
        else:
            print(f""Unsupported method: {method}"")
            continue

        actual_response = {
            ""StatusCode"": response_obj.status_code,
            ""Body"": response_obj.text[:500]  # Limit to first 500 characters for readability
        }

        # Display comparison of expected and actual responses
        print(""\n=== Response Comparison ==="")
        print(f""Expected Status Code: {expected_response.get('StatusCode')}"")
        print(f""Actual Status Code: {actual_response['StatusCode']}"")
        print(f""Expected Body (first 500 chars): {expected_response.get('Body', '')[:500]}"")
        print(f""Actual Body (first 500 chars): {actual_response['Body']}"")

        # Pause for user input before proceeding to the next request
        input(""\nPress Enter to continue to the next request..."")

# CLI menu
def display_menu():
    file_path = None
    external_variables = {}

    while True:
        if not file_path:
            print(""\nNo file selected. Please select a JSON file to proceed."")
            file_path = input(""Enter the path to the JSON file: "")
            try:
                _, external_variables = parse_request_file(file_path)
                for key, value in external_variables.items():
                    if key in (""extracted_email_"", ""extracted_password_"", ""extracted_username_""):
                        external_variables[key] = urllib.parse.unquote_plus(value)
            except Exception as e:
                print(f""Error loading file: {e}"")
                file_path = None
                continue

        print(""\n=== Main Menu ==="")
        print(f""Current file: {file_path}"")
        print(""1. Replay requests from the selected JSON file"")
        print(""2. Replay requests step by step from the selected JSON file"")
        print(""3. Modify external variables"")
        print(""4. Change JSON file"")
        print(""5. Exit"")
        choice = input(""Enter your choice: "")

        if choice == ""1"":
            replay_requests(file_path)
        elif choice == ""2"":
            replay_requests_step_by_step(file_path)
        elif choice == ""3"":
            modify_external_variables(external_variables)
        elif choice == ""4"":
            file_path = None  # Reset file selection
        elif choice == ""5"":
            print(""Exiting the program. Goodbye!"")
            break
        else:
            print(""Invalid choice. Please try again."")

# Modify external variables CLI
def modify_external_variables(external_variables):
    print(""\n=== Modify External Variables ==="")
    print(""Current external variables:"")
    for key, value in external_variables.items():
        print(f""{key}: {value}"")

    while True:
        print(""\nOptions:"")
        print(""1. Modify a variable"")
        print(""2. Add a new variable"")
        print(""3. Delete a variable"")
        print(""4. Return to main menu"")
        choice = input(""Enter your choice: "")

        if choice == ""1"":
            print(external_variables)
            key = input(""Enter the name of the variable to modify: "")
            if key in external_variables:
                new_value = input(f""Enter the new value for {key}: "")
                external_variables[key] = new_value
                print(f""Variable '{key}' updated to '{new_value}'."")
            else:
                print(f""Variable '{key}' not found."")
        elif choice == ""2"":
            key = input(""Enter the name of the new variable: "")
            value = input(f""Enter the value for {key}: "")
            external_variables[key] = value
            print(f""Variable '{key}' added with value '{value}'."")
        elif choice == ""3"":
            key = input(""Enter the name of the variable to delete: "")
            if key in external_variables:
                del external_variables[key]
                print(f""Variable '{key}' deleted."")
            else:
                print(f""Variable '{key}' not found."")
        elif choice == ""4"":
            break
        else:
            print(""Invalid choice. Please try again."")

if __name__ == ""__main__"":
    display_menu()
";

                // Write the Python code to the selected file
                await FileIO.WriteTextAsync(saveFile, pythonCode);

                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(saveFile);

                if (status == FileUpdateStatus.Complete)
                {
                    Log($"Python script exported successfully as {saveFile.Name}", LogType.Success);
                }
                else
                {
                    Log($"Failed to finalize saving Python script to {saveFile.Name}. Status: {status}", LogType.Error);
                }
            }
            catch (Exception ex)
            {
                Log($"Error writing Python script to file {saveFile.Name}: {ex.Message}", LogType.Error);
                Debug.WriteLine($"Python Export Error: {ex}");
            }
        }
        else
        {
            Log("Export to Python cancelled.", LogType.Info);
        }
    }
    #endregion 

}