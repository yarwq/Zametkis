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
using System.Windows.Shapes;
using Microsoft.Win32;
using Zametkis.Controls;
using Zametkis.Enums;
using Zametkis.Models;

namespace Zametkis;

public partial class WorkDirectory : Window
{
    private double CameraX;
    private double CameraY;
    private double CameraZoom = 1.0;
    //
    private Point _panStartScreenPoint;
    private double _panStartCameraX;
    private double _panStartCameraY;
    private ScaleTransform scaleTransform;
    private ToolsZametki _currentTool;
    private Window workWindow;
    private TranslateTransform translate;
    private List<Point>? _currentStrokePoints;
    private ShapePath? _currentStrokeShape;
    private readonly List<UIElement> _undoStack = new();
    private readonly List<UIElement> _redoStack = new();

    private static readonly (string Label, double Thickness)[] ThicknessOptions =
    {
        ("Тонкая", 1.5),
        ("Средняя", 3.5),
        ("Толстая", 7.0)
    };

    private static readonly Color[] PaintColors =
    {
        Colors.Black,
        Color.FromRgb(0xE5, 0x39, 0x35),
        Color.FromRgb(0xFB, 0x8C, 0x00),
        Color.FromRgb(0x43, 0xA0, 0x47),
        Color.FromRgb(0x19, 0x76, 0xD2),
        Color.FromRgb(0x8E, 0x24, 0xAA),
        Color.FromRgb(0x6D, 0x4C, 0x41),
    };

    private double _currentStrokeThickness = ThicknessOptions[0].Thickness;
    private Color _currentStrokeColor = Colors.Black;
    private Button? _selectedThicknessButton;
    private Border? _selectedColorSwatch;

    public WorkDirectory()
    {
        _currentTool = ToolsZametki.None;
        InitializeComponent();
        BuildPaintOptionsUI();

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
        UpdateCamera();

        Closed += WorkDirectory_Closed;
        KeyDown += WorkDirectory_KeyDown;
    }

    // когда доска закрывается (крестиком или иначе) - закрываем все WebView2-редакторы
    // и принудительно завершаем процесс целиком, иначе скрытое MainWindow не даст приложению выйти
    private void WorkDirectory_Closed(object? sender, EventArgs e)
    {
        foreach (var note in paintSurface.Children.OfType<RichTextNote>().ToList())
            note.CloseEditor();

        Application.Current.Shutdown();
    }

    private void WorkDirectory_KeyDown(object sender, KeyEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        if (e.Key == Key.S)
        {
            SaveZametki(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            Redo();
            e.Handled = true;
        }
        else if (e.Key == Key.Z)
        {
            Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y)
        {
            Redo();
            e.Handled = true;
        }
    }

    // отслеживаем только добавление целых элементов (текст/фото/мазок) - этого достаточно
    // для "базового" undo/redo; редактирование текста внутри заметки откатывается родным
    // undo браузера в самом WebView2
    private void RegisterAddedItem(UIElement element)
    {
        _undoStack.Add(element);
        _redoStack.Clear();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0)
            return;

        var item = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        paintSurface.Children.Remove(item);
        _redoStack.Add(item);
    }

