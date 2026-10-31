namespace Aldune.Core;

/// <summary>
/// Qué hace una nota con sus tareas hechas, decidido POR NOTA y no por dispositivo. Un ajuste local no sirve en una lista
/// compartida (la de la compra): si un PC borra lo hecho, el borrado se sincroniza a todos los demás. Viaja con la nota como
/// campo opcional del contenido cifrado (<c>completedTasks</c>, solo se escribe si no es <see cref="Default"/>), igual que
/// <c>pinnedToTop</c>; el formato de sync sigue siendo el 4. Los valores son números en el JSON y en la base: no reordenar.
/// </summary>
public enum CompletedTasksPolicy
{
    /// <summary>Cada dispositivo según sus ajustes (mover al final, borrar automáticamente).</summary>
    Default = 0,
    /// <summary>La tarea hecha se queda donde está y la nota nunca borra nada.</summary>
    KeepInPlace = 1,
    /// <summary>La tarea hecha baja al final de la lista y la nota nunca borra nada.</summary>
    MoveToEnd = 2
}

public static class CompletedTasksPolicyRules
{
    /// <summary>Siguiente valor al pulsar el elemento de menú que cicla: Default → mantener → al final → Default.</summary>
    public static CompletedTasksPolicy Next(CompletedTasksPolicy current) => current switch
    {
        CompletedTasksPolicy.Default => CompletedTasksPolicy.KeepInPlace,
        CompletedTasksPolicy.KeepInPlace => CompletedTasksPolicy.MoveToEnd,
        _ => CompletedTasksPolicy.Default
    };

    /// <summary>Un número desconocido (de una versión futura) se lee como Default: peor sería fallar al descifrar la nota.</summary>
    public static CompletedTasksPolicy Normalize(CompletedTasksPolicy value) =>
        Enum.IsDefined(value) ? value : CompletedTasksPolicy.Default;

    /// <summary>¿Puede el borrado automático tocar las tareas de esta nota? Solo con Default y si ese dispositivo lo tiene activado.</summary>
    public static bool MayDelete(CompletedTasksPolicy policy, AppSettings settings) =>
        policy == CompletedTasksPolicy.Default && settings.AutoHideCompletedTasks;

    /// <summary>¿Baja una tarea recién marcada al final? La política de la nota manda sobre el ajuste del dispositivo en los dos sentidos.</summary>
    public static bool ShouldMoveToEnd(CompletedTasksPolicy policy, AppSettings settings) => policy switch
    {
        CompletedTasksPolicy.KeepInPlace => false,
        CompletedTasksPolicy.MoveToEnd => true,
        _ => settings.MoveCompletedTasksToEnd
    };
}
