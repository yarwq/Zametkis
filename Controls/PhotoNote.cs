using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Zametkis.Controls;

// фото с подписью: заголовок-плашка всегда показывает превью подписи (или плейсхолдер),
// по клику разворачивает/сворачивает редактируемое многострочное поле под ним
public class PhotoNote : StackPanel
{
    public string SourcePath { get; }
    public string CaptionText => _captionBox.Text;
    public bool IsCaptionExpanded => _expanded;

    private readonly TextBox _captionBox;
    private readonly TextBlock _previewText;
    private readonly TextBlock _chevron;
    private bool _expanded;

    public PhotoNote(ImageSource imageSource, string sourcePath, string caption, bool expanded)
    {
        SourcePath = sourcePath;
        Width = 200;

        var image = new Image { Source = imageSource, Stretch = Stretch.Uniform };
        Children.Add(image);

        _chevron = new TextBlock
        {
            Width = 16,
            Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush")
        };
        _previewText = new TextBlock
        {
            Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
        headerPanel.Children.Add(_chevron);
        headerPanel.Children.Add(_previewText);

        var header = new Border
        {
            Background = (Brush)Application.Current.FindResource("ToolIdleBrush"),
            Padding = new Thickness(8, 4, 8, 4),
            Cursor = Cursors.Hand,
            Child = headerPanel
        };
        header.MouseLeftButtonUp += (_, _) => SetExpanded(!_expanded);
        Children.Add(header);

        _captionBox = new TextBox
        {
            Text = caption,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 40,
            BorderThickness = new Thickness(0),
            Background = Brushes.White,
            Padding = new Thickness(8)
        };
        _captionBox.TextChanged += (_, _) => UpdatePreview();
        Children.Add(_captionBox);

        SetExpanded(expanded);
    }

    private void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        _captionBox.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        _chevron.Text = expanded ? "▾" : "▸";
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        string text = _captionBox.Text;
        bool empty = string.IsNullOrWhiteSpace(text);
        _previewText.Text = empty ? "Подпись..." : text;
        _previewText.FontStyle = empty ? FontStyles.Italic : FontStyles.Normal;
    }
}
