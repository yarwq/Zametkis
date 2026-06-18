using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;

namespace Zametkis.Controls;

// заметка с HTML-редактируемым содержимым: жирный/курсив/списки/таблицы через мини-тулбар внутри страницы,
// плюс кнопка "HTML" для прямого редактирования разметки (в т.ч. таблиц)
//
// Ctrl+S/Ctrl+Z и т.п. отдельно прокидывать не нужно: WPF-обёртка WebView2 сама форвардит
// "акселераторные" сочетания клавиш (с Ctrl/Alt) в обычный bubbling KeyDown, см.
// WebView2Base.CoreWebView2Controller_AcceleratorKeyPressed -> OnPreviewKeyDown -> KeyDown
public class RichTextNote : Border
{
    private const double MinNoteWidth = 240;
    private const double MinNoteHeight = 160;
    private const double HandleSize = 8.0;

    private readonly WebView2 _webView;
    private readonly Canvas _handleCanvas;
    private bool _isReady;

    private enum ResizeDir { TL, T, TR, L, R, BL, B, BR }

    public RichTextNote()
    {
        Width = 380;
        Height = 260;
        MinWidth = MinNoteWidth;
        MinHeight = MinNoteHeight;

        _webView = new WebView2();

        var contentBorder = new Border
        {
            BorderBrush = (Brush)Application.Current.FindResource("AccentBrush"),
            BorderThickness = new Thickness(1.5),
            Background = Brushes.White,
            Child = _webView
        };

        _handleCanvas = new Canvas();

        var handleTemplate = BuildHandleTemplate();

        foreach (ResizeDir dir in Enum.GetValues<ResizeDir>())
        {
            var thumb = new Thumb
            {
                Width = HandleSize,
                Height = HandleSize,
                Cursor = CursorForDir(dir),
                Template = handleTemplate,
                Tag = dir
            };
            thumb.DragDelta += (_, e) => OnDrag(dir, e);
            _handleCanvas.Children.Add(thumb);
        }

        var grid = new Grid();
        grid.Children.Add(contentBorder);
        grid.Children.Add(_handleCanvas);
        Child = grid;

        SizeChanged += (_, _) => PositionHandles();
        Loaded += (_, _) => PositionHandles();
    }

