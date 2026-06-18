using System.Windows;

namespace Zametkis.Models;

public class NoteItemData
{
    public string Type { get; set; } = string.Empty; // "Text", "Photo", "Stroke"
    public double X { get; set; }
    public double Y { get; set; }
    public string? Text { get; set; } // для "Text" - содержимое, для "Photo" - подпись
    public string? FileName { get; set; }
    public bool CaptionExpanded { get; set; } = true;
    public List<Point>? Points { get; set; } // абсолютные координаты точек мазка, для типа "Stroke"
}

public class NoteDocument
{
    public List<NoteItemData> Items { get; set; } = new();
}
