using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Aldune.Core;
using Aldune.Core.Importing;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Importing notes from another app: pick a source, pick the file or folder, see what would be added (and what would not, and why), then
/// import. The preview writes nothing; the import only ever adds notes. Reading and writing run off the UI thread so a big export does not
/// freeze the window.
/// </summary>
public partial class ImportNotesWindow : Window
{
    private enum SourceKind { Sticky, Keep, Evernote, Folder }

    private readonly NotesRepository _repository;
    private readonly AppCoordinator _coordinator;
    private ImportPlan? _plan;
    private int _imported;
    private bool _busy;
    private bool _finished;
    private int _generation;

    private ImportNotesWindow(NotesRepository repository, AppCoordinator coordinator)
    {
        _repository = repository;
        _coordinator = coordinator;
        InitializeComponent();
        ThemeManager.ScaleWindowToText(this);
        NativeMethods.CloakUntilFirstFrame(this);

        SourceBox.Items.Add(Strings.ImportSourceSticky);
        SourceBox.Items.Add(Strings.ImportSourceKeep);
        SourceBox.Items.Add(Strings.ImportSourceEvernote);
        SourceBox.Items.Add(Strings.ImportSourceFolder);
        SourceBox.SelectedIndex = 0;
        Loaded += (_, _) => SourceBox.Focus();
    }

    /// <summary>Opens the dialog and returns how many notes were imported (0 if none), so the caller can refresh its lists.</summary>
    internal static int Show(Window owner, NotesRepository repository, AppCoordinator coordinator)
    {
        var dialog = new ImportNotesWindow(repository, coordinator) { Owner = owner, Topmost = owner.Topmost };
        dialog.ShowDialog();
        return dialog._imported;
    }

    private SourceKind Kind => (SourceKind)Math.Max(SourceBox.SelectedIndex, 0);

    private IImportSource CurrentSource => Kind switch
    {
        SourceKind.Sticky => new StickyNotesSource(),
        SourceKind.Keep => new KeepTakeoutSource(),
        SourceKind.Evernote => new EvernoteSource(),
        _ => new TextFolderSource(),
    };

