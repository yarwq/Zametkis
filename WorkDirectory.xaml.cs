using System.IO;
using System.IO.Compression;
using System.Text.Json;
using IOPath = System.IO.Path;
using ShapePath = System.Windows.Shapes.Path;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using Zametkis.Enums;
using Zametkis.Models;

namespace Zametkis;

public partial class WorkDirectory : Window
{
    private double CameraX;
    private double CameraY;
    private double CameraZoom = 1.0;
    //
    private Point currentPoint = new Point();
    private ScaleTransform scaleTransform;
    private ToolsZametki _currentTool;
    private Window workWindow;
    private TranslateTransform translate;
    private List<Point>? _currentStrokePoints;
    private ShapePath? _currentStrokeShape;
    public WorkDirectory()
    {
        _currentTool = ToolsZametki.None;
        InitializeComponent();

        // зум и панорамирование - через RenderTransform, а не LayoutTransform:
        // LayoutTransform участвует в раскладке и сжимает локальный размер канваса при масштабировании,
        // из-за чего тайловая сетка точек на фоне рендерится нестабильно на больших зумах
        scaleTransform = new ScaleTransform(1.0, 1.0);
        translate = new TranslateTransform();
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(scaleTransform);
        transformGroup.Children.Add(translate);
        paintSurface.RenderTransform = transformGroup;

        workWindow = Window.GetWindow(this);
    }

    private void AddPaint(object sender, RoutedEventArgs e)
    {
        if (_currentTool != ToolsZametki.Paint)
        _currentTool = ToolsZametki.Paint;
        else
        {
            _currentTool = ToolsZametki.None;
        }
        UpdateToolButtonVisuals();
    }

    private void AddText(object sender, RoutedEventArgs e)
    {
        if(_currentTool != ToolsZametki.Text)
        _currentTool = ToolsZametki.Text;
        else
        {
            _currentTool = ToolsZametki.None;
        }
        UpdateToolButtonVisuals();
    }

    private void AddPhoto(object sender, RoutedEventArgs e)
    {
        if (_currentTool != ToolsZametki.Photo)
            _currentTool = ToolsZametki.Photo;
        else
        {
            _currentTool = ToolsZametki.None;
        }
        UpdateToolButtonVisuals();
    }

    private void UpdateToolButtonVisuals()
    {
        SetToolButtonActive(PaintToolButton, _currentTool == ToolsZametki.Paint);
        SetToolButtonActive(TextToolButton, _currentTool == ToolsZametki.Text);
        SetToolButtonActive(PhotoToolButton, _currentTool == ToolsZametki.Photo);
    }

    private void SetToolButtonActive(Button button, bool active)
    {
        button.Style = (Style)FindResource(active ? "ToolButtonActiveStyle" : "ToolButtonStyle");
    }

    private void WorkWithTool(object sender, MouseButtonEventArgs e)
    {
        var pos = Mouse.GetPosition(paintSurface);

        var posX = pos.X;
        var posY = pos.Y;
        switch (_currentTool)
        {
            case ToolsZametki.Text:
                paintSurface.Children.Add(CreateTextBox(posX, posY, string.Empty));
                break;
            case ToolsZametki.Photo:
                var dialog = new OpenFileDialog
                {
                    Filter = "Изображения|*.png;*.jpg;*.jpeg;*.gif;*.bmp"
                };
                if (dialog.ShowDialog() == true)
                {
                    paintSurface.Children.Add(CreateImage(posX, posY, dialog.FileName));
                }
                break;
            case ToolsZametki.Paint:

            default:
                break;
        }
    }

    private static TextBox CreateTextBox(double x, double y, string text)
    {
        TextBox tb = new TextBox
        {
            MinWidth = 60,
            MinHeight = 20,
            AcceptsReturn = true,
            Text = text
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        return tb;
    }

    private static Image CreateImage(double x, double y, string filePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath);
        bitmap.EndInit();

        Image image = new Image
        {
            Source = bitmap,
            Width = 200,
            Stretch = Stretch.Uniform
        };
        Canvas.SetLeft(image, x);
        Canvas.SetTop(image, y);
        return image;
    }