    private static ControlTemplate BuildHandleTemplate()
    {
        var template = new ControlTemplate(typeof(Thumb));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.BackgroundProperty, Brushes.White);
        factory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x6C, 0x63, 0xFF)));
        factory.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
        template.VisualTree = factory;
        return template;
    }

    private void PositionHandles()
    {
        double w = ActualWidth;
        double h = ActualHeight;
        double hs = HandleSize;

        foreach (Thumb thumb in _handleCanvas.Children)
        {
            var dir = (ResizeDir)thumb.Tag;

            double left = dir is ResizeDir.TL or ResizeDir.L or ResizeDir.BL ? -hs / 2
                        : dir is ResizeDir.TR or ResizeDir.R or ResizeDir.BR ? w - hs / 2
                        : w / 2 - hs / 2;

            double top  = dir is ResizeDir.TL or ResizeDir.T or ResizeDir.TR ? -hs / 2
                        : dir is ResizeDir.BL or ResizeDir.B or ResizeDir.BR ? h - hs / 2
                        : h / 2 - hs / 2;

            Canvas.SetLeft(thumb, left);
            Canvas.SetTop(thumb, top);
        }
    }

    private static Cursor CursorForDir(ResizeDir dir) => dir switch
    {
        ResizeDir.TL or ResizeDir.BR => Cursors.SizeNWSE,
        ResizeDir.TR or ResizeDir.BL => Cursors.SizeNESW,
        ResizeDir.T  or ResizeDir.B  => Cursors.SizeNS,
        _                            => Cursors.SizeWE
    };

    private void OnDrag(ResizeDir dir, DragDeltaEventArgs e)
    {
        double dx = e.HorizontalChange;
        double dy = e.VerticalChange;

        if (dir is ResizeDir.TR or ResizeDir.R or ResizeDir.BR)
            Width = Math.Max(MinNoteWidth, Width + dx);

        if (dir is ResizeDir.TL or ResizeDir.L or ResizeDir.BL)
        {
            double newWidth = Math.Max(MinNoteWidth, Width - dx);
            double left = Canvas.GetLeft(this);
            Canvas.SetLeft(this, (double.IsNaN(left) ? 0 : left) + Width - newWidth);
            Width = newWidth;
        }

        if (dir is ResizeDir.BL or ResizeDir.B or ResizeDir.BR)
            Height = Math.Max(MinNoteHeight, Height + dy);

        if (dir is ResizeDir.TL or ResizeDir.T or ResizeDir.TR)
        {
            double newHeight = Math.Max(MinNoteHeight, Height - dy);
            double top = Canvas.GetTop(this);
            Canvas.SetTop(this, (double.IsNaN(top) ? 0 : top) + Height - newHeight);
            Height = newHeight;
        }
    }

    public async Task InitializeAsync(string initialHtml, bool focusAfterInit = false)
    {
        await _webView.EnsureCoreWebView2Async();
        _webView.CoreWebView2.NavigateToString(EditorHtml);

        var tcs = new TaskCompletionSource();
        void OnNavigationCompleted(object? sender, object e) => tcs.TrySetResult();
        _webView.NavigationCompleted += OnNavigationCompleted;
        await tcs.Task;
        _webView.NavigationCompleted -= OnNavigationCompleted;

        await SetContentAsync(initialHtml);
        _isReady = true;

        if (focusAfterInit)
        {
            _webView.Focus();
            await _webView.CoreWebView2.ExecuteScriptAsync("editor.focus()");
        }
    }

    public async Task SetContentAsync(string html)
    {
        string encoded = JsonSerializer.Serialize(html ?? string.Empty);
        await _webView.CoreWebView2.ExecuteScriptAsync($"setContent({encoded})");
    }

    public async Task<string> GetContentAsync()
    {
        if (!_isReady)
            return string.Empty;

        string raw = await _webView.CoreWebView2.ExecuteScriptAsync("getContent()");
        return JsonSerializer.Deserialize<string>(raw) ?? string.Empty;
    }

    // явно закрывает дочерний WebView2-контроллер, чтобы его процессы (msedgewebview2.exe)
    // не оставались висеть после закрытия приложения
    public void CloseEditor()
    {
        _webView.Dispose();
    }

    private const string EditorHtml = """
    <!DOCTYPE html>
    <html>
    <head>
    <meta charset="utf-8">
    <meta name="color-scheme" content="light only">
    <style>
      html, body { margin:0; padding:0; font-family: "Segoe UI", sans-serif; font-size: 13px; color:#1F2333; background:#FFFFFF; height:100%; }
      body { display:flex; flex-direction:column; }
      #toolbar { flex:0 0 auto; display:flex; gap:2px; padding:4px; background:#F1F1F7; border-bottom:1px solid #E4E4EC; color:#1F2333; }
      #toolbar button { border:none; background:transparent; color:#1F2333; padding:4px 8px; border-radius:6px; cursor:pointer; font-size:12px; }
      #toolbar button:hover { background:#E6E6F2; }
      #editor { flex:1 1 auto; padding:8px; outline:none; overflow:auto; background:#FFFFFF; color:#1F2333; }
      #editor table { border-collapse: collapse; width:100%; margin:4px 0; }
      #editor td, #editor th { border:1px solid #C7C7D6; padding:4px 6px; min-width:24px; }
      #source { flex:1 1 auto; width:100%; box-sizing:border-box; display:none; font-family: Consolas, monospace; font-size:12px; border:none; padding:8px; resize:none; outline:none; background:#FFFFFF; color:#1F2333; }
      #hints { flex:0 0 auto; display:none; background:#F7F7FB; border-top:1px solid #E4E4EC; padding:8px 10px; max-height:150px; overflow:auto; }
      #hints .hints-title { font-weight:600; margin-bottom:6px; }
      #hints .hint-row { margin-bottom:6px; line-height:1.5; }
      #hints code { background:#EFEFF5; padding:1px 4px; border-radius:4px; font-family:Consolas, monospace; }
      #hints button.hint-insert { border:none; background:#6C63FF; color:#FFFFFF; padding:2px 8px; border-radius:6px; font-size:11px; cursor:pointer; margin-left:6px; }
      #hints button.hint-insert:hover { background:#5A52E0; }
    </style>
    </head>
    <body>
      <div id="toolbar">
        <button onclick="cmd('bold')" title="Жирный"><b>Ж</b></button>
        <button onclick="cmd('italic')" title="Курсив"><i>К</i></button>
        <button onclick="cmd('underline')" title="Подчёркнутый"><u>Ч</u></button>
        <button onclick="cmd('insertUnorderedList')" title="Маркированный список">• список</button>
        <button onclick="cmd('insertOrderedList')" title="Нумерованный список">1. список</button>
        <button onclick="insertTable()" title="Вставить таблицу">Таблица</button>
        <button onclick="toggleSource()" title="Редактировать HTML напрямую" id="srcBtn">HTML</button>
      </div>
      <div id="editor" contenteditable="true"></div>
      <textarea id="source" spellcheck="false"></textarea>
      <div id="hints">
        <div class="hints-title">Подсказка: как менять вид через код</div>
        <div class="hint-row">
          Цвет фона ячейки таблицы: добавьте в тег <code>style="background:#FFF3CD;"</code>
          <button class="hint-insert" onclick="insertExample('cell')">Вставить пример</button>
        </div>
        <div class="hint-row">
          Цвет и размер текста: оберните текст в <code>&lt;span style="color:#1976D2; font-size:20px;"&gt;...&lt;/span&gt;</code>
          <button class="hint-insert" onclick="insertExample('text')">Вставить пример</button>
        </div>
        <div class="hint-row">
          Объединить две ячейки в строке: добавьте в первую <code>colspan="2"</code>, а вторую удалите
        </div>
      </div>
    <script>
      var editor = document.getElementById('editor');
      var source = document.getElementById('source');
      var srcBtn = document.getElementById('srcBtn');
      var hints = document.getElementById('hints');
      var sourceMode = false;

      function insertExample(kind) {
        var snippets = {
          cell: '<table><tr><td style="background:#FFF3CD;">Цветная ячейка</td><td>Обычная ячейка</td></tr></table><br>',
          text: '<span style="color:#1976D2; font-size:20px;">Крупный синий текст</span><br>'
        };
        var snippet = snippets[kind];
        if (!snippet) return;
        if (sourceMode) {
          var pos = source.selectionStart >= 0 ? source.selectionStart : source.value.length;
          source.value = source.value.slice(0, pos) + snippet + source.value.slice(pos);
          source.focus();
        } else {
          editor.focus();
          document.execCommand('insertHTML', false, snippet);
        }
      }

      function cmd(name) { editor.focus(); document.execCommand(name, false, null); }

      function insertTable() {
        editor.focus();
        var html = '<table><tr><td>&nbsp;</td><td>&nbsp;</td><td>&nbsp;</td></tr>'
          + '<tr><td>&nbsp;</td><td>&nbsp;</td><td>&nbsp;</td></tr></table><br>';
        document.execCommand('insertHTML', false, html);
      }

      function toggleSource() {
        sourceMode = !sourceMode;
        if (sourceMode) {
          source.value = formatHtml(editor.innerHTML);
          editor.style.display = 'none';
          source.style.display = 'block';
          hints.style.display = 'block';
          srcBtn.classList.add('active');
        } else {
          editor.innerHTML = source.value;
          source.style.display = 'none';
          hints.style.display = 'none';
          editor.style.display = 'block';
          srcBtn.classList.remove('active');
        }
      }

      function getContent() {
        if (sourceMode) editor.innerHTML = source.value;
        return formatHtml(editor.innerHTML);
      }

      function setContent(html) { editor.innerHTML = html; }

      // делает HTML читаемым: каждый тег на своей строке, с отступами по уровню вложенности
      function formatHtml(html) {
        var container = document.createElement('div');
        container.innerHTML = html;
        return formatChildren(container, 0).trim();
      }

      function formatChildren(node, depth) {
        var indent = '  '.repeat(depth);
        var result = '';
        for (var i = 0; i < node.childNodes.length; i++) {
          var child = node.childNodes[i];
          if (child.nodeType === Node.TEXT_NODE) {
            var text = child.textContent.replace(/\s+/g, ' ').trim();
            if (text) result += indent + text + '\n';
          } else if (child.nodeType === Node.ELEMENT_NODE) {
            var tag = child.tagName.toLowerCase();
            var attrs = '';
            for (var j = 0; j < child.attributes.length; j++) {
              attrs += ' ' + child.attributes[j].name + '="' + child.attributes[j].value + '"';
            }
            if (child.childNodes.length === 0) {
              result += indent + '<' + tag + attrs + '></' + tag + '>\n';
            } else {
              result += indent + '<' + tag + attrs + '>\n';
              result += formatChildren(child, depth + 1);
              result += indent + '</' + tag + '>\n';
            }
          }
        }
        return result;
      }
    </script>
    </body>
    </html>
    """;
}
