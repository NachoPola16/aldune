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
    private const int MaxTextLength = MarkdownLive.MaxNoteLength;
    // Second strike of a bold piece: far enough to read as weight, close enough not to blur the glyph.
    private const double BoldOffset = 0.7;
    private const double ItalicSkewDegrees = -11;
    private const double MarkerOpacity = 0.55;

    private readonly TextBox _box;
    private static readonly DependencyPropertyDescriptor _fontSize = DependencyPropertyDescriptor.FromProperty(Control.FontSizeProperty, typeof(TextBox));
    private readonly EventHandler _fontSizeChanged;
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

        box.TextChanged += (_, _) => { box.UpdateLayout(); Changed(); };
        box.SelectionChanged += (_, _) => Changed();
        box.SizeChanged += (_, _) => Changed();
        box.GotKeyboardFocus += (_, _) => Changed();
        box.LostKeyboardFocus += (_, _) => Changed();
        box.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => Changed()));
        // Zoom changes the font size without touching the text: nothing else would tell the layer to draw again. The descriptor keeps the
        // box alive for as long as the handler is subscribed, so it is only subscribed while the box is loaded.
        _fontSizeChanged = (_, _) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(Refresh));
        box.Loaded += (_, _) => { _fontSize.RemoveValueChanged(box, _fontSizeChanged); _fontSize.AddValueChanged(box, _fontSizeChanged); };
        box.Unloaded += (_, _) => _fontSize.RemoveValueChanged(box, _fontSizeChanged);
    }

    // The drawn pieces repeat the text box's own text: a screen reader must read the box once, never these copies. WPF has no per-element
    // switch for this, so the layer gets a peer that is neither a control nor content and shows no children.
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new HiddenPeer(this);

    private sealed class HiddenPeer(FrameworkElement owner) : System.Windows.Automation.Peers.FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsContentElementCore() => false;
        protected override bool IsControlElementCore() => false;
        protected override List<System.Windows.Automation.Peers.AutomationPeer>? GetChildrenCore() => null;
        protected override string GetClassNameCore() => nameof(MarkdownLiveLayer);
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

    private bool _settlePending;

    // Something that changes what is drawn happened. Draw now (so a typed letter never waits a frame), and once more when the layout has
    // settled: during a layout pass the box can still answer for the previous layout, which lost a wrapped line at some zoom levels.
    private void Changed()
    {
        Refresh();
        if (_settlePending) return;
        _settlePending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => { _settlePending = false; Refresh(); }));
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
        bool drew = false;
        if (_enabled && text.Length > 0 && text.Length <= MaxTextLength)
        {
            try
            {
                if (!ReferenceEquals(_parsedText, text) && _parsedText != text)
                {
                    _spans = MarkdownLive.Parse(text);
                    _parsedText = text;
                }
                DrawVisibleLines(text);
                drew = Children.Count > 0;
            }
            catch (Exception ex)
            {
                // Never leave the person with invisible text: if drawing fails, the box shows its own glyphs again and the layer stays off.
                AppLog.Write("markdown", "live markdown layer failed, using the plain text box: " + ex.Message);
                Children.Clear();
                _enabled = false;
            }
        }
        // The box's glyphs go transparent only once something is drawn in their place (never on a box that has not been measured yet).
        IsDrawing = drew;
        var wanted = drew ? Brushes.Transparent : _ink;
        if (!ReferenceEquals(_box.Foreground, wanted)) _box.Foreground = wanted;
    }

    private void DrawVisibleLines(string text)
    {
        var starts = VisualLineStarts(text);
        if (starts.Count == 0) return;

        int selStart = -1, selEnd = -1;
        if (_box.IsSelectionActive && _box.SelectionLength > 0) { selStart = _box.SelectionStart; selEnd = selStart + _box.SelectionLength; }

        int spanIndex = 0;
        for (int i = 0; i < starts.Count; i++)
        {
            int start = starts[i];
            int end = i + 1 < starts.Count ? starts[i + 1] : text.Length;
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
                if (to > from) DrawPiece(text, from, to, span.Style, lineRect.Top, lineRect.Height, selStart, selEnd);
            }
        }
    }

    /// <summary>
    /// The index of the first character of each visual line in view, found by asking the box what is at the left edge of each line band.
    /// The box's own line-index methods are not used: with some looks (a taller line height than the font, the terminal looks) they
    /// repeat a line and report a last visible line that does not exist, which drew lines twice and left the last ones out.
    /// </summary>
    // A fraction of a line, not a whole one: at a zoom the real line pitch is a fraction of a pixel off the box's reported height, and a
    // whole-line step eventually jumps over a line. The same line found twice is skipped by index.
    private const double LineProbeStep = 0.6;

    private List<int> VisualLineStarts(string text)
    {
        var starts = new List<int>();
        double viewport = _box.ViewportHeight > 0 ? _box.ViewportHeight : _box.ActualHeight;
        if (viewport <= 0) return starts;

        int firstChar = _box.GetCharacterIndexFromPoint(new Point(0, 1), snapToText: true);
        if (firstChar < 0) return starts;
        var firstRect = _box.GetRectFromCharacterIndex(firstChar);
        double height = firstRect.IsEmpty ? 0 : firstRect.Height;
        if (height < 4) return starts;

        // Walk down the viewport one line band at a time (from the middle of the first line), and one band past it for a line half in view.
        double y = firstRect.Top + height / 2;
        int previous = -1;
        for (int guard = 0; y < viewport + height && guard < 4000; guard++, y += height * LineProbeStep)
        {
            int index = _box.GetCharacterIndexFromPoint(new Point(0, y), snapToText: true);
            if (index < 0 || index <= previous) continue;
            // The same index twice means the band is below the last line: snapping returns the end of the text.
            if (index >= text.Length && previous >= 0 && starts.Count > 0 && _box.GetRectFromCharacterIndex(index).Top <= _box.GetRectFromCharacterIndex(previous).Top) break;
            starts.Add(index);
            previous = index;
        }
        return starts;
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
            // No fixed line height here: a box with one (the terminal and notebook looks) puts the text at the top of its line, while a text
            // block with the same height puts it lower, so each piece is natural height and sits on the top of the box's line instead.
            IsHitTestVisible = false,
        };
        TextOptions.SetTextFormattingMode(block, TextOptions.GetTextFormattingMode(_box));
        TextOptions.SetTextRenderingMode(block, TextOptions.GetTextRenderingMode(_box));
        SetLeft(block, x);
        SetTop(block, top);
        return block;
    }
}
