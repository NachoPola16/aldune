package app.aldune.syncclient

import app.aldune.core.NoteState
import app.aldune.core.ProfileCodes
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncKeyFormat
import app.aldune.core.SyncNote
import app.aldune.sync.HttpSyncTransport
import java.io.File
import java.security.SecureRandom
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import org.junit.Assume.assumeTrue
import org.junit.Test

/**
 * Herramienta de sondas, no una prueba: hace de «PC» frente a un servidor de sync real mientras se prueba la app en un dispositivo
 * (docs/ANDROID_PROBES.md, «Sonda de la sync»). Solo corre si `ALDUNE_PROBE` está definida; sin ella se salta y no cuesta nada.
 *
 * Variables: `ALDUNE_PROBE` (code | list | put | protected | delete | unsigned), `ALDUNE_PROBE_URL`, `ALDUNE_PROBE_TOKEN`, `ALDUNE_PROBE_KEYFILE`
 * (donde se guarda la clave de la sonda), `ALDUNE_PROBE_OUT` (donde escribe el resultado), y según el comando `ALDUNE_PROBE_ID`,
 * `ALDUNE_PROBE_TEXT`, `ALDUNE_PROBE_STATE`, `ALDUNE_PROBE_DEVICE`. Siempre contra un servidor de pruebas, nunca el real.
 */
class ProbePcSimulator {
    private fun env(name: String): String? = System.getenv(name)?.takeIf { it.isNotBlank() }

    @Test fun probe() {
        val command = env("ALDUNE_PROBE")
        assumeTrue(command != null)
        val url = env("ALDUNE_PROBE_URL")!!
        val token = env("ALDUNE_PROBE_TOKEN")!!
        val keyFile = File(env("ALDUNE_PROBE_KEYFILE")!!)
        val out = File(env("ALDUNE_PROBE_OUT")!!)
        val device = env("ALDUNE_PROBE_DEVICE") ?: "pcpcpcpcpcpcpcpcpcpcpcpcpcpcpcpc"
        val key = if (keyFile.exists()) SyncKeyFormat.decode(keyFile.readText().trim()) else ByteArray(32).also {
            SecureRandom().nextBytes(it)
            keyFile.writeText(SyncKeyFormat.encode(it))
        }
        val transport = HttpSyncTransport(url, token)
        val now = OffsetDateTime.now(ZoneOffset.UTC).let { it.withNano(it.nano / 100 * 100) }

        val text = when (command) {
            "code" -> ProfileCodes.encode(key, ProfileCodes.SCOPE_ALL, emptyList(), "Sonda", ProfileCodes.TRANSPORT_SERVER, url)
            "list" -> transport.readAll().sortedBy { it.updatedAt }.joinToString("\n") { e ->
                if (e.tombstone) {
                    val signed = SyncEnvelopeCodec.isAuthenticTombstone(e, key)
                    "${e.noteId} TOMBSTONE signed=$signed device=${e.deviceId} at=${e.updatedAt}"
                } else {
                    runCatching { SyncEnvelopeCodec.decryptNote(e, key) }.fold(
                        { n -> "${n.id} state=${n.state} protected=${n.isProtected} device=${e.deviceId} at=${e.updatedAt} text=${n.text.replace("\n", "\\n").replace("\r", "\\r")}" },
                        { "${e.noteId} UNREADABLE ${it.message}" },
                    )
                }
            }
            "put" -> {
                val id = UUID.fromString(env("ALDUNE_PROBE_ID") ?: UUID.randomUUID().toString())
                val note = SyncNote(
                    id, env("ALDUNE_PROBE_TEXT") ?: "nota del PC", "#FFDDD4", now, now, env("ALDUNE_PROBE_STATE")?.toInt() ?: NoteState.ACTIVE,
                    "", emptyList(), false, null, null,
                )
                transport.write(SyncEnvelopeCodec.createNote(note, key, device))
                "put $id"
            }
            // Una nota protegida como la subiría Windows: texto vacío y el contenido cifrado aparte (aquí, bytes de relleno: la app no lo abre en F5).
            "protected" -> {
                val id = UUID.fromString(env("ALDUNE_PROBE_ID") ?: UUID.randomUUID().toString())
                val filler = java.util.Base64.getEncoder().encodeToString(ByteArray(16) { it.toByte() })
                val note = SyncNote(
                    id, "", "#FFDDD4", now, now, NoteState.ACTIVE, "", listOf("secreta"), true,
                    app.aldune.core.ProtectedNoteContent(salt = filler, cipherText = filler, nonce = filler, tag = filler), 1.5,
                )
                transport.write(SyncEnvelopeCodec.createNote(note, key, device))
                "protected $id"
            }
            "delete" -> {
                val id = UUID.fromString(env("ALDUNE_PROBE_ID")!!)
                transport.write(SyncEnvelopeCodec.createTombstone(id, now, key, device))
                "deleted $id (signed)"
            }
            // Un borrado con la firma de OTRA clave: el receptor no puede comprobarlo y debe mandar la nota a la papelera, nunca borrarla.
            "unsigned" -> {
                val id = UUID.fromString(env("ALDUNE_PROBE_ID")!!)
                transport.write(SyncEnvelopeCodec.createTombstone(id, now, ByteArray(32) { 7 }, device))
                "deleted $id (unsigned)"
            }
            else -> error("Comando de sonda desconocido: $command")
        }
        out.writeText(text + "\n")
    }
}
