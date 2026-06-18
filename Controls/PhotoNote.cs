using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Zametkis.Controls;

// фото с подписью: заголовок-плашка всегда показывает превью подписи (или плейсхолдер),
// по клику разворачивает/сворачивает редактируемое многострочное поле под ним.
// если файл - анимированный GIF, проигрывает все кадры по циклу.
public class PhotoNote : StackPanel
{
    public string SourcePath { get; }
    public string CaptionText => _captionBox.Text;
    public bool IsCaptionExpanded => _expanded;

    private readonly TextBox _captionBox;
    private readonly TextBlock _previewText;
    private readonly TextBlock _chevron;
    private bool _expanded;

    public PhotoNote(string filePath, string caption, bool expanded)
    {
        SourcePath = filePath;
        Width = 200;

        var image = new Image { Stretch = Stretch.Uniform };
        if (TryCreateGifAnimation(filePath, out var animation))
            image.BeginAnimation(Image.SourceProperty, animation);
        else
            image.Source = LoadStaticBitmap(filePath);
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

    private static BitmapImage LoadStaticBitmap(string filePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath);
        bitmap.EndInit();
        return bitmap;
    }

    // если файл - GIF с несколькими кадрами, собирает покадровую анимацию с зацикливанием;
    // для статичных картинок (и однокадровых GIF) возвращает false
    private static bool TryCreateGifAnimation(string filePath, out ObjectAnimationUsingKeyFrames? animation)
    {
        animation = null;
        if (!string.Equals(System.IO.Path.GetExtension(filePath), ".gif", StringComparison.OrdinalIgnoreCase))
            return false;

        GifBitmapDecoder decoder;
        try
        {
            decoder = new GifBitmapDecoder(new Uri(filePath), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        }
        catch
        {
            return false;
        }

        if (decoder.Frames.Count <= 1)
            return false;

        var keyFrames = new ObjectAnimationUsingKeyFrames();
        var time = TimeSpan.Zero;
        foreach (var frame in decoder.Frames)
        {
            time += GetFrameDelay(frame);
            keyFrames.KeyFrames.Add(new DiscreteObjectKeyFrame(frame, time));
        }
        keyFrames.Duration = time;
        keyFrames.RepeatBehavior = RepeatBehavior.Forever;
        animation = keyFrames;
        return true;
    }

    private static TimeSpan GetFrameDelay(BitmapFrame frame)
    {
        const int defaultDelayMs = 100;
        if (frame.Metadata is BitmapMetadata metadata)
        {
            try
            {
                object? delay = metadata.GetQuery("/grctlext/Delay");
                if (delay != null)
                {
                    int ms = Convert.ToInt32(delay) * 10;
                    return TimeSpan.FromMilliseconds(ms > 0 ? ms : defaultDelayMs);
                }
            }
            catch
            {
                // у части GIF нет метаданных задержки кадра - используем стандартную
            }
        }
        return TimeSpan.FromMilliseconds(defaultDelayMs);
    }
}
