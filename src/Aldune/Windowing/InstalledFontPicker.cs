using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// «Installed fonts…»: the families installed on the PC, each row drawn in its own face. Only the family NAME is kept; nothing is copied. Built in
/// code (a list, a search box and two buttons) with the app's palette, so it follows every look like the other windows.
/// </summary>
internal sealed class InstalledFontPicker : Window
{
    private readonly ListBox _list = new();
    private readonly TextBox _search = new();
    private readonly List<FontFamily> _all;

    public string? Chosen { get; private set; }

    public InstalledFontPicker(string? current)
    {
        Title = Strings.NoteFontPickerTitle;
        Width = 420; Height = 560; MinWidth = 320; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "AlduneGroundBrush");
        SetResourceReference(ForegroundProperty, "AlduneTextBrush");
        SetResourceReference(FontFamilyProperty, "AldunePrimaryFont");

        // Not the symbol fonts (their letters are pictures) nor the vertical-writing duplicates ("@Name").
        _all = Fonts.SystemFontFamilies
            .Where(family => !family.Source.StartsWith('@') && !IsSymbol(family))
            .OrderBy(family => family.Source, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var root = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = Strings.Accept, IsDefault = true, Padding = new Thickness(18, 6, 18, 6), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = Strings.DialogCancel, IsCancel = true, Padding = new Thickness(18, 6, 18, 6) };
        ok.Click += (_, _) => Accept();
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        _search.Margin = new Thickness(0, 0, 0, 8);
        _search.Padding = new Thickness(6, 4, 6, 4);
        AutomationProperties.SetName(_search, Strings.NoteFontPickerSearch);
        _search.TextChanged += (_, _) => Fill(current);
        DockPanel.SetDock(_search, Dock.Top);
        root.Children.Add(_search);

        AutomationProperties.SetName(_list, Strings.NoteFontPickerTitle);
        _list.SetResourceReference(BackgroundProperty, "AlduneRaisedBrush");
        _list.SetResourceReference(ForegroundProperty, "AlduneTextBrush");
        _list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetIsVirtualizing(_list, true);
        _list.MouseDoubleClick += (_, e) => { if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(_list, source) is not null) Accept(); };
        root.Children.Add(_list);
        Content = root;

        Loaded += (_, _) => { Fill(current); _search.Focus(); };
    }

    private static bool IsSymbol(FontFamily family)
    {
        try { return new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out var glyphs) && glyphs.Symbol; }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException) { return true; } // a broken font file is left out
    }

    private void Fill(string? current)
    {
        string query = _search.Text.Trim();
        _list.Items.Clear();
        ListBoxItem? selected = null;
        foreach (var family in _all.Where(f => query.Length == 0 || f.Source.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
        {
            // The family's own face in the row, so the person sees what they would get.
            var item = new ListBoxItem
            {
                Tag = family.Source,
                Padding = new Thickness(8, 5, 8, 5),
                Content = new TextBlock { Text = family.Source, FontFamily = family, FontSize = 17, TextTrimming = TextTrimming.CharacterEllipsis },
            };
            AutomationProperties.SetName(item, family.Source);
            _list.Items.Add(item);
            if (string.Equals(family.Source, current, StringComparison.OrdinalIgnoreCase)) selected = item;
        }
        if (selected is not null) { _list.SelectedItem = selected; _list.ScrollIntoView(selected); }
        else if (_list.Items.Count > 0 && query.Length > 0) _list.SelectedIndex = 0;
    }

    private void Accept()
    {
        if (_list.SelectedItem is not ListBoxItem { Tag: string name }) return;
        Chosen = name;
        DialogResult = true;
    }
}
