using System.ComponentModel;
using Aldune.Core;

namespace Aldune.Windowing;

/// <summary>
/// La piel activa, para los DataTrigger de las plantillas
/// (<c>{Binding Card, Source={x:Static local:SkinState.Current}}</c>). Lo que es un recurso (fuentes,
/// degradado) lo pone <see cref="ThemeManager"/>; lo que decide qué piezas se ven (franja, onda,
/// relieve, botones cuadrados) se lee de aquí y cambia en vivo.
/// </summary>
public sealed class SkinState : INotifyPropertyChanged
{
    public static SkinState Current { get; } = new();

    private AppSkin _skin = AppSkin.Default;
    private bool _square;

    public AppSkin Skin
    {
        get => _skin;
        internal set { if (_skin == value) return; _skin = value; Raise(); }
    }

    /// <summary>Esquinas rectas (opción del bloque 1): los botones redondos del dock pasan a cuadrados.</summary>
    public bool Square
    {
        get => _square;
        internal set { if (_square == value) return; _square = value; Raise(); }
    }

    public SkinCard Card => _skin.Card;
    public bool Bevel => _skin.Border == SkinBorder.Bevel;
    public bool GradientTitleBar => _skin.TitleBar != SkinTitleBar.Plain;
    public bool PromptLine => _skin.PromptLine;

    public event PropertyChangedEventHandler? PropertyChanged;

    // string.Empty: cambian a la vez todas las propiedades derivadas.
    private void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
