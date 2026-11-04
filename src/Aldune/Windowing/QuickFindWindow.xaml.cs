using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>One row of the palette: what the template binds to. The ranking and the locked-note rules live in <see cref="QuickFind"/>.</summary>
internal sealed class QuickFindRow
{
    public QuickFindRow(QuickFindHit hit)
    {
        Hit = hit;
        // A locked note without a visible name shows the same «Protected note» as the dock and Manage notes.
        Title = hit.Title.Length > 0 ? hit.Title : Strings.ProtectedNote;
        Snippet = hit.Snippet;
        TagsText = string.Join("  ", hit.Tags.Select(tag => "#" + tag));
    }

    public QuickFindHit Hit { get; }
    public string Title { get; }
    public string Snippet { get; }
    public string TagsText { get; }
    public bool IsLocked => Hit.IsLocked;
    public bool HasSnippet => Snippet.Length > 0;

    // What a screen reader says for the row: the title, and that it is locked when it is (the padlock is only a drawing). A locked note with
    // no visible name already is «Protected note», which must not be said twice.
    public string AutomationName => IsLocked && Hit.Title.Length > 0 ? Title + ", " + Strings.ProtectedNote : Title;
}

/// <summary>
/// The quick-find palette: a small borderless search window that the global shortcut opens over whatever program is in front. The keyboard
/// stays in the text box the whole time; Up and Down move the selection of the list, which is how a launcher feels (no Tab, no mouse).
/// It closes the moment it loses the activation, so clicking anywhere else dismisses it.
/// </summary>
internal sealed partial class QuickFindWindow : Window
{
    private readonly IReadOnlyList<Note> _notes;
    private readonly Action<QuickFindHit> _open;
    private readonly Func<QuickFindHit, bool> _copy;
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(70) };
    // Past this many notes a keystroke waits a moment for the next one before filtering (the tolerant search scans every body).
    private const int DebounceFrom = 300;
    // The height of the palette with a full list (search field, eight rows, footer): the window is placed as if it had it, so it does not
    // jump about while the number of results changes.
    private const double FullHeight = 560;
    private bool _closing;

    internal QuickFindWindow(IReadOnlyList<Note> notes, Action<QuickFindHit> open, Func<QuickFindHit, bool> copy)
    {
        InitializeComponent();
        _notes = notes;
        _open = open;
        _copy = copy;
        ThemeManager.ScaleWindowToText(this);
        NativeMethods.CloakUntilFirstFrame(this);
        _debounce.Tick += (_, _) => { _debounce.Stop(); Refilter(); };
        Refilter();
    }

    /// <summary>The rows being shown, best first (the probe and the tests read them).</summary>
    internal IReadOnlyList<QuickFindRow> Rows => Results.Items.Cast<QuickFindRow>().ToList();

    internal QuickFindRow? Selected => Results.SelectedItem as QuickFindRow;

    internal string FooterMessage => FooterText.Text;

    /// <summary>Opens on the screen where the pointer is, centred, and takes the keyboard even though another program has it.</summary>
    internal void ShowCentredOnCursorMonitor()
    {
        var area = MonitorEnumerator.MonitorContainingCursor()?.WorkArea
            ?? new WorkingArea(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
        WindowStartupLocation = WindowStartupLocation.Manual;
        double height = Math.Min(FullHeight * ThemeManager.TextFactor, area.Height * 0.9);
        Left = area.X + (area.Width - Width) / 2;
        Top = area.Y + (area.Height - height) / 2;

        bool animate = SystemParameters.ClientAreaAnimation;
        // Reduced motion (Windows «Show animations» off): it simply appears.
        if (animate) Opacity = 0;
        Show();
        if (animate)
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(110)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
            });
            Opacity = 1;
        }
        NativeMethods.ForceActivate(this);
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    // --- Filtering ---------------------------------------------------------------------------------------------------------------------

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        Placeholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_notes.Count > DebounceFrom) { _debounce.Stop(); _debounce.Start(); }
        else Refilter();
    }

    private void Refilter()
    {
        var rows = QuickFind.Search(_notes, SearchBox.Text).Select(hit => new QuickFindRow(hit)).ToList();
        Results.ItemsSource = rows;
        bool any = rows.Count > 0;
        Results.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        NoneText.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        ResetFooter();
        if (any) Select(0);
    }

    private void ResetFooter() => FooterText.Text = Strings.QuickFindFooter;

    // --- Keys --------------------------------------------------------------------------------------------------------------------------

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                CloseNow();
                break;
            case Key.Down:
                MoveSelection(1);
                break;
            case Key.Up:
                MoveSelection(-1);
                break;
            case Key.PageDown:
                MoveSelection(5);
                break;
            case Key.PageUp:
                MoveSelection(-5);
                break;
            case Key.Enter:
                Choose(copy: (Keyboard.Modifiers & ModifierKeys.Control) != 0);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    /// <summary>Moves the selection, stopping at the ends: a list this short wrapping round would lose the person's place.</summary>
    internal void MoveSelection(int delta)
    {
        if (Results.Items.Count == 0) return;
        Select(Math.Clamp(Math.Max(0, Results.SelectedIndex) + delta, 0, Results.Items.Count - 1));
        ResetFooter();
    }

    private void Select(int index)
    {
        Results.SelectedIndex = index;
        if (Results.SelectedItem is { } item) Results.ScrollIntoView(item);
    }

    /// <summary>Enter: open the selected note and close. Ctrl+Enter: copy its text (never a protected note's) and close.</summary>
    internal void Choose(bool copy)
    {
        if (Selected is not { } row) return;

        if (!copy)
        {
            // Close first: a protected note asks for its password, and the prompt must not open under this window or be dismissed with it.
            CloseNow();
            _open(row.Hit);
            return;
        }

        if (_copy(row.Hit)) CloseNow();
        else FooterText.Text = Strings.QuickFindLockedCopy;   // stays open: the note could not be copied, and the person may want to open it
    }

    private void OnResultsMouseUp(object sender, MouseButtonEventArgs e)
    {
        var item = (e.OriginalSource as DependencyObject) is { } source ? ItemsControl.ContainerFromElement(Results, source) as ListBoxItem : null;
        if (item?.DataContext is not QuickFindRow) return;
        item.IsSelected = true;
        Choose(copy: false);
    }

    // --- Closing -----------------------------------------------------------------------------------------------------------------------

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // Click outside (or Alt+Tab): the palette is a passing question, not a window to keep around.
        if (!_closing) CloseNow();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Also when the coordinator closes it (the shortcut pressed again): Deactivated then must not close a second time.
        _closing = true;
        base.OnClosing(e);
    }

    private void CloseNow()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }
}
