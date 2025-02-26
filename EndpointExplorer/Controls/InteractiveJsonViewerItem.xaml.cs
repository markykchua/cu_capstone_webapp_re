using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
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
    public InteractiveJsonViewerItem()
    {
        this.InitializeComponent();
        //placeholder
        this.Tapped += InteractiveJsonViewerItem_Tapped;
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

    private void InteractiveJsonViewerItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
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

    /// <summary>
    /// Called when the user requests to edit this token.
    /// Override this method to open your custom edit interface.
    /// </summary>
    /// <param name="flowElement">The associated FlowElement.</param>
    /// <param name="token">The selected JSON token.</param>
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