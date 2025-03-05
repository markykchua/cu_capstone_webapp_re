using System.Collections.ObjectModel;
using CommandLine;

namespace EndpointExplorer.Controls;
// InteractiveJsonViewer.xaml.cs
public sealed partial class InteractiveJsonViewer : UserControl
{
    private bool _isInView;
    public bool IsInView
    {
        get => _isInView;
        set
        {
            if (_isInView != value)
            {
                _isInView = value;
                if (!value)
                {
                    if (JsonItemsControl != null)
                    {
                        JsonItemsControl.ItemsSource = new ObservableCollection<JToken> { new JValue(string.Empty) };
                    }
                }
                else
                {
                    UpdateJsonItems();
                }
            }

        }
    }
    public FrameworkElement Frame => this;
    public InteractiveJsonViewer()
    {
        this.InitializeComponent();
    }

    public static readonly DependencyProperty FlowElementProperty =
        DependencyProperty.Register(
            nameof(FlowElement),
            typeof(FlowElement),
            typeof(InteractiveJsonViewer),
            new PropertyMetadata(null, OnFlowElementChanged));

    public FlowElement FlowElement
    {
        get => (FlowElement)GetValue(FlowElementProperty);
        set => SetValue(FlowElementProperty, value);
    }

    private static void OnFlowElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InteractiveJsonViewer viewer)
        {
            viewer.UpdateJsonItems();
        }
    }

    private void UpdateJsonItems()
    {
        if (FlowElement?.Value is JToken token)
        {
            JsonItemsControl.ItemsSource = ParseJsonToItems(token);
        }
        else
        {
            JsonItemsControl.ItemsSource = null;
        }
    }

    private ObservableCollection<JToken> ParseJsonToItems(JToken token)
    {
        var items = new ObservableCollection<JToken>();

        if (token is JArray jsonArray)
        {
            foreach (var child in jsonArray)
            {
                items.Add(child);
            }
        }
        else if (token is JObject jsonObject)
        {
            foreach (var property in jsonObject.Properties())
            {
                items.Add(property); // Add JProperty instead of property.Value
            }
        }
        else
        {
            items.Add(token);
        }

        return items;
    }
}
