using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Aldune.Interop;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Color editor that follows Aldune's dark chrome instead of opening the unrelated WinForms
/// color dialog. The HEX field is the precise input; the RGB sliders provide a quick visual way
/// to tune the same value. The preview and note share an adaptive WCAG-readable foreground.
/// </summary>
public partial class CustomColorWindow : Window
{
    private bool _updating;
    private string _selectedColor = "#F5E3B3";

    private CustomColorWindow(string initialColor)
    {
        InitializeComponent();
        NativeMethods.CloakUntilFirstFrame(this);
        _selectedColor = NormalizeColor(initialColor) ?? _selectedColor;
        Loaded += (_, _) => UpdateFromColor(_selectedColor);
    }

    public static string? Show(Window owner, string initialColor)
    {
        var dialog = new CustomColorWindow(initialColor)
        {
            Owner = owner,
            Topmost = owner.Topmost
        };

        return dialog.ShowDialog() == true ? dialog._selectedColor : null;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeMethods.ApplyRoundedCorners(new WindowInteropHelper(this).Handle);
    }

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating || !IsLoaded) return;

        var color = Color.FromRgb(
            (byte)Math.Round(RedSlider.Value),
            (byte)Math.Round(GreenSlider.Value),
            (byte)Math.Round(BlueSlider.Value));
        UpdateFromColor(ToHex(color), updateSliders: false);
    }

    private void OnRgbTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updating || !IsLoaded ||
            !byte.TryParse(RedValue.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var red) ||
            !byte.TryParse(GreenValue.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var green) ||
            !byte.TryParse(BlueValue.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var blue))
            return;

        UpdateFromColor(ToHex(Color.FromRgb(red, green, blue)));
    }

    private void OnSpectrumChanged(object? sender, EventArgs e)
    {
        if (_updating || !IsLoaded) return;
        UpdateFromColor(ToHex(Spectrum.SelectedColor));
    }

    private void OnHexChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updating || !IsLoaded) return;

        var normalized = NormalizeColor(HexBox.Text);
        if (normalized is null)
        {
            HexHint.Text = Strings.CustomColorInvalidHex;
            ContrastHint.Text = string.Empty;
            ApplyButton.IsEnabled = false;
            return;
        }

        UpdateFromColor(normalized);
    }

    private void UpdateFromColor(string color, bool updateSliders = true)
    {
        var mediaColor = (Color)ColorConverter.ConvertFromString(color)!;
        _selectedColor = ToHex(mediaColor);
        bool readable = NoteColorPalette.IsReadableCustom(_selectedColor);

        _updating = true;
        try
        {
            PreviewPanel.Background = new SolidColorBrush(mediaColor);
            PreviewSwatch.Background = new SolidColorBrush(mediaColor);
            Spectrum.SelectedColor = mediaColor;
            PreviewHex.Text = _selectedColor;
            HexBox.Text = _selectedColor;
            var previewInk = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                NoteColorPalette.ForegroundFor(_selectedColor))!);
            PreviewLabel.Foreground = previewInk;
            PreviewHex.Foreground = previewInk;
            PreviewSwatch.BorderBrush = previewInk;

            if (updateSliders)
            {
                RedSlider.Value = mediaColor.R;
                GreenSlider.Value = mediaColor.G;
                BlueSlider.Value = mediaColor.B;
            }

            RedValue.Text = mediaColor.R.ToString(CultureInfo.InvariantCulture);
            GreenValue.Text = mediaColor.G.ToString(CultureInfo.InvariantCulture);
            BlueValue.Text = mediaColor.B.ToString(CultureInfo.InvariantCulture);
            HexHint.Text = string.Empty;
            ContrastHint.Text = readable
                ? Strings.CustomColorReadable
                : Strings.CustomColorContrastError;
            ContrastHint.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(readable ? "#A9C99A" : "#E8A0A0")!);
            ApplyButton.IsEnabled = readable;
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (!NoteColorPalette.IsReadableCustom(_selectedColor)) return;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ApplyButton.IsEnabled)
        {
            DialogResult = true;
            e.Handled = true;
        }
    }

    private static string? NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (!text.StartsWith('#')) text = $"#{text}";
        if (text.Length != 7) return null;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(text)!;
            return ToHex(color);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

}
