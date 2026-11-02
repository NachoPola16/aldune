using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// The search box of Settings. The index is read from what is on the pages (every label, checkbox text and button), so a new option
/// is found without anyone remembering to register it; matching is <see cref="FuzzySearch"/>.
/// </summary>
public partial class SettingsWindow
{
    private sealed record SettingEntry(string Page, string Text, FrameworkElement Element);

    private static readonly string[] SearchablePages = ["General", "Appearance", "Notes", "Sounds", "Colors", "Dock", "Sync", "Help"];
    private const int MaxSearchResults = 9;

    private List<SettingEntry> _searchHits = [];

    /// <summary>Ctrl+F (from anywhere in Settings) puts the cursor in the search box.</summary>
    private bool TryHandleSearchShortcut(KeyEventArgs e)
    {
        if (e.Key != Key.F || Keyboard.Modifiers != ModifierKeys.Control) return false;
        SettingsSearchBox.Focus();
        SettingsSearchBox.SelectAll();
        e.Handled = true;
        return true;
    }

    private List<SettingEntry> BuildSearchIndex()
    {
        var entries = new List<SettingEntry>();
        var seen = new HashSet<string>();
        foreach (var page in SearchablePages)
        {
            if (FindName(page + "Page") is not FrameworkElement root || PageNav(page).Visibility != Visibility.Visible) continue;
            Collect(root, page);
        }
        return entries;

        void Collect(DependencyObject node, string page)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                string? text = child switch
                {
                    TextBlock block => block.Text,
                    ContentControl { Content: string content } => content,
                    _ => null,
                };
                text = text?.Trim();
                if (child is FrameworkElement element && text is { Length: >= 3 } && seen.Add(page + "\n" + text))
                    entries.Add(new SettingEntry(page, text, element));
                Collect(child, page);
            }
        }
    }

    private void OnSettingsSearchChanged(object sender, TextChangedEventArgs e)
    {
        string query = SettingsSearchBox.Text;
        bool searching = !string.IsNullOrWhiteSpace(query);
        SettingsSearchResults.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        SettingsNavList.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
        SettingsSearchResults.Children.Clear();
        _searchHits = [];
        if (!searching) return;

        _searchHits = FuzzySearch.Rank(BuildSearchIndex(), entry => entry.Text, query).Take(MaxSearchResults).ToList();
        if (_searchHits.Count == 0)
        {
            var none = new TextBlock { Text = Strings.SettingsSearchNone, Margin = new Thickness(8, 6, 8, 0), TextWrapping = TextWrapping.Wrap };
            none.SetResourceReference(TextBlock.ForegroundProperty, "AlduneMutedTextBrush");
            SettingsSearchResults.Children.Add(none);
            return;
        }

        foreach (var hit in _searchHits)
        {
            var title = new TextBlock { Text = hit.Text, TextTrimming = TextTrimming.CharacterEllipsis };
            var where = new TextBlock { Text = PageTitle(hit.Page), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
            where.SetResourceReference(TextBlock.ForegroundProperty, "AlduneMutedTextBrush");
            var content = new StackPanel { Width = 150 };
            content.Children.Add(title);
            content.Children.Add(where);
            var button = new Button
            {
                Content = content, Tag = hit, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = (Style)FindResource("SearchResultButtonStyle"),
            };
            button.Click += (_, _) => GoToSetting(hit);
            SettingsSearchResults.Children.Add(button);
        }
    }

    private void OnSettingsSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _searchHits.Count > 0)
        {
            GoToSetting(_searchHits[0]);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SettingsSearchBox.Text.Length > 0)
        {
            // First Esc clears the search; the next one closes Settings, as always.
            SettingsSearchBox.Clear();
            e.Handled = true;
        }
    }

    private static string PageTitle(string page) => page switch
    {
        "General" => Strings.SettingsPageGeneral,
        "Appearance" => Strings.SettingsPageAppearance,
        "Notes" => Strings.SettingsPageNotes,
        "Sounds" => Strings.SettingsPageSounds,
        "Colors" => Strings.SettingsPageColors,
        "Dock" => Strings.SettingsPageDock,
        "Sync" => Strings.SettingsPageSync,
        _ => Strings.SettingsPageHelp,
    };

    /// <summary>Opens the page of the setting, scrolls it into view and marks it for a moment so the eye finds it.</summary>
    private void GoToSetting(SettingEntry hit)
    {
        SettingsSearchBox.Clear();
        ShowPage(hit.Page);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            Reveal(hit.Element);
            Flash(hit.Element);
        });
    }

    /// <summary>
    /// Scrolls the page so that <paramref name="element"/> sits a little below the top. BringIntoView only moves the page the minimum and left
    /// the line stuck against the edge of the window (or, called before the page had been laid out, did nothing: the notice that points to an
    /// option arrived at its page and not at its line).
    /// </summary>
    private void Reveal(FrameworkElement element)
    {
        PageScroller.UpdateLayout();
        if (PageScroller.Content is not UIElement content || !element.IsVisible) { element.BringIntoView(); return; }
        double top = element.TransformToVisual(content).Transform(new Point(0, 0)).Y;
        // The page never grows for this (it did, to bring the last settings up, and the window visibly stretched): a setting at the very end
        // stays where the end of the page puts it, which the page's own bottom margin keeps clear of the edge.
        PageScroller.ScrollToVerticalOffset(Math.Min(Math.Max(0, top - RevealMargin), PageScroller.ScrollableHeight));
    }

    private const double RevealMargin = 90;

    /// <summary>Opens <paramref name="page"/> and, when an element name is given, scrolls to it and lights it (a notice elsewhere sends the person to the option it talks about).</summary>
    internal void ShowOption(string page, string? elementName)
    {
        ShowPage(page);
        if (elementName is null || FindName(elementName) is not FrameworkElement element) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            Reveal(element);
            Flash(element);
        });
    }

    /// <summary>
    /// Lights the setting for a moment: a frame and a wash in the accent colour drawn over it, fading out. It is an adorner, not a background:
    /// the switches and rows draw their own template, so a background set on them was never seen.
    /// </summary>
    private void Flash(FrameworkElement element)
    {
        ClearFlash();
        var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(element);
        if (layer is null) return;
        var adorner = new FlashAdorner(element);
        layer.Add(adorner);
        _flashLayer = layer;
        _flashAdorner = adorner;
        var fade = new System.Windows.Media.Animation.DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(1600)))
        {
            BeginTime = TimeSpan.FromMilliseconds(500),
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn },
        };
        fade.Completed += (_, _) => { layer.Remove(adorner); if (ReferenceEquals(_flashAdorner, adorner)) _flashAdorner = null; };
        adorner.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private System.Windows.Documents.AdornerLayer? _flashLayer;
    private System.Windows.Documents.Adorner? _flashAdorner;

    /// <summary>The light of the previous jump goes out when another starts or the page changes (an adorner of a hidden page is drawn where it was).</summary>
    private void ClearFlash()
    {
        if (_flashAdorner is not null) _flashLayer?.Remove(_flashAdorner);
        _flashAdorner = null;
    }

    private sealed class FlashAdorner : System.Windows.Documents.Adorner
    {
        public FlashAdorner(UIElement target) : base(target) => IsHitTestVisible = false;

        /// <summary>The border that paints the card of the element (most of its width, with a background), if it has one.</summary>
        private static Border? CardOf(DependencyObject element)
        {
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(element);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node is Border { Background: not null, ActualWidth: > 0 } border && element is FrameworkElement host
                    && border.ActualWidth >= host.ActualWidth * 0.8 && border.ActualHeight >= host.ActualHeight * 0.5
                    && !ReferenceEquals(border, element))
                    return border;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) queue.Enqueue(VisualTreeHelper.GetChild(node, i));
            }
            return null;
        }

        protected override void OnRender(DrawingContext context)
        {
            var accent = (Brush)FindResource("AlduneAccentBrush");
            var wash = accent.CloneCurrentValue();
            wash.Opacity = 0.22;
            var pen = new Pen(accent, 2);
            // Over what is drawn, not over the element: a row (the list of looks) has a margin around its card that is not part of it, and the
            // frame over the whole element left a gap outside the card.
            var size = AdornedElement.RenderSize;
            var area = new System.Windows.Rect(0, 0, size.Width, size.Height);
            if (CardOf(AdornedElement) is { } card && card.IsVisible && card.ActualWidth > 0)
            {
                var origin = card.TransformToAncestor(AdornedElement).Transform(new System.Windows.Point(0, 0));
                area = new System.Windows.Rect(origin.X, origin.Y, card.ActualWidth, card.ActualHeight);
            }
            context.DrawRoundedRectangle(wash, pen, new System.Windows.Rect(area.X + 1, area.Y + 1, Math.Max(0, area.Width - 2), Math.Max(0, area.Height - 2)), 5, 5);
        }
    }
}
