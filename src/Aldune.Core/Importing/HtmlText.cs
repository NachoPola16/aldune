using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Aldune.Core.Importing;

/// <summary>
/// HTML / ENML to the plain text of an Aldune note. It is a small tokenizer and not an HTML parser on purpose: the input is
/// untrusted, ENML carries a DOCTYPE, and a note only needs lines, lists, check boxes and links. Nothing here fetches or
/// resolves anything.
/// </summary>
public static partial class HtmlText
{
    [GeneratedRegex(@"<!--.*?-->|<!\[CDATA\[.*?\]\]>|<![^>]*>|<\?.*?\?>|<[^>]*>|[^<]+|<", RegexOptions.Singleline)]
    private static partial Regex Tokens();

    [GeneratedRegex(@"^<(?<close>/)?(?<name>[A-Za-z][A-Za-z0-9:_-]*)(?<attrs>[^>]*)>$", RegexOptions.Singleline)]
    private static partial Regex Tag();

    [GeneratedRegex(@"href\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex Href();

    [GeneratedRegex(@"checked\s*=\s*[""']?true", RegexOptions.IgnoreCase)]
    private static partial Regex CheckedAttr();

    // Google Keep's HTML draws its check boxes as a span with a ballot-box character; they become real to-dos before tokenizing.
    [GeneratedRegex(@"<span[^>]*class=""[^""]*bullet[^""]*""[^>]*>\s*(?<box>&#9744;|&#9745;|&#9746;|☐|☑|☒)\s*</span>", RegexOptions.IgnoreCase)]
    private static partial Regex KeepBullet();

    private static readonly HashSet<string> Blocks = new(StringComparer.OrdinalIgnoreCase)
        { "div", "p", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "pre", "table", "tr", "section", "article", "header", "footer", "hr", "body" };

    private static readonly HashSet<string> Paragraphs = new(StringComparer.OrdinalIgnoreCase)
        { "p", "h1", "h2", "h3", "h4", "h5", "h6" };

    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase) { "script", "style", "head", "title" };

    public static string ToNoteText(string html)
    {
        html = KeepBullet().Replace(html, m =>
            m.Groups["box"].Value is "&#9744;" or "☐" ? "<en-todo/>" : "<en-todo checked=\"true\"/>");
        return new Converter().Run(html);
    }

    private sealed class Converter
    {
        private readonly StringBuilder _sb = new();
        private readonly List<(bool Ordered, int Count)> _lists = [];
        private readonly Stack<(string Href, int Start)> _links = new();
        private string? _skipUntil;
        private bool _pendingSpace;
        private bool _noLead;
        private bool _pendingBlank;
        private bool _inPre;
        private string? _bulletIndent;
        private string? _bulletMarker;

        public string Run(string html)
        {
            foreach (Match token in Tokens().Matches(html))
            {
                var value = token.Value;
                if (value.StartsWith("<!--", StringComparison.Ordinal) || value.StartsWith("<?", StringComparison.Ordinal)
                    || value.StartsWith("<!", StringComparison.Ordinal))
                    continue;

                if (value[0] == '<')
                {
                    var tag = Tag().Match(value);
                    if (tag.Success) HandleTag(tag.Groups["close"].Success, tag.Groups["name"].Value.ToLowerInvariant(), tag.Groups["attrs"].Value);
                    else if (_skipUntil is null) AddText(value);
                    continue;
                }

                if (_skipUntil is null) AddText(WebUtility.HtmlDecode(value));
            }

            return Finish();
        }

        private void HandleTag(bool closing, string name, string attrs)
        {
            if (_skipUntil is not null)
            {
                if (closing && name == _skipUntil) _skipUntil = null;
                return;
            }

            if (!closing && Skipped.Contains(name) && !attrs.TrimEnd().EndsWith('/'))
            {
                _skipUntil = name;
                return;
            }

            switch (name)
            {
                case "br":
                    Flush();
                    TrimLineEnd();
                    _sb.Append('\n');
                    _pendingSpace = false;
                    _noLead = false;
                    break;
                case "ul" or "ol":
                    BreakLine();
                    if (closing) { if (_lists.Count > 0) _lists.RemoveAt(_lists.Count - 1); _bulletMarker = null; }
                    else _lists.Add((name == "ol", 0));
                    break;
                case "li":
                    BreakLine();
                    _bulletMarker = null;
                    if (!closing) StartItem();
                    break;
                case "en-todo" when !closing:
                    BreakLine();
                    // A to-do replaces the arrow of a list item (it keeps the indent), so the marker is dropped before the flush writes it.
                    var indent = _bulletIndent ?? "";
                    _bulletIndent = _bulletMarker = null;
                    Flush();
                    _sb.Append(indent);
                    _sb.Append(CheckedAttr().IsMatch(attrs) ? TaskLines.Checked : TaskLines.Unchecked).Append(' ');
                    _noLead = true;
                    _pendingSpace = false;
                    break;
                case "a":
                    if (!closing)
                    {
                        var href = Href().Match(attrs);
                        _links.Push((href.Success ? WebUtility.HtmlDecode(href.Groups["v"].Value).Trim() : "", _sb.Length));
                    }
                    else if (_links.Count > 0) EndLink(_links.Pop());
                    break;
                case "pre":
                    BreakLine();
                    _inPre = !closing;
                    if (closing) _pendingBlank = false;
                    break;
                default:
                    if (!Blocks.Contains(name)) break;
                    BreakLine();
                    if (closing && Paragraphs.Contains(name) && _sb.Length > 0) _pendingBlank = true;
                    break;
            }
        }

        private void StartItem()
        {
            var depth = Math.Max(_lists.Count, 1);
            _bulletIndent = new string(' ', 2 * (depth - 1));
            if (_lists.Count > 0 && _lists[^1].Ordered)
            {
                var (ordered, count) = _lists[^1];
                _lists[^1] = (ordered, count + 1);
                _bulletMarker = $"{count + 1}. ";
            }
            else _bulletMarker = BulletLines.Glyph + " ";
        }

        private void EndLink((string Href, int Start) link)
        {
            if (link.Href.Length == 0 || !Uri.TryCreate(link.Href, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https" or "mailto")) return;
            var shown = _sb.ToString(link.Start, _sb.Length - link.Start).Trim();
            if (shown.Length == 0) { Flush(); _sb.Append(link.Href); return; }
            if (shown.Contains(link.Href, StringComparison.OrdinalIgnoreCase)) return;
            _sb.Append(" (").Append(link.Href).Append(')');
        }

        private void AddText(string text)
        {
            foreach (var c in text)
            {
                if (_inPre)
                {
                    if (c == '\r') continue;
                    Flush();
                    _sb.Append(c);
                    continue;
                }

                if (char.IsWhiteSpace(c)) { _pendingSpace = true; continue; }

                Flush();
                if (_pendingSpace && !AtLineStart) _sb.Append(' ');
                _pendingSpace = false;
                _noLead = false;
                _sb.Append(c);
            }
        }

        private bool AtLineStart => _sb.Length == 0 || _sb[^1] == '\n' || _noLead;

        // Everything that must come before the next piece of content: a blank line after a paragraph, then the list marker of an item.
        private void Flush()
        {
            if (_pendingBlank)
            {
                _pendingBlank = false;
                if (_sb.Length > 0) { TrimLineEnd(); if (_sb[^1] != '\n') _sb.Append('\n'); _sb.Append('\n'); }
            }

            if (_bulletMarker is not null)
            {
                _sb.Append(_bulletIndent).Append(_bulletMarker);
                _bulletIndent = _bulletMarker = null;
                _noLead = true;
            }
        }

        private void BreakLine()
        {
            if (_pendingBlank) Flush();
            TrimLineEnd();
            if (_sb.Length > 0 && _sb[^1] != '\n') _sb.Append('\n');
            _pendingSpace = false;
            _noLead = false;
        }

        private void TrimLineEnd()
        {
            while (_sb.Length > 0 && _sb[^1] == ' ') _sb.Length--;
        }

        private string Finish()
        {
            var lines = _sb.ToString().Split('\n').Select(l => l.TrimEnd()).ToList();
            var result = new List<string>();
            foreach (var line in lines)
            {
                if (line.Length == 0 && (result.Count == 0 || result[^1].Length == 0)) continue;
                result.Add(line);
            }

            while (result.Count > 0 && result[^1].Length == 0) result.RemoveAt(result.Count - 1);
            return string.Join("\r\n", result);
        }
    }
}
