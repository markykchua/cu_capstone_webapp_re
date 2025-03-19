using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EndpointExplorer.Controls;
//InteractiveJsonViewerItem.xaml.cs
public sealed partial class InteractiveJsonViewerItem : UserControl
{

    public bool IsJObject => Token is JObject && !HasInlineValue;
    public bool IsJArray => Token is JArray && !HasInlineValue;
    public bool IsJProperty => Token is JProperty;
    public bool IsJValue => Token is JValue && !(Token is JContainer);
    public string PropertyName => (Token as JProperty)?.Name;
    public string TokenValue => (Token as JValue)?.Value?.ToString();
    public bool HasInlineValue => Token is JProperty prop && ((prop.Value is JValue) || (prop.Value is JObject || prop.Value is JArray) && Token.Values().Count() == 0);
    public bool ShowChildren => Children.Count > 0 && !HasInlineValue;
    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler<JsonEditEventArgs> JsonEdited;
    public InteractiveJsonViewerItem()
    {
        this.InitializeComponent();
        //placeholder
        this.RightTapped += Border_RightTapped;
    }

    private void Border_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        // Show the context menu at the position of the pointer
        JsonItemContextMenu.ShowAt(sender as UIElement, e.GetPosition(sender as UIElement));

        // Mark the event as handled
        e.Handled = true;
    }

    private async void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditDialog.Title = IsJProperty ? $"Edit '{PropertyName}'" : "Edit Value";
        EditTextBox.Text = GetEditText();

        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
        Windows.UI.Core.CoreDispatcherPriority.Normal,
        async () =>
        {
            // Your dialog code here
            ContentDialog editDialog = new ContentDialog()
            {
                Title = IsJProperty ? $"Edit '{PropertyName}'" : "Edit Value",
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            TextBox editTextBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 200,
                Text = GetEditText()
            };

            editDialog.Content = editTextBox;

            try
            {
                var result = await editDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    ApplyEdit(editTextBox.Text);
                }
            }
            catch (Exception ex)
            {
                // Handle exception
            }
        });
    }

    // Get the text to edit based on the token type
    private string GetEditText()
    {
        if (IsJProperty)
        {
            JProperty property = Token as JProperty;
            return property.Value.ToString(Newtonsoft.Json.Formatting.Indented);
        }
        else if (IsJObject || IsJArray)
        {
            return Token.ToString(Newtonsoft.Json.Formatting.Indented);
        }
        else if (IsJValue)
        {
            return TokenValue;
        }
        return string.Empty;
    }

    // Apply the edited value to the token
    private void ApplyEdit(string editedText)
    {
        try
        {
            JToken newValue;
            try
            {
                newValue = JToken.Parse(editedText);
            }
            catch (JsonReaderException)
            {
                // If the edited text is not valid JSON, treat it as a string
                newValue = editedText;
            }

            // Parse the edited text based on the original token type
            if (IsJProperty)
            {
                Console.WriteLine($"Editing property with value: {editedText} and type: {Token.Type}");
                JsonEdited?.Invoke(this, new JsonEditEventArgs(Token.Path, newValue));
            }
            else if (IsJObject || IsJArray || IsJValue)
            {
                newValue = JToken.Parse(editedText);

                // Raise the event with the token path and new value
                JsonEdited?.Invoke(this, new JsonEditEventArgs(Token.Path, newValue));
            }
        }
        catch (Exception ex)
        {
            // Handle parsing errors
            // You might want to show an error message to the user
            System.Diagnostics.Debug.WriteLine($"Error parsing JSON: {ex.Message}");
        }
    }


    public string InlineValue
    {
        get
        {
            var dbg = Token;
            if (Token is not JProperty)
                return JValue.CreateNull().ToString();

            JProperty property = Token as JProperty;
            var returnString = property.Value.Type switch
            {
                JTokenType.Array => property.Value.ToString(),
                JTokenType.Object => property.Value.ToString(),
                _ => property.Value.ToString(Newtonsoft.Json.Formatting.None)
            };
            if (returnString.Length > 50)
            {
                return returnString[..50] + "....";
            }
            return returnString;
        }
    }

    #region Dependency Properties

    public JToken Token
    {
        get { return (JToken)GetValue(TokenProperty); }
        set { SetValue(TokenProperty, value); }
    }

    public static readonly DependencyProperty TokenProperty =
        DependencyProperty.Register(
            nameof(Token),
            typeof(JToken),
            typeof(InteractiveJsonViewerItem),
            new PropertyMetadata(null, OnTokenChanged));
    public ObservableCollection<JToken> Children { get; } = new ObservableCollection<JToken>();

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static void OnTokenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InteractiveJsonViewerItem item)
        {
            item.UpdateChildren(e.NewValue as JToken);
            item.OnPropertyChanged(nameof(IsJObject));
            item.OnPropertyChanged(nameof(IsJArray));
            item.OnPropertyChanged(nameof(IsJProperty));
            item.OnPropertyChanged(nameof(IsJValue));
            item.OnPropertyChanged(nameof(PropertyName));
            item.OnPropertyChanged(nameof(TokenValue));
        }
    }

    private void OnJsonEdited(object sender, JsonEditEventArgs e)
    {
        // Forward the event
        JsonEdited?.Invoke(this, e);
    }


    private void UpdateChildren(JToken token)
    {
        Children.Clear();
        if (token is JArray jsonArray)
        {
            foreach (var child in jsonArray)
            {
                Children.Add(child);
            }
        }
        else if (token is JObject jsonObject)
        {
            foreach (var property in jsonObject.Properties())
            {
                Children.Add(property); // Add JProperty to children
            }
        }
        else if (token is JProperty jProperty)
        {
            Children.Add(jProperty.Value); // Add JProperty's value for rendering
        }
        // JValue has no children, so no handling needed
    }

    /// <summary>
    /// Gets or sets the FlowElement associated with this item.
    /// </summary>
    public FlowElement FlowElement
    {
        get { return (FlowElement)GetValue(FlowElementProperty); }
        set { SetValue(FlowElementProperty, value); }
    }

    public static readonly DependencyProperty FlowElementProperty =
        DependencyProperty.Register(
            nameof(FlowElement),
            typeof(FlowElement),
            typeof(InteractiveJsonViewerItem),
            new PropertyMetadata(null));

    #endregion

    private void UpdateTokenDisplay()
    {
        // If you have a TextBlock (e.g., TokenTextBlock) bound to Token's content,
        // you might update it here. For instance:
        // TokenTextBlock.Text = Token?.ToString() ?? string.Empty;
        // If your XAML already binds to Token.ToString(), this method can be left empty.
    }

    /// <summary>
    /// Handles the Edit button's Click event.
    /// </summary>
    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        OnEditToken(FlowElement, Token);
    }

    private void OnEditToken(FlowElement flowElement, JToken token)
    {
        // placeholder
        System.Diagnostics.Debug.WriteLine($"Edit requested for token: {token} in element {flowElement.Request.Url}");
    }
}

public class JTokenEmptyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is JObject obj) return !obj.HasValues;
        if (value is JArray arr) return !arr.HasValues;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

public class JsonEditEventArgs : EventArgs
{
    public string Path { get; }
    public JToken NewValue { get; }

    public JsonEditEventArgs(string path, JToken newValue)
    {
        Path = path;
        NewValue = newValue;
    }
}