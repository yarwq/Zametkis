using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Zametkis.Enums;

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
    public WorkDirectory()
    {
        _currentTool = ToolsZametki.None;
        InitializeComponent();
        paintSurface.RenderTransform = translate = new TranslateTransform();
        workWindow = Window.GetWindow(this);
    }

    /// <summary>
    /// мы принимаем точку экрана при вызове функции
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    private Point screenToWorld(Point point)
    {
        var pX=(point.X/CameraZoom)+CameraX;
        var pY=(point.Y/CameraZoom)+CameraY;
        Point res = new Point(pX, pY);
        return res;
    }
     
    
    private void AddPaint(object sender, RoutedEventArgs e)
    {
        if (_currentTool != ToolsZametki.Paint)
        _currentTool = ToolsZametki.Paint;
        else 
        {
            _currentTool = ToolsZametki.None;
        }
    }

    private void AddText(object sender, RoutedEventArgs e)
    {
        if(_currentTool != ToolsZametki.Text)
        _currentTool = ToolsZametki.Text;
        else
        {
            _currentTool = ToolsZametki.None;
        }
    }

    private void WorkWithTool(object sender, MouseButtonEventArgs e)
    {
        var pos = screenToWorld(Mouse.GetPosition(Grid1));
        
        var posX = pos.X;
        var posY = pos.Y;
        switch (_currentTool)
        {
            case ToolsZametki.Text:
                TextBox tb = new TextBox();
                tb.MinWidth = 60;
                tb.MinHeight = 20;
                tb.AcceptsReturn = true;
                tb.SetValue(Canvas.TopProperty, posY );
                tb.SetValue(Canvas.LeftProperty, posX);
                paintSurface.Children.Add(tb);
                break;
            case ToolsZametki.Paint: 
                
            default:
                break;
        }
    }

    // реализовать функу чтобы увеличивалось место где курсор
    private void ZoomInAndOut(object sender, MouseWheelEventArgs e)
    {
        // Determine the direction of the zoom (in or out)
        bool zoomIn = e.Delta > 0;

        // Set the scale value based on the direction of the zoom
        CameraZoom += zoomIn ? 0.1 : -0.1;

        // Set the maximum and minimum scale values
        CameraZoom = CameraZoom < 0.1 ? 0.1 : CameraZoom;
        CameraZoom = CameraZoom > 10.0 ? 10.0 : CameraZoom;

        // Apply the scale transformation to the ItemsControl
        scaleTransform = new ScaleTransform(CameraZoom, CameraZoom);
        paintSurface.LayoutTransform = scaleTransform;
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
                    currentPoint = screenToWorld(Mouse.GetPosition(Grid1));
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
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Line line = new Line();

                    line.Stroke = SystemColors.WindowFrameBrush;
                    var worldMouse = screenToWorld(Mouse.GetPosition(Grid1));

                    line.X1 = currentPoint.X;
                    line.Y1 = currentPoint.Y;

                    line.X2 = worldMouse.X;
                    line.Y2 = worldMouse.Y;

                    currentPoint = worldMouse;

                    paintSurface.Children.Add(line);
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

    }
}

