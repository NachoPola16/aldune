namespace Aldune.Core;

/// <summary>
/// Compatibilidad y operaciones básicas de perfiles. El resto de la aplicación sigue trabajando
/// con los campos de sincronización históricos de <see cref="AppSettings"/>: antes de guardar se
/// copian al perfil activo y al cambiar de perfil se hace la operación inversa.
/// </summary>
public static class SyncProfileStore
{
    public static void Ensure(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.SyncProfiles ??= new List<SyncProfileSettings>();
        settings.SyncProfiles.RemoveAll(profile => profile is null || string.IsNullOrWhiteSpace(profile.Id));

        if (settings.SyncProfiles.Count == 0)
        {
            settings.SyncProfiles.Add(CreateFromLegacy(settings, "Mis dispositivos"));
        }

        var active = settings.SyncProfiles.FirstOrDefault(profile => profile.Id == settings.ActiveSyncProfileId)
                     ?? settings.SyncProfiles[0];
        settings.ActiveSyncProfileId = active.Id;
        LoadActiveToLegacy(settings);
    }

    public static SyncProfileSettings Create(AppSettings settings, string name)
    {
        Ensure(settings);

        var profile = new SyncProfileSettings
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Vínculo {settings.SyncProfiles.Count + 1}" : name.Trim(),
            SyncIntervalMinutes = 15,
        };
        settings.SyncProfiles.Add(profile);
        settings.ActiveSyncProfileId = profile.Id;
        LoadActiveToLegacy(settings);
        return profile;
    }

    public static bool DeleteActive(AppSettings settings)
    {
        Ensure(settings);
        if (settings.SyncProfiles.Count <= 1) return false;

        var index = settings.SyncProfiles.FindIndex(profile => profile.Id == settings.ActiveSyncProfileId);
        if (index < 0) index = 0;
        settings.SyncProfiles.RemoveAt(index);
        settings.ActiveSyncProfileId = settings.SyncProfiles[Math.Min(index, settings.SyncProfiles.Count - 1)].Id;
        LoadActiveToLegacy(settings);
        return true;
    }

    public static SyncProfileSettings GetActive(AppSettings settings)
    {
        Ensure(settings);
        return settings.SyncProfiles.First(profile => profile.Id == settings.ActiveSyncProfileId);
    }

    public static void LoadActiveToLegacy(AppSettings settings)
    {
        var profile = settings.SyncProfiles?.FirstOrDefault(item => item.Id == settings.ActiveSyncProfileId);
        if (profile is null) return;

        settings.SyncEnabled = profile.SyncEnabled;
        settings.SyncTransport = profile.SyncTransport;
        settings.SyncFolderPath = profile.SyncFolderPath;
        settings.SyncServerUrl = profile.SyncServerUrl;
        settings.WrappedSyncServerToken = Clone(profile.WrappedSyncServerToken);
        settings.SyncWebDavUsername = profile.SyncWebDavUsername;
        settings.WrappedSyncWebDavPassword = Clone(profile.WrappedSyncWebDavPassword);
        settings.SyncAutomatically = profile.SyncAutomatically;
        settings.SyncIntervalMinutes = profile.SyncIntervalMinutes;
        settings.LastSyncAt = profile.LastSyncAt;
        settings.SyncDeviceId = profile.SyncDeviceId;
        settings.WrappedSyncKey = Clone(profile.WrappedSyncKey);
        settings.WrappedPendingSyncKey = Clone(profile.WrappedPendingSyncKey);
        settings.SyncKeyRotationPending = profile.SyncKeyRotationPending;
        settings.SyncScope = profile.SyncScope;
        settings.SyncNoteIds = profile.SyncNoteIds?.ToList() ?? new List<Guid>();
    }

    public static void SaveActiveFromLegacy(AppSettings settings)
    {
        EnsureWithoutProjection(settings);
        var profile = settings.SyncProfiles?.FirstOrDefault(item => item.Id == settings.ActiveSyncProfileId);
        if (profile is null) return;

        profile.SyncEnabled = settings.SyncEnabled;
        profile.SyncTransport = settings.SyncTransport;
        profile.SyncFolderPath = settings.SyncFolderPath;
        profile.SyncServerUrl = settings.SyncServerUrl;
        profile.WrappedSyncServerToken = Clone(settings.WrappedSyncServerToken);
        profile.SyncWebDavUsername = settings.SyncWebDavUsername;
        profile.WrappedSyncWebDavPassword = Clone(settings.WrappedSyncWebDavPassword);
        profile.SyncAutomatically = settings.SyncAutomatically;
        profile.SyncIntervalMinutes = settings.SyncIntervalMinutes;
        profile.LastSyncAt = settings.LastSyncAt;
        profile.SyncDeviceId = settings.SyncDeviceId;
        profile.WrappedSyncKey = Clone(settings.WrappedSyncKey);
        profile.WrappedPendingSyncKey = Clone(settings.WrappedPendingSyncKey);
        profile.SyncKeyRotationPending = settings.SyncKeyRotationPending;
        profile.SyncScope = settings.SyncScope;
        profile.SyncNoteIds = settings.SyncNoteIds?.ToList() ?? new List<Guid>();
    }

    private static void EnsureWithoutProjection(AppSettings settings)
    {
        settings.SyncProfiles ??= new List<SyncProfileSettings>();
        if (settings.SyncProfiles.Count == 0)
        {
            settings.SyncProfiles.Add(CreateFromLegacy(settings, "Mis dispositivos"));
            settings.ActiveSyncProfileId = settings.SyncProfiles[0].Id;
        }
    }

    private static SyncProfileSettings CreateFromLegacy(AppSettings settings, string name) => new()
    {
        Name = name,
        SyncEnabled = settings.SyncEnabled,
        SyncTransport = settings.SyncTransport,
        SyncFolderPath = settings.SyncFolderPath,
        SyncServerUrl = settings.SyncServerUrl,
        WrappedSyncServerToken = Clone(settings.WrappedSyncServerToken),
        SyncWebDavUsername = settings.SyncWebDavUsername,
        WrappedSyncWebDavPassword = Clone(settings.WrappedSyncWebDavPassword),
        SyncAutomatically = settings.SyncAutomatically,
        SyncIntervalMinutes = settings.SyncIntervalMinutes,
        LastSyncAt = settings.LastSyncAt,
        SyncDeviceId = settings.SyncDeviceId,
        WrappedSyncKey = Clone(settings.WrappedSyncKey),
        WrappedPendingSyncKey = Clone(settings.WrappedPendingSyncKey),
        SyncKeyRotationPending = settings.SyncKeyRotationPending,
        SyncScope = settings.SyncScope,
        SyncNoteIds = settings.SyncNoteIds?.ToList() ?? new List<Guid>(),
    };

    private static byte[]? Clone(byte[]? value) => value is null ? null : (byte[])value.Clone();
}
