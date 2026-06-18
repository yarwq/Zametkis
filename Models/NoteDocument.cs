using System.Windows;

namespace Zametkis.Models;

public class NoteItemData
{
    public string Type { get; set; } = string.Empty; // "Text", "Photo", "Stroke", "Page"
    public double X { get; set; }
    public double Y { get; set; }
    public string? Text { get; set; } // для "Text" - содержимое, для "Photo" - подпись, для "Page" - заголовок
    public string? FileName { get; set; }
    public bool CaptionExpanded { get; set; } = true;
    public List<Point>? Points { get; set; } // абсолютные координаты точек мазка, для типа "Stroke"
    public string? Color { get; set; } // цвет мазка ("#FFRRGGBB"), для типа "Stroke"
    public double Thickness { get; set; } = 1.5; // толщина мазка, для типа "Stroke"
    public List<NoteItemData>? Children { get; set; } // вложенный контент, для типа "Page"
    public double CameraX { get; set; } // запомненная камера вложенной страницы, для типа "Page"
    public double CameraY { get; set; }
    public double CameraZoom { get; set; } = 1.0;
}

public class NoteDocument
{
    public List<NoteItemData> Items { get; set; } = new();
}
