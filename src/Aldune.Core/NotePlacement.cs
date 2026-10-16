namespace Aldune.Core;

/// <summary>
/// Coordenadas y dimensiones de una ventana de nota guardadas para recordar su posición en el
/// escritorio (estilo post-it libre) — una por nota y por pantalla (<see cref="MonitorKey"/>, el
/// <c>MonitorInfo.DeviceName</c> del monitor donde estaba la nota al cerrarla). Por pantalla y no
/// una sola global: abrir la nota desde el dock de un monitor no debe traerla desde donde se dejó
/// en otro.
///
/// <see cref="ManualHeight"/>: el alto lo fijó el usuario arrastrando el borde. Sin la marca, una nota
/// encogida por debajo de su texto era indistinguible de una ajustada sola y al reabrirla volvía al alto
/// del texto.
/// </summary>
public sealed record NotePlacement(Guid NoteId, string MonitorKey, double Left, double Top, double Width, double Height, bool ManualHeight = false);
