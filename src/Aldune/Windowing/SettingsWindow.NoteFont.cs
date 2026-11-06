using System.IO;
using System.Windows;
using System.Windows.Controls;
using Aldune.Core;
using Aldune.Resources;
using Microsoft.Win32;

namespace Aldune.Windowing;

// The note font setting (Settings > Notes). The choices are the four named by role, the family picked from the installed fonts and one imported
// file; the font file lives in the app's own `fonts` folder and, like the choice, never leaves this device. Spec: docs/specs/2026-11-04-note-font-choice-design.md.
public partial class SettingsWindow
{
    private bool _loadingNoteFont;

    private string FontsDirectory => Path.Combine(AppDataDirectory, "fonts");

    private void PopulateNoteFont()
    {
        _loadingNoteFont = true;
        try
        {
            NoteFontCombo.Items.Clear();
            void Add(string label, NoteFontChoice choice)
            {
                var item = new ComboBoxItem { Content = label, Tag = choice };
                NoteFontCombo.Items.Add(item);
                if (_settings.NoteFontChoice == choice) NoteFontCombo.SelectedItem = item;
            }
            Add(Strings.NoteFontSystem, NoteFontChoice.System);
            Add(Strings.NoteFontSerif, NoteFontChoice.Serif);
            Add(Strings.NoteFontEasy, NoteFontChoice.EasyToRead);
            Add(Strings.NoteFontMono, NoteFontChoice.Monospace);
            bool hasImported = ImportedFontExists();
            if (hasImported) Add(Strings.NoteFontImportedItem(_settings.NoteFontName ?? ""), NoteFontChoice.Imported);
            if (!string.IsNullOrWhiteSpace(_settings.NoteFontName) && _settings.NoteFontChoice == NoteFontChoice.Installed)
                Add(Strings.NoteFontInstalledItem(_settings.NoteFontName), NoteFontChoice.Installed);
            // A stored choice that cannot be shown (the imported file went missing, or an unknown value) is shown as System.
            if (NoteFontCombo.SelectedItem is null) NoteFontCombo.SelectedIndex = 0;
            NoteFontRemoveButton.Visibility = hasImported ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _loadingNoteFont = false; }
        UpdateNoteFontLookNotice();
    }

    /// <summary>Tells, under the font choice, when the look in use keeps its own font and so hides the choice (terminal, XP, notebooks).</summary>
    private void UpdateNoteFontLookNotice() =>
        NoteFontLookNoticeBox.Visibility = NoteFonts.LookKeepsItsFont(ThemeManager.Skin.NoteFont) ? Visibility.Visible : Visibility.Collapsed;

    private bool ImportedFontExists() =>
        !string.IsNullOrEmpty(_settings.NoteFontFile) && _settings.NoteFontFile == Path.GetFileName(_settings.NoteFontFile)
        && File.Exists(Path.Combine(FontsDirectory, _settings.NoteFontFile));

    private void ApplyNoteFont()
    {
        _settingsService.Save(_settings);
        ThemeManager.ApplyNoteFontSettings(_settings, FontsDirectory);
        // The open notes take the new font by themselves: their body uses a DynamicResource.
        ThemeManager.ApplySkin(Application.Current, ThemeManager.Skin);
        PopulateNoteFont();
    }

    private void OnNoteFontSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingNoteFont || NoteFontCombo.SelectedItem is not ComboBoxItem { Tag: NoteFontChoice choice } || choice == _settings.NoteFontChoice) return;
        _settings.NoteFontChoice = choice;
        // The names only mean something for their own choice: a built-in choice drops them (and the imported file stays until it is removed or replaced).
        if (choice is not (NoteFontChoice.Installed or NoteFontChoice.Imported)) _settings.NoteFontName = null;
        ApplyNoteFont();
    }

    private void OnNoteFontInstalledClick(object sender, RoutedEventArgs e)
    {
        var dialog = new InstalledFontPicker(_settings.NoteFontChoice == NoteFontChoice.Installed ? _settings.NoteFontName : null) { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Chosen)) return;
        _settings.NoteFontChoice = NoteFontChoice.Installed;
        _settings.NoteFontName = dialog.Chosen;
        ApplyNoteFont();
    }

    private void OnNoteFontImportClick(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Filter = "TrueType / OpenType|*.ttf;*.otf", CheckFileExists = true, Multiselect = false };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            // The size is checked before reading: a huge file is never loaded into memory.
            var info = new FileInfo(picker.FileName);
            if (info.Length > FontFileInspector.MaxBytes) { Reject(FontFileProblem.TooLarge); return; }
            var data = File.ReadAllBytes(picker.FileName);
            var inspected = FontFileInspector.Inspect(data, picker.FileName);
            if (!inspected.IsValid) { Reject(inspected.Problem); return; }

            // One imported font at a time: the new file replaces the old one. Written under a fresh name so the platform's font cache
            // never holds on to a changed file, and only after it is in place is the old one removed.
            Directory.CreateDirectory(FontsDirectory);
            var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(picker.FileName).ToLowerInvariant();
            File.WriteAllBytes(Path.Combine(FontsDirectory, fileName), data);
            var previous = _settings.NoteFontFile;
            _settings.NoteFontChoice = NoteFontChoice.Imported;
            _settings.NoteFontName = inspected.Family;
            _settings.NoteFontFile = fileName;
            ApplyNoteFont();
            DeleteImportedFile(previous);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Reject(FontFileProblem.NotAFont);
        }

        void Reject(FontFileProblem problem) => AppDialog.Show(this, problem switch
        {
            FontFileProblem.Extension => Strings.NoteFontRejectedExtension,
            FontFileProblem.TooLarge => Strings.NoteFontRejectedTooLarge,
            _ => Strings.NoteFontRejectedInvalid
        }, Strings.NoteFontLabel, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnNoteFontRemoveClick(object sender, RoutedEventArgs e)
    {
        var file = _settings.NoteFontFile;
        if (_settings.NoteFontChoice == NoteFontChoice.Imported) _settings.NoteFontChoice = NoteFontChoice.System;
        _settings.NoteFontFile = null;
        if (_settings.NoteFontChoice != NoteFontChoice.Installed) _settings.NoteFontName = null;
        ApplyNoteFont();
        DeleteImportedFile(file);
    }

    private void DeleteImportedFile(string? file)
    {
        if (string.IsNullOrEmpty(file) || file != Path.GetFileName(file) || file == _settings.NoteFontFile) return;
        try { File.Delete(Path.Combine(FontsDirectory, file)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* a font held open by the loader goes on the next removal */ }
    }
}
