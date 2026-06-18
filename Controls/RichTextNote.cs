using System.Text.Json;
using System.Windows.Controls;
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
    private readonly WebView2 _webView;
    private bool _isReady;

    public RichTextNote()
    {
        Width = 380;
        Height = 260;

        _webView = new WebView2();
        Child = _webView;
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
      #toolbar { display:flex; gap:2px; padding:4px; background:#F1F1F7; border-bottom:1px solid #E4E4EC; color:#1F2333; }
      #toolbar button { border:none; background:transparent; color:#1F2333; padding:4px 8px; border-radius:6px; cursor:pointer; font-size:12px; }
      #toolbar button:hover { background:#E6E6F2; }
      #editor { padding:8px; min-height:60px; outline:none; overflow:auto; background:#FFFFFF; color:#1F2333; }
      #editor table { border-collapse: collapse; width:100%; margin:4px 0; }
      #editor td, #editor th { border:1px solid #C7C7D6; padding:4px 6px; min-width:24px; }
      #source { width:100%; box-sizing:border-box; min-height:60px; display:none; font-family: Consolas, monospace; font-size:12px; border:none; padding:8px; resize:none; outline:none; background:#FFFFFF; color:#1F2333; }
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
    <script>
      var editor = document.getElementById('editor');
      var source = document.getElementById('source');
      var srcBtn = document.getElementById('srcBtn');
      var sourceMode = false;

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
          srcBtn.classList.add('active');
        } else {
          editor.innerHTML = source.value;
          source.style.display = 'none';
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
