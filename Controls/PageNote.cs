using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Zametkis.Models;

namespace Zametkis.Controls;

// карточка вложенной страницы (как в Milanote): клик по иконке открывает её как отдельный
// бесконечный холст (см. WorkDirectory.OpenPage/GoBack), заголовок редактируется прямо тут.
// Пока страница не открыта, её содержимое хранится как чистые данные в NestedItems - никаких
// живых дочерних элементов (в т.ч. WebView2 у текстовых заметок) она в фоне не держит.
public class PageNote : Border
{
    public string Title => _titleBox.Text;
    public List<NoteItemData> NestedItems { get; set; } = new();
    public double NestedCameraX { get; set; }
    public double NestedCameraY { get; set; }
    public double NestedCameraZoom { get; set; } = 1.0;

    public event EventHandler? OpenRequested;

    private readonly TextBox _titleBox;

    public PageNote(string title)
    {
        Width = 160;
        Background = (Brush)Application.Current.FindResource("SurfaceBrush");
        BorderBrush = (Brush)Application.Current.FindResource("BorderBrush");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(10);

        var icon = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 6,3 L 22,3 L 22,11 L 30,11 L 30,37 L 6,37 Z M 22,3 L 22,11 L 30,11"),
            Stroke = (Brush)Application.Current.FindResource("AccentBrush"),
            StrokeThickness = 1.6,
            StrokeLineJoin = PenLineJoin.Round,
            Width = 36,
            Height = 40,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 16, 0, 8)
        };

        var openArea = new Border
        {
            Background = (Brush)Application.Current.FindResource("ToolIdleBrush"),
            CornerRadius = new CornerRadius(9, 9, 0, 0),
            Cursor = Cursors.Hand,
            Child = icon
        };
        openArea.MouseLeftButtonUp += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        _titleBox = new TextBox
        {
            Text = title,
            TextAlignment = TextAlignment.Center,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = (Brush)Application.Current.FindResource("TextPrimaryBrush"),
            Padding = new Thickness(8, 6, 8, 8),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };

        var stack = new StackPanel();
        stack.Children.Add(openArea);
        stack.Children.Add(_titleBox);
        Child = stack;
    }
}
