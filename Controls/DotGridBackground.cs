using System.Windows;
using System.Windows.Media;

namespace Zametkis.Controls;

// рисует только те точки сетки, которые попадают в текущий видимый кусок мира,
// поэтому сетка не привязана к локальному размеру Canvas и остаётся "бесконечной" при панорамировании
public class DotGridBackground : FrameworkElement
{
    public double CameraX { get; set; }
    public double CameraY { get; set; }
    public double CameraZoom { get; set; } = 1.0;

    private const double GridSpacing = 24.0;
    private const double DotRadius = 1.3;

    private static readonly Brush BackgroundFill = new SolidColorBrush(Color.FromRgb(0xFB, 0xFB, 0xFD));
    private static readonly Brush DotFill = new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xDE));

    static DotGridBackground()
    {
        BackgroundFill.Freeze();
        DotFill.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0 || CameraZoom <= 0)
            return;

        dc.DrawRectangle(BackgroundFill, null, new Rect(0, 0, width, height));

        double worldLeft = CameraX;
        double worldTop = CameraY;
        double worldRight = CameraX + width / CameraZoom;
        double worldBottom = CameraY + height / CameraZoom;

        double firstX = Math.Floor(worldLeft / GridSpacing) * GridSpacing;
        double firstY = Math.Floor(worldTop / GridSpacing) * GridSpacing;
        double screenDotRadius = DotRadius * CameraZoom;

        for (double wx = firstX; wx <= worldRight; wx += GridSpacing)
        {
            double sx = (wx - CameraX) * CameraZoom;
            for (double wy = firstY; wy <= worldBottom; wy += GridSpacing)
            {
                double sy = (wy - CameraY) * CameraZoom;
                dc.DrawEllipse(DotFill, null, new Point(sx, sy), screenDotRadius, screenDotRadius);
            }
        }
    }
}