    private void Redo()
    {
        if (_redoStack.Count == 0)
            return;

        var item = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        paintSurface.Children.Add(item);
        _undoStack.Add(item);
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
        PaintOptionsPanel.Visibility = _currentTool == ToolsZametki.Paint ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetToolButtonActive(Button button, bool active)
    {
        button.Style = (Style)FindResource(active ? "ToolButtonActiveStyle" : "ToolButtonStyle");
    }

    // строим панель толщины/цвета пера программно, чтобы не плодить почти одинаковые блоки в XAML
    private void BuildPaintOptionsUI()
    {
        foreach (var (label, thickness) in ThicknessOptions)
        {
            var dot = new Ellipse
            {
                Width = Math.Clamp(thickness * 2.2, 6, 18),
                Height = Math.Clamp(thickness * 2.2, 6, 18),
                Fill = Brushes.Black
            };
            var button = new Button
            {
                Content = dot,
                Width = 36,
                Height = 32,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = label,
                Style = (Style)FindResource("ToolButtonStyle")
            };
            button.Click += (_, _) => SetStrokeThickness(thickness, button);
            ThicknessPanel.Children.Add(button);
            if (Math.Abs(thickness - _currentStrokeThickness) < 0.01)
                _selectedThicknessButton = button;
        }
        if (_selectedThicknessButton != null)
            _selectedThicknessButton.Style = (Style)FindResource("ToolButtonActiveStyle");

        foreach (var color in PaintColors)
        {
            var swatch = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 6, 0),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand
            };
            swatch.MouseLeftButtonUp += (_, _) => SetStrokeColor(color, swatch);
            ColorSwatchesPanel.Children.Add(swatch);
            if (color == _currentStrokeColor)
                _selectedColorSwatch = swatch;
        }
        if (_selectedColorSwatch != null)
            _selectedColorSwatch.BorderBrush = (Brush)FindResource("AccentBrush");
    }

    private void SetStrokeThickness(double thickness, Button button)
    {
        _currentStrokeThickness = thickness;
        if (_selectedThicknessButton != null)
            _selectedThicknessButton.Style = (Style)FindResource("ToolButtonStyle");
        button.Style = (Style)FindResource("ToolButtonActiveStyle");
        _selectedThicknessButton = button;
    }

    private void SetStrokeColor(Color color, Border swatch)
    {
        _currentStrokeColor = color;
        if (_selectedColorSwatch != null)
            _selectedColorSwatch.BorderBrush = Brushes.Transparent;
        swatch.BorderBrush = (Brush)FindResource("AccentBrush");
        _selectedColorSwatch = swatch;
    }

    private async void WorkWithTool(object sender, MouseButtonEventArgs e)
    {
        var pos = Mouse.GetPosition(paintSurface);

        var posX = pos.X;
        var posY = pos.Y;
        switch (_currentTool)
        {
            case ToolsZametki.Text:
                // инструмент одноразовый: после размещения заметки сразу выходим в "None",
                // иначе следующий клик (даже чтобы поставить курсор в саму заметку) создаст ещё одну
                _currentTool = ToolsZametki.None;
                UpdateToolButtonVisuals();
                try
                {
                    var note = CreateRichTextNote(posX, posY);
                    // WebView2 нужно сначала добавить в визуальное дерево (получить родительское окно/HWND),
                    // и только потом инициализировать - иначе EnsureCoreWebView2Async не отрабатывает
                    paintSurface.Children.Add(note);
                    RegisterAddedItem(note);
                    await note.InitializeAsync(string.Empty, focusAfterInit: true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось создать текстовую заметку: {ex.Message}", "Zametkis", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                break;
            case ToolsZametki.Photo:
                var dialog = new OpenFileDialog
                {
                    Filter = "Изображения и GIF|*.png;*.jpg;*.jpeg;*.gif;*.bmp"
                };
                if (dialog.ShowDialog() == true)
                {
                    _currentTool = ToolsZametki.None;
                    UpdateToolButtonVisuals();
                    var photoNote = CreatePhotoNote(posX, posY, dialog.FileName, string.Empty, true);
                    paintSurface.Children.Add(photoNote);
                    RegisterAddedItem(photoNote);
                }
                break;
            case ToolsZametki.Paint:

            default:
                break;
        }
    }

    private static RichTextNote CreateRichTextNote(double x, double y)
    {
        var note = new RichTextNote();
        Canvas.SetLeft(note, x);
        Canvas.SetTop(note, y);
        return note;
    }

    private static PhotoNote CreatePhotoNote(double x, double y, string filePath, string caption, bool expanded)
    {
        var photoNote = new PhotoNote(filePath, caption, expanded);
        Canvas.SetLeft(photoNote, x);
        Canvas.SetTop(photoNote, y);
        return photoNote;
    }

    private static ShapePath CreateStrokePath(List<Point> points, Color color, double thickness)
    {
        return new ShapePath
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
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

        dotGridBackground.CameraX = CameraX;
        dotGridBackground.CameraY = CameraY;
        dotGridBackground.CameraZoom = CameraZoom;
        dotGridBackground.InvalidateVisual();
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
                    _currentStrokeShape = CreateStrokePath(_currentStrokePoints, _currentStrokeColor, _currentStrokeThickness);
                    paintSurface.Children.Add(_currentStrokeShape);
                    RegisterAddedItem(_currentStrokeShape);
                }
                break;
            case ToolsZametki.None:
                // панорамирование начинаем только если кликнули по пустому канвасу (его фону
                // или фону точечной сетки за ним), а не по дочернему элементу (фото, подпись,
                // заметка) - иначе клик по плашке подписи тоже захватывает мышь и "улетает" камера.
                // Захват вешаем на canvasViewport (не на сам paintSurface) - у paintSurface
                // собственная "пустая" область при панорамировании уезжает вместе с трансформом
                // и со временем перестаёт покрывать видимый вьюпорт
                if (e.ButtonState == MouseButtonState.Pressed &&
                    (e.OriginalSource == paintSurface || e.OriginalSource == dotGridBackground))
                {
                    // запоминаем точку старта и камеру на момент старта - дальше каждый раз считаем
                    // полное смещение от этой точки, а не складываем дельты по кусочкам.
                    // canvasViewport - стабильная (без трансформа) система координат самого вьюпорта,
                    // в отличие от Grid1, который включает ещё и тулбар сверху.
                    // Важно выставить эти поля ДО CaptureMouse(): захват мыши синхронно
                    // переигрывает MouseMove (WPF пересчитывает hit-test под капотом), и если
                    // он долетит до DrawME раньше, чем тут проставлены стартовые значения,
                    // камера телепортируется в точку клика (старт считается от нулей)
                    _panStartScreenPoint = Mouse.GetPosition(canvasViewport);
                    _panStartCameraX = CameraX;
                    _panStartCameraY = CameraY;
                    canvasViewport.CaptureMouse();
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
                    if (canvasViewport.IsMouseCaptured)
                    {
                        var p = e.GetPosition(canvasViewport);
                        var totalDiff = p - _panStartScreenPoint;

                        CameraX = _panStartCameraX - totalDiff.X / CameraZoom;
                        CameraY = _panStartCameraY - totalDiff.Y / CameraZoom;
                        UpdateCamera();
                    }
                }
                break;
        }
    }

    private void StopMoving(object sender, MouseButtonEventArgs e)
    {
        if (canvasViewport.IsMouseCaptured)
        {
            canvasViewport.ReleaseMouseCapture();
        }

        _currentStrokePoints = null;
        _currentStrokeShape = null;
    }

    private async void SaveZametki(object sender, RoutedEventArgs e)
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
                case RichTextNote richTextNote:
                    document.Items.Add(new NoteItemData
                    {
                        Type = "Text",
                        X = Canvas.GetLeft(richTextNote),
                        Y = Canvas.GetTop(richTextNote),
                        Text = await richTextNote.GetContentAsync()
                    });
                    break;
                case PhotoNote photoNote:
                    string sourcePath = photoNote.SourcePath;
                    string ext = string.IsNullOrEmpty(IOPath.GetExtension(sourcePath)) ? ".png" : IOPath.GetExtension(sourcePath);
                    string fileName = $"photo{photoIndex++}{ext}";
                    if (File.Exists(sourcePath))
                        File.Copy(sourcePath, IOPath.Combine(imagesDir, fileName), overwrite: true);
                    document.Items.Add(new NoteItemData
                    {
                        Type = "Photo",
                        X = Canvas.GetLeft(photoNote),
                        Y = Canvas.GetTop(photoNote),
                        FileName = "images/" + fileName,
                        Text = photoNote.CaptionText,
                        CaptionExpanded = photoNote.IsCaptionExpanded
                    });
                    break;
                case ShapePath strokePath when strokePath.Tag is List<Point> strokePoints:
                    document.Items.Add(new NoteItemData
                    {
                        Type = "Stroke",
                        Points = strokePoints,
                        Color = (strokePath.Stroke as SolidColorBrush)?.Color.ToString(),
                        Thickness = strokePath.StrokeThickness
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

    public async Task LoadDocument(string path)
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
        _undoStack.Clear();
        _redoStack.Clear();

        foreach (var item in document.Items)
        {
            switch (item.Type)
            {
                case "Text":
                    var note = CreateRichTextNote(item.X, item.Y);
                    paintSurface.Children.Add(note);
                    await note.InitializeAsync(item.Text ?? string.Empty);
                    break;
                case "Photo":
                    if (!string.IsNullOrEmpty(item.FileName))
                    {
                        string imagePath = IOPath.Combine(tempDir, item.FileName);
                        if (File.Exists(imagePath))
                            paintSurface.Children.Add(CreatePhotoNote(item.X, item.Y, imagePath, item.Text ?? string.Empty, item.CaptionExpanded));
                    }
                    break;
                case "Stroke":
                    if (item.Points != null && item.Points.Count > 0)
                    {
                        Color strokeColor = Colors.Black;
                        if (!string.IsNullOrEmpty(item.Color))
                        {
                            try { strokeColor = (Color)ColorConverter.ConvertFromString(item.Color); }
                            catch { /* старый файл без цвета или повреждённое значение - используем чёрный */ }
                        }
                        double thickness = item.Thickness > 0 ? item.Thickness : 1.5;
                        paintSurface.Children.Add(CreateStrokePath(item.Points, strokeColor, thickness));
                    }
                    break;
            }
        }
    }
}

