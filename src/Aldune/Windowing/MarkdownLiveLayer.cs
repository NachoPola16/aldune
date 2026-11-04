using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aldune.Core;
using Aldune.Interop;

namespace Aldune.Windowing;

/// <summary>
/// Live Markdown for the note body without replacing the editor. The text box stays the editor (caret, selection, undo, tasks, accessibility,
/// the text that is saved and synced), but its glyphs are made transparent and this canvas, laid over it, draws the visible lines again with
/// the styles <see cref="MarkdownLive"/> finds. Every piece is placed at the x the text box itself reports for its first character and keeps
/// the box's font, so the caret never drifts: emphasis is drawn without changing a glyph's advance width (bold is the same glyphs struck twice,
/// italic the same glyphs skewed). The selected text is left to the text box, which paints it in the selection colours.
/// </summary>
internal sealed class MarkdownLiveLayer : Canvas
{
    // Past this the note is drawn by the text box alone: the scanner and the layer are meant for notes, not for a pasted file.
    private const int MaxTextLength = 150_000;
    // Second strike of a bold piece: far enough to read as weight, close enough not to blur the glyph.
    private const double BoldOffset = 0.7;
    private const double ItalicSkewDegrees = -11;
    private const double MarkerOpacity = 0.38;

    private readonly TextBox _box;
    private Brush _ink = Brushes.Black;
    private bool _enabled;
    private string? _parsedText;
    private IReadOnlyList<MarkdownSpan> _spans = [];
    private bool _building;

