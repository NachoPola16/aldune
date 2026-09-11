namespace Fanote.Core;

/// <summary>
/// Los tres atajos rápidos del selector de recordatorio de <c>NoteWindow</c>. Puro y parametrizado
/// por <c>now</c> (en vez de leer <c>DateTimeOffset.Now</c> internamente) para poder testear "Esta
/// noche" a los dos lados del límite de las 20:00 sin depender del reloj real — ver
/// docs/superpowers/specs/2026-09-11-fanote-reminders-design.md.
///
/// Todo se calcula en la hora local de <paramref name="now"/> (mismo <c>Offset</c> que trae), no en
/// UTC: quien llama (NoteWindow) trabaja con <c>DateTimeOffset.Now</c>, y la conversión a UTC pasa a
/// ocurrir una sola vez, en <see cref="NotesRepository.SetReminder"/>.
/// </summary>
public static class ReminderPresets
{
    private const int TonightHour = 20;
    private const int TomorrowMorningHour = 9;

    public static DateTimeOffset InOneHour(DateTimeOffset now) => now.AddHours(1);

    /// <summary>Hoy a las 20:00 si aún no han dado las 20:00; si ya son las 20:00 en punto o más
    /// tarde, mañana a las 20:00 — nunca propone una hora que ya pasó hoy.</summary>
    public static DateTimeOffset Tonight(DateTimeOffset now)
    {
        var eightPm = new DateTimeOffset(now.Year, now.Month, now.Day, TonightHour, 0, 0, now.Offset);
        return now < eightPm ? eightPm : eightPm.AddDays(1);
    }

    /// <summary>El día siguiente a las 9:00, sin importar la hora de <paramref name="now"/>.</summary>
    public static DateTimeOffset TomorrowMorning(DateTimeOffset now)
    {
        var tomorrow = now.AddDays(1);
        return new DateTimeOffset(tomorrow.Year, tomorrow.Month, tomorrow.Day, TomorrowMorningHour, 0, 0, now.Offset);
    }
}