    private void OnSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || _busy) return;
        var kind = Kind;
        HelpText.Text = kind switch
        {
            SourceKind.Sticky => Strings.ImportHelpSticky,
            SourceKind.Keep => Strings.ImportHelpKeep,
            SourceKind.Evernote => Strings.ImportHelpEvernote,
            _ => Strings.ImportHelpFolder,
        };
        UseWindowsButton.Visibility = kind == SourceKind.Sticky ? Visibility.Visible : Visibility.Collapsed;
        ChooseFileButton.Visibility = kind == SourceKind.Folder ? Visibility.Collapsed : Visibility.Visible;
        ChooseFolderButton.Visibility = kind is SourceKind.Keep or SourceKind.Folder ? Visibility.Visible : Visibility.Collapsed;
        ChooseFileButton.Content = kind == SourceKind.Keep ? Strings.ImportChooseZip : Strings.ImportChooseFile;
        ResetPreview(Strings.ImportNothingChosen);
    }

    private void OnUseWindowsClick(object sender, RoutedEventArgs e)
    {
        var path = StickyNotesSource.DefaultPath();
        if (path is null)
        {
            ResetPreview(Strings.ImportFailureText(ImportFailure.NotFound, sticky: true));
            return;
        }

        _ = LoadAsync(path);
    }

    private void OnChooseFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { CheckFileExists = true };
        switch (Kind)
        {
            case SourceKind.Sticky: dialog.Filter = Strings.ImportFilterSticky; break;
            case SourceKind.Keep: dialog.Filter = Strings.ZipFileFilter; break;
            default: dialog.Filter = Strings.ImportFilterEvernote; break;
        }

        if (dialog.ShowDialog(this) == true) _ = LoadAsync(dialog.FileName);
    }

    private void OnChooseFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.ImportFolderDialogTitle };
        if (dialog.ShowDialog(this) == true) _ = LoadAsync(dialog.FolderName);
    }

    /// <summary>Reads the source (no writes) and shows the plan. Also the entry point for tests and probes.</summary>
    internal async Task LoadAsync(string path)
    {
        if (_busy) return;
        var generation = ++_generation;
        var source = CurrentSource;
        var sticky = Kind == SourceKind.Sticky;
        PathText.Text = ShortenPath(path);
        PathText.ToolTip = path;
        ResetPreview(Strings.ImportReading, keepPath: true);
        SetBusy(true);
        try
        {
            var (preview, plan) = await Task.Run(() =>
            {
                var read = source.Read(path);
                var existing = _repository.GetByState(NoteState.Active)
                    .Concat(_repository.GetByState(NoteState.Archived))
                    .Concat(_repository.GetByState(NoteState.Trashed));
                return (read, NoteImporter.Plan(read, existing));
            });
            if (generation != _generation) return;
            ShowPreview(preview, plan, sticky);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SummaryText.Text = Strings.ImportFailureText(ImportFailure.Unreadable, sticky);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowPreview(ImportPreview preview, ImportPlan plan, bool sticky)
    {
        DetailsPanel.Children.Clear();
        if (preview.Failure != ImportFailure.None)
        {
            SummaryText.Text = Strings.ImportFailureText(preview.Failure, sticky);
            DetailsScroller.Visibility = Visibility.Collapsed;
            ColoursText.Visibility = Visibility.Collapsed;
            ImportButton.IsEnabled = false;
            return;
        }

        _plan = plan;
        SummaryText.Text = plan.ToCreate.Count > 0 ? Strings.ImportWillAdd(plan.ToCreate.Count) : Strings.ImportNothingToAdd;

        if (plan.Duplicates > 0) AddDetail(Strings.ImportAlreadyThere(plan.Duplicates));
        foreach (var group in preview.Skipped.GroupBy(s => s.Reason))
        {
            int count = group.Count();
            AddDetail(group.Key switch
            {
                ImportSkipReason.Empty => Strings.ImportSkippedEmpty(count),
                ImportSkipReason.TooLarge => Strings.ImportSkippedTooLarge(count),
                ImportSkipReason.NotText => Strings.ImportSkippedNotText(count),
                ImportSkipReason.LimitReached => Strings.ImportSkippedLimit(count),
                _ => Strings.ImportSkippedUnreadable(count),
            });
        }

        if (preview.AttachmentsIgnored > 0)
            AddDetail(Strings.ImportAttachmentsIgnored(preview.AttachmentsIgnored, preview.NotesWithAttachments), warning: true);

        DetailsScroller.Visibility = DetailsPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ColoursText.Visibility = plan.ToCreate.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ImportButton.IsEnabled = plan.ToCreate.Count > 0;
        if (ImportButton.IsEnabled) ImportButton.Focus();
    }

    /// <summary>A long path keeps its start (the drive) and its end (the file name) and loses the middle, so what was chosen is still recognisable.</summary>
    internal static string ShortenPath(string path, int max = 64)
    {
        if (path.Length <= max) return path;
        int head = Math.Min(12, max / 4);
        return string.Concat(path.AsSpan(0, head), "…", path.AsSpan(path.Length - (max - head - 1)));
    }

    private void AddDetail(string text, bool warning = false)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
        block.SetResourceReference(TextBlock.FontSizeProperty, "AlduneTextSmall");
        block.SetResourceReference(TextBlock.ForegroundProperty, warning ? "AlduneAttentionBrush" : "AlduneTextSoftBrush");
        DetailsPanel.Children.Add(block);
    }

    private void ResetPreview(string message, bool keepPath = false)
    {
        _plan = null;
        if (!keepPath)
        {
            PathText.Text = "";
            PathText.ToolTip = null;
        }

        SummaryText.Text = message;
        DetailsPanel.Children.Clear();
        DetailsScroller.Visibility = Visibility.Collapsed;
        ColoursText.Visibility = Visibility.Collapsed;
        ImportButton.IsEnabled = false;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        SourceBox.IsEnabled = !busy;
        UseWindowsButton.IsEnabled = ChooseFileButton.IsEnabled = ChooseFolderButton.IsEnabled = !busy;
        if (busy) ImportButton.IsEnabled = false;
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        if (_plan is not { ToCreate.Count: > 0 } plan || _busy) return;
        var colors = _coordinator.ImportColorPolicy;
        SetBusy(true);
        SummaryText.Text = Strings.ImportImporting;
        try
        {
            var result = await Task.Run(() => NoteImporter.Run(plan, _repository, colors));
            _imported += result.Created;
            SummaryText.Text = Strings.ImportDone(result.Created, result.Failed);
            _plan = null;
            _finished = true;
            DetailsPanel.Children.Clear();
            DetailsScroller.Visibility = Visibility.Collapsed;
            ColoursText.Visibility = Visibility.Collapsed;
            CloseButton.Content = Strings.ImportClose;
            CloseButton.Style = (Style)FindResource("PrimaryColorDialogButtonStyle");
            ImportButton.Visibility = Visibility.Collapsed;
            SourceBox.IsEnabled = false;
            UseWindowsButton.IsEnabled = ChooseFileButton.IsEnabled = ChooseFolderButton.IsEnabled = false;
            CloseButton.Focus();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SummaryText.Text = Strings.UnexpectedErrorMessage(ex.Message);
            SetBusy(false);
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e) =>
        NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        // Esc closes unless the work is running (a half-finished import cannot be cancelled cleanly).
        if (e.Key == Key.Escape && !_busy)
        {
            Close();
            e.Handled = true;
        }
        // Enter on a button presses it; elsewhere it runs the import when there is one ready.
        else if (e.Key == Key.Enter && Keyboard.FocusedElement is not Button && ImportButton.IsEnabled && !_finished)
        {
            OnImportClick(ImportButton, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}
