using System.Media;
using Aldune.Core;
using Aldune.Interop;

namespace Aldune.Windowing;

/// <summary>
/// Reproduce lo que <see cref="SoundPlan"/> ha decidido. Nunca bloquea ni lanza: un .wav que no se puede leer, o
/// un equipo sin tarjeta de sonido, se queda en silencio y se apunta en el registro de diagnóstico.
/// </summary>
internal static class AppSounds
{
    // SoundPlayer carga el .wav en segundo plano: mientras suena hay que conservar una referencia.
    private static SoundPlayer? _player;

    public static void Play(SoundAction action)
    {
        try
        {
            switch (action.Kind)
            {
                case SoundActionKind.System:
                    SystemSoundOf(action.System)?.Play();
                    break;
                case SoundActionKind.File:
                    _player?.Dispose();
                    _player = new SoundPlayer(action.Path!);
                    _player.Play();
                    break;
            }
        }
        catch (Exception ex)
        {
            DockDiagnostics.Write("sonidos", $"no se pudo reproducir: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static SystemSound? SystemSoundOf(SoundChoice choice) => choice switch
    {
        SoundChoice.Asterisk => SystemSounds.Asterisk,
        SoundChoice.Exclamation => SystemSounds.Exclamation,
        SoundChoice.Beep => SystemSounds.Beep,
        SoundChoice.Hand => SystemSounds.Hand,
        SoundChoice.Question => SystemSounds.Question,
        _ => null,
    };
}