    private static ShapePath CreateStrokePath(List<Point> points)
    {
        return new ShapePath
        {
            Stroke = SystemColors.WindowFrameBrush,
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Data = BuildSmoothGeometry(points),
            Tag = points
        };
    }

    // строим гладкую кривую через все точки мазка (Catmull-Rom, переведённый в кубические Bezier-сегменты),
    // иначе при сильном зуме виден ломаный набор прямых отрезков, по которым мазок был "сэмплирован"
    private static PathGeometry BuildSmoothGeometry(List<Point> points)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = false };

        if (points.Count < 3)
        {
            for (int i = 1; i < points.Count; i++)
                figure.Segments.Add(new LineSegment(points[i], true));
        }
        else
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Point p0 = i == 0 ? points[i] : points[i - 1];
                Point p1 = points[i];
                Point p2 = points[i + 1];
                Point p3 = i + 2 < points.Count ? points[i + 2] : p2;

                Point c1 = new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
                Point c2 = new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);

                figure.Segments.Add(new BezierSegment(c1, c2, p2, true));
            }
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void ZoomInAndOut(object sender, MouseWheelEventArgs e)
    {
        // точка мира, которая сейчас под курсором - она должна остаться под курсором после зума
        var worldMouse = Mouse.GetPosition(paintSurface);
        double oldZoom = CameraZoom;

        bool zoomIn = e.Delta > 0;
        CameraZoom += zoomIn ? 0.1 : -0.1;
        CameraZoom = CameraZoom < 0.1 ? 0.1 : CameraZoom;
        CameraZoom = CameraZoom > 10.0 ? 10.0 : CameraZoom;

        double ratio = oldZoom / CameraZoom;
        CameraX = worldMouse.X - (worldMouse.X - CameraX) * ratio;
        CameraY = worldMouse.Y - (worldMouse.Y - CameraY) * ratio;

        scaleTransform.ScaleX = CameraZoom;
        scaleTransform.ScaleY = CameraZoom;
        UpdateCamera();
    }
    
    
    ///разобраться с формулами
    ///
    private void UpdateCamera()
    {
        translate.X = -CameraX * CameraZoom;
        translate.Y = -CameraY * CameraZoom;
    }
    
    
    
    /// <summary>
    /// тут мы начинаем рисовать (запоминаем откуда начнется рисунок при касании мышки)
    /// сменить название
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void PaintME(object sender, MouseButtonEventArgs e)
    {
        switch (_currentTool)
        {
            case ToolsZametki.Paint:
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    _currentStrokePoints = new List<Point> { Mouse.GetPosition(paintSurface) };
                    _currentStrokeShape = CreateStrokePath(_currentStrokePoints);
                    paintSurface.Children.Add(_currentStrokeShape);
                }
                break;
            case ToolsZametki.None:
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    paintSurface.CaptureMouse();
                    currentPoint = Mouse.GetPosition(Grid1);
                }
                break;
        }
    }
    /// <summary>
    /// тут рисуем за движением мыши
    /// сменить название
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>

    private void DrawME(object sender, MouseEventArgs e)
    {
        switch (_currentTool)
        {
            case ToolsZametki.Paint:
                if (e.LeftButton == MouseButtonState.Pressed && _currentStrokePoints != null && _currentStrokeShape != null)
                {
                    _currentStrokePoints.Add(Mouse.GetPosition(paintSurface));
                    _currentStrokeShape.Data = BuildSmoothGeometry(_currentStrokePoints);
                }
                break;
            case ToolsZametki.None:
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    if (paintSurface.IsMouseCaptured)
                    {
                        var p = e.GetPosition(Grid1);
                        var diff = p - currentPoint;
                        currentPoint = p;

                        CameraX -= diff.X / CameraZoom;
                        CameraY -= diff.Y / CameraZoom;
                        UpdateCamera();
                    }
                    //TranslateTransform translateTransform1 = new TranslateTransform ( 50 , 20 );
                }
                break;
        }
    }

    private void StopMoving(object sender, MouseButtonEventArgs e)
    {
        if (paintSurface.IsMouseCaptured)
        {
            paintSurface.ReleaseMouseCapture();
        }

        _currentStrokePoints = null;
        _currentStrokeShape = null;
    }

    private void SaveZametki(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Заметки Zametkis (*.zametki)|*.zametki",
            DefaultExt = "zametki",
            FileName = "Заметка"
        };
        if (dialog.ShowDialog() != true)
            return;

        string tempDir = IOPath.Combine(IOPath.GetTempPath(), "Zametkis_" + Guid.NewGuid());
        string imagesDir = IOPath.Combine(tempDir, "images");
        Directory.CreateDirectory(imagesDir);

        var document = new NoteDocument();
        int photoIndex = 1;

        foreach (UIElement child in paintSurface.Children)
        {
            switch (child)
            {
                case TextBox tb:
                    document.Items.Add(new NoteItemData
                    {
                        Type = "Text",
                        X = Canvas.GetLeft(tb),
                        Y = Canvas.GetTop(tb),
                        Text = tb.Text
                    });
                    break;
                case Image img:
                    string sourcePath = (img.Source as BitmapImage)?.UriSource?.LocalPath ?? string.Empty;
                    string ext = string.IsNullOrEmpty(sourcePath) ? ".png" : IOPath.GetExtension(sourcePath);
                    string fileName = $"photo{photoIndex++}{ext}";
                    if (!string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath))
                        File.Copy(sourcePath, IOPath.Combine(imagesDir, fileName), overwrite: true);
                    document.Items.Add(new NoteItemData
                    {
                        Type = "Photo",
                        X = Canvas.GetLeft(img),
                        Y = Canvas.GetTop(img),
                        FileName = "images/" + fileName
                    });
                    break;
                case ShapePath strokePath when strokePath.Tag is List<Point> strokePoints:
                    document.Items.Add(new NoteItemData
                    {
                        Type = "Stroke",
                        Points = strokePoints
                    });
                    break;
            }
        }

        string json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(IOPath.Combine(tempDir, "manifest.json"), json);

        if (File.Exists(dialog.FileName))
            File.Delete(dialog.FileName);
        ZipFile.CreateFromDirectory(tempDir, dialog.FileName);
        Directory.Delete(tempDir, recursive: true);
    }

    public void LoadDocument(string path)
    {
        string tempDir = IOPath.Combine(IOPath.GetTempPath(), "Zametkis_open_" + Guid.NewGuid());
        ZipFile.ExtractToDirectory(path, tempDir);

        string manifestPath = IOPath.Combine(tempDir, "manifest.json");
        if (!File.Exists(manifestPath))
            return;

        string json = File.ReadAllText(manifestPath);
        var document = JsonSerializer.Deserialize<NoteDocument>(json);
        if (document == null)
            return;

        paintSurface.Children.Clear();

        foreach (var item in document.Items)
        {
            switch (item.Type)
            {
                case "Text":
                    paintSurface.Children.Add(CreateTextBox(item.X, item.Y, item.Text ?? string.Empty));
                    break;
                case "Photo":
                    if (!string.IsNullOrEmpty(item.FileName))
                    {
                        string imagePath = IOPath.Combine(tempDir, item.FileName);
                        if (File.Exists(imagePath))
                            paintSurface.Children.Add(CreateImage(item.X, item.Y, imagePath));
                    }
                    break;
                case "Stroke":
                    if (item.Points != null && item.Points.Count > 0)
                        paintSurface.Children.Add(CreateStrokePath(item.Points));
                    break;
            }
        }
    }
}

