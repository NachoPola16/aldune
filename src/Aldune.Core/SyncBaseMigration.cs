namespace Aldune.Core;

/// <summary>
/// Una sola vez, al primer arranque con bases de sync por perfil: antes solo sincronizaba el perfil elegido y sus bases vivían en una
/// tabla sin perfil, que la migración de la base de datos deja bajo la clave vacía (la del primer perfil). Si quien sincronizaba era otro,
/// esas bases son de su almacén: se le pasan, o el primer perfil compararía contra versiones de otro almacén (y el otro empezaría sin
/// bases, con un falso conflicto por cada nota distinta).
/// </summary>
public static class SyncBaseMigration
{
    public static void Run(AppSettings settings, NotesRepository repository)
    {
        if (settings.SyncBasesKeyed) return;
        SyncProfileStore.Ensure(settings);
        var activeKey = SyncProfileStore.BaseKeyOfActive(settings);
        if (activeKey.Length > 0) repository.MoveSyncBases("", activeKey);
        settings.SyncBasesKeyed = true;
    }
}
