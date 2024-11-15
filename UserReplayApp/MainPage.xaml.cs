using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace UserReplayApp;

public partial class MainPage : ContentPage
{
	int count = 0;
	string fileName = string.Empty;

	public MainPage()
	{
		InitializeComponent();
	}

	private async void OnSelectFileClicked(object sender, EventArgs e)
	{
		try
		{
			// Open the file picker
			var fileResult = await FilePicker.PickAsync();

			if (fileResult != null)
			{
				// Log or use the selected file
				Console.WriteLine($"Selected File: {fileResult.FileName} at {fileResult.FullPath}");
				await DisplayAlert("File Selected", $"You selected {fileResult.FileName}", "OK");
				fileName = fileResult.FileName;
			}
			else
			{
				await DisplayAlert("No File", "No file selected.", "OK");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
			await DisplayAlert("Error", "Failed to pick a file.", "OK");
		}
	}

	private void OnAddBoxClicked(object sender, EventArgs e)
	{
		count++;
		Console.WriteLine($"Button clicked {count} times");
		AddDraggableBox($"Box #{count}", new Point(100, 100));
	}

	private void AddDraggableBox(string text, Point initialPosition)
	{
		// Create the Border
		var draggableBorder = new Border
		{
			WidthRequest = 100,
			HeightRequest = 100,
			BackgroundColor = Colors.SkyBlue,
			Stroke = Colors.Black,
			StrokeThickness = 2,
			StrokeShape = new RoundRectangle { CornerRadius = 20 },
			Content = new Label
			{
				Text = text,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center,
				TextColor = Colors.White
			}
		};

		// Add the PanGestureRecognizer
		double x = 0, y = 0; // Variables to track position
		var panGesture = new PanGestureRecognizer();
		panGesture.PanUpdated += (sender, e) =>
		{
			switch (e.StatusType)
			{
				case GestureStatus.Started:
					x = draggableBorder.TranslationX;
					y = draggableBorder.TranslationY;
					break;

				case GestureStatus.Running:
					draggableBorder.TranslationX = x + e.TotalX;
					draggableBorder.TranslationY = y + e.TotalY;
					break;
			}
		};
		draggableBorder.GestureRecognizers.Add(panGesture);

		// Position the Border in the AbsoluteLayout
		AbsoluteLayout.SetLayoutBounds(draggableBorder, new Rect(initialPosition.X, initialPosition.Y, 100, 100));
		AbsoluteLayout.SetLayoutFlags(draggableBorder, AbsoluteLayoutFlags.None);

		MainLayout.Children.Add(draggableBorder);
		(MainLayout as IView).InvalidateArrange();
	}
}

