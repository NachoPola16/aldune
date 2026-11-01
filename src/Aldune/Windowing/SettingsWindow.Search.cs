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
            hit.Element.BringIntoView();
            Flash(hit.Element);
        });
    }

    private void Flash(FrameworkElement element)
    {
        // Only text and panels take a background; anything else is just scrolled to.
        var (property, original) = element switch
        {
            TextBlock => (TextBlock.BackgroundProperty, element.GetValue(TextBlock.BackgroundProperty)),
            Control => (Control.BackgroundProperty, element.GetValue(Control.BackgroundProperty)),
            _ => (null, null),
        };
        if (property is null) return;
        element.SetResourceReference(property, "AlduneSelectedBrush");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            element.ClearValue(property);
            if (original is not null && original != DependencyProperty.UnsetValue) element.SetValue(property, original);
        };
        timer.Start();
    }
}