    internal MarkdownLiveLayer(TextBox box)
    {
        _box = box;
        IsHitTestVisible = false;
        ClipToBounds = true;
        SnapsToDevicePixels = false;

        box.TextChanged += (_, _) => { box.UpdateLayout(); Refresh(); };
        box.SelectionChanged += (_, _) => Refresh();
        box.SizeChanged += (_, _) => Refresh();
        box.GotKeyboardFocus += (_, _) => Refresh();
        box.LostKeyboardFocus += (_, _) => Refresh();
        box.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => Refresh()));
        // Zoom changes the font size without touching the text: nothing else would tell the layer to draw again.
        var size = DependencyPropertyDescriptor.FromProperty(Control.FontSizeProperty, typeof(TextBox));
        size.AddValueChanged(box, (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(Refresh)));
    }

    /// <summary>True while the layer is the one drawing the text (the box's own glyphs are then transparent).</summary>
    internal bool IsDrawing { get; private set; }

    internal bool Enabled
    {
        get => _enabled;
        set { if (_enabled == value) return; _enabled = value; Refresh(); }
    }

    /// <summary>The ink of the note: used for the drawn text, and for the text box itself whenever the layer is not drawing.</summary>
    internal void SetInk(Brush ink)
    {
        _ink = ink;
        Refresh();
    }

    internal void Refresh()
    {
        if (_building) return;
        _building = true;
        try { Rebuild(); }
        finally { _building = false; }
    }

    private void Rebuild()
    {
        Children.Clear();
        string text = _box.Text;
        bool draw = _enabled && text.Length > 0 && text.Length <= MaxTextLength;
        // Foreground is the one thing shared with the rest of the window: it flips only when the answer changes.
        var wanted = draw ? Brushes.Transparent : _ink;
        if (!ReferenceEquals(_box.Foreground, wanted)) _box.Foreground = wanted;
        IsDrawing = draw;
        if (!draw) return;

        try
        {
            if (!ReferenceEquals(_parsedText, text) && _parsedText != text)
            {
                _spans = MarkdownLive.Parse(text);
                _parsedText = text;
            }
            DrawVisibleLines(text);
        }
        catch (Exception ex)
        {
            // Never leave the person with invisible text: if drawing fails, the box shows its own glyphs again and the layer stays off.
            AppLog.Write("markdown", "live markdown layer failed, using the plain text box: " + ex.Message);
            Children.Clear();
            IsDrawing = false;
            _enabled = false;
            _box.Foreground = _ink;
        }
    }

    private void DrawVisibleLines(string text)
    {
        int firstLine = _box.GetFirstVisibleLineIndex();
        int lastLine = _box.GetLastVisibleLineIndex();
        if (firstLine < 0 || lastLine < firstLine) return;

        int selStart = -1, selEnd = -1;
        if (_box.IsSelectionActive && _box.SelectionLength > 0) { selStart = _box.SelectionStart; selEnd = selStart + _box.SelectionLength; }

        int spanIndex = 0;
        for (int line = firstLine; line <= lastLine; line++)
        {
            int start = _box.GetCharacterIndexFromLineIndex(line);
            int length = _box.GetLineLength(line);
            if (start < 0 || length <= 0) continue;
            int end = start + length;
            while (end > start && (text[end - 1] == '\r' || text[end - 1] == '\n')) end--;
            if (end <= start) continue;

            var lineRect = _box.GetRectFromCharacterIndex(start);
            if (lineRect.IsEmpty) continue;

            spanIndex = FirstSpanAtOrAfter(start, spanIndex);
            for (int s = spanIndex; s < _spans.Count && _spans[s].Start < end; s++)
            {
                var span = _spans[s];
                int from = Math.Max(span.Start, start);
                int to = Math.Min(span.Start + span.Length, end);
                DrawPiece(text, from, to, span.Style, lineRect.Top, lineRect.Height, selStart, selEnd);
            }
        }
    }

    // Spans are sorted and contiguous: walk back to the one that covers `start` (the cursor `from` only ever moves forward, line to line).
    private int FirstSpanAtOrAfter(int start, int from)
    {
        int s = Math.Min(from, _spans.Count - 1);
        if (s < 0) return 0;
        while (s > 0 && _spans[s].Start > start) s--;
        while (s < _spans.Count - 1 && _spans[s].Start + _spans[s].Length <= start) s++;
        return s;
    }

    private void DrawPiece(string text, int from, int to, MarkdownStyle style, double top, double height, int selStart, int selEnd)
    {
        // Tabs are not drawn: the box leaves its own gap and the next piece is placed from the box's x. A selected stretch is drawn in the
        // selection text colour, as the box would (it cannot: its glyphs are transparent).
        bool Selected(int i) => selStart >= 0 && i >= selStart && i < selEnd;
        int a = from;
        while (a < to)
        {
            if (text[a] == '	') { a++; continue; }
            bool selected = Selected(a);
            int b = a;
            while (b < to && text[b] != '	' && Selected(b) == selected) b++;
            DrawRun(text.Substring(a, b - a), a, style, top, height, selected);
            a = b;
        }
    }

    private void DrawRun(string piece, int charIndex, MarkdownStyle style, double top, double height, bool selected)
    {
        var rect = _box.GetRectFromCharacterIndex(charIndex);
        if (rect.IsEmpty) return;
        double x = rect.Left;
        bool marker = style.HasFlag(MarkdownStyle.Marker);
        bool bold = !marker && style.HasFlag(MarkdownStyle.Bold);
        bool italic = !marker && style.HasFlag(MarkdownStyle.Italic);

        var ink = selected ? _box.SelectionTextBrush ?? _ink : _ink;
        var block = NewBlock(piece, x, top, height, ink);
        if (marker) block.Opacity = MarkerOpacity;
        if (italic)
        {
            block.RenderTransformOrigin = new Point(0, 0.8);
            block.RenderTransform = new SkewTransform(ItalicSkewDegrees, 0);
        }
        var decorations = new TextDecorationCollection();
        if (!marker && style.HasFlag(MarkdownStyle.Strike)) decorations.Add(TextDecorations.Strikethrough[0]);
        if (!marker && style.HasFlag(MarkdownStyle.Link)) decorations.Add(TextDecorations.Underline[0]);
        if (decorations.Count > 0) block.TextDecorations = decorations;

        if (!marker && style.HasFlag(MarkdownStyle.Code))
        {
            block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var chip = new Border
            {
                Width = block.DesiredSize.Width + 2,
                Height = Math.Max(0, height - 4),
                Background = _ink,
                Opacity = 0.11,
                CornerRadius = new CornerRadius(3),
                IsHitTestVisible = false,
            };
            SetLeft(chip, x - 1);
            SetTop(chip, top + 2);
            Children.Add(chip);
        }

        Children.Add(block);
        if (bold)
        {
            var strike = NewBlock(piece, x + BoldOffset, top, height, ink);
            if (italic)
            {
                strike.RenderTransformOrigin = new Point(0, 0.8);
                strike.RenderTransform = new SkewTransform(ItalicSkewDegrees, 0);
            }
            if (decorations.Count > 0) strike.TextDecorations = decorations;
            Children.Add(strike);
        }
    }

    private TextBlock NewBlock(string text, double x, double top, double height, Brush ink)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = _box.FontFamily,
            FontSize = _box.FontSize,
            FontStyle = _box.FontStyle,
            FontWeight = _box.FontWeight,
            FontStretch = _box.FontStretch,
            Foreground = ink,
            TextWrapping = TextWrapping.NoWrap,
            // The box's own line metrics: a fixed line height with the text placed in it, so the baseline lands where the box's glyphs would be.
            LineHeight = TextBlock.GetLineHeight(_box),
            LineStackingStrategy = TextBlock.GetLineStackingStrategy(_box),
            IsHitTestVisible = false,
        };
        TextOptions.SetTextFormattingMode(block, TextOptions.GetTextFormattingMode(_box));
        TextOptions.SetTextRenderingMode(block, TextOptions.GetTextRenderingMode(_box));
        SetLeft(block, x);
        SetTop(block, top);
        return block;
    }
}
