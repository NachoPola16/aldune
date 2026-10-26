package app.aldune.syncclient

/**
 * Id del perfil que ya existía cuando Android solo admitía uno (y de las filas de `sync_base`/`sync_conflict` de entonces). Es fijo para que la
 * migración de Room (SQL puro) y el fichero de secretos de siempre no tengan que moverse.
 */
const val LEGACY_PROFILE_ID = "default"

private val SAFE_PROFILE_ID = Regex("[A-Za-z0-9-]+")

/** El fichero de secretos de un perfil. El id entra en un nombre de fichero, así que solo admite letras, cifras y guiones. */
fun vaultFileName(profileId: String): String {
    require(SAFE_PROFILE_ID.matches(profileId)) { "Id de perfil no válido: $profileId" }
    return if (profileId == LEGACY_PROFILE_ID) "syncsecrets.bin" else "syncsecrets-$profileId.bin"
}

/**
 * El resultado de una pasada que recorre varios perfiles. Un fallo gana (el primero): la política espera tras él para las pasadas
 * automáticas, y el perfil que sí sincronizó solo repite trabajo sin daño. Sin fallos, los contadores se suman.
 */
fun combineOutcomes(outcomes: List<SyncOutcome>): SyncOutcome {
    outcomes.filterIsInstance<SyncOutcome.Failure>().firstOrNull()?.let { return it }
    val ok = outcomes.filterIsInstance<SyncOutcome.Success>()
    return SyncOutcome.Success(
        uploaded = ok.sumOf { it.uploaded },
        received = ok.sumOf { it.received },
        tombstonesApplied = ok.sumOf { it.tombstonesApplied },
        trashed = ok.sumOf { it.trashed },
        conflicts = ok.sumOf { it.conflicts },
        omitted = ok.sumOf { it.omitted },
        skippedConcurrent = ok.sumOf { it.skippedConcurrent },
    )
}

/**
 * Un solo estado para la cabecera de la lista: «ejecutando» si alguno lo está, el último intento más reciente, el primer fallo y, como última
 * sync buena, la del perfil más atrasado (si alguno nunca sincronizó, ninguna: no se puede decir que todo esté al día).
 */
fun aggregateStatus(statuses: Collection<SyncStatus>): SyncStatus {
    if (statuses.isEmpty()) return SyncStatus()
    val successes = statuses.map { it.lastSuccessAt }
    return SyncStatus(
        lastSuccessAt = if (successes.any { it == null }) null else successes.filterNotNull().min(),
        lastAttemptAt = statuses.mapNotNull { it.lastAttemptAt }.maxOrNull(),
        lastFailure = statuses.firstNotNullOfOrNull { it.lastFailure },
        running = statuses.any { it.running },
    )
}
