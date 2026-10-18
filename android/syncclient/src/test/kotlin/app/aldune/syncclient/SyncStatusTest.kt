package app.aldune.syncclient

import java.time.Duration
import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class SyncStatusTest {
    private val now = Instant.parse("2026-03-10T12:00:00Z")
    private fun ago(duration: Duration) = now.minus(duration)

    // --- RelativeTime: cada frontera ---

    @Test fun `los limites del tiempo relativo`() {
        val cases = listOf(
            Duration.ZERO to RelativeTime.JustNow,
            Duration.ofSeconds(59) to RelativeTime.JustNow,
            Duration.ofSeconds(60) to RelativeTime.Minutes(1),
            Duration.ofSeconds(119) to RelativeTime.Minutes(1),
            Duration.ofMinutes(3) to RelativeTime.Minutes(3),
            Duration.ofMinutes(59).plusSeconds(59) to RelativeTime.Minutes(59),
            Duration.ofMinutes(60) to RelativeTime.Hours(1),
            Duration.ofHours(23).plusMinutes(59) to RelativeTime.Hours(23),
            Duration.ofHours(24) to RelativeTime.Days(1),
            Duration.ofDays(6).plusHours(23) to RelativeTime.Days(6),
            Duration.ofDays(7) to RelativeTime.Date(ago(Duration.ofDays(7))),
            Duration.ofDays(400) to RelativeTime.Date(ago(Duration.ofDays(400))),
        )
        for ((elapsed, expected) in cases) assertEquals(expected, relativeTime(ago(elapsed), now), elapsed.toString())
    }

    @Test fun `una hora guardada por delante del reloj es ahora mismo, no dentro de`() {
        assertEquals(RelativeTime.JustNow, relativeTime(now.plusSeconds(500), now))
    }

    // --- StatusLine ---

    @Test fun `sin perfil no se dibuja nada`() {
        assertEquals(StatusLine(StatusIcon.NONE, StatusMessage.NOT_CONFIGURED), formatStatus(null, now))
    }

    @Test fun `con perfil y sin ninguna sync todavia`() {
        val line = formatStatus(SyncStatus(), now)
        assertEquals(StatusIcon.OK, line.icon)
        assertEquals(StatusMessage.NEVER_SYNCED, line.message)
        assertNull(line.relative)
    }

    @Test fun `ultima sync buena hace unos minutos`() {
        val line = formatStatus(SyncStatus(lastSuccessAt = ago(Duration.ofMinutes(3)), lastAttemptAt = ago(Duration.ofMinutes(3))), now)
        assertEquals(StatusLine(StatusIcon.OK, StatusMessage.LAST_SYNC, RelativeTime.Minutes(3)), line)
    }

    @Test fun `sincronizando lleva su icono y conserva la ultima sync`() {
        val line = formatStatus(SyncStatus(lastSuccessAt = ago(Duration.ofHours(2)), running = true), now)
        assertEquals(StatusLine(StatusIcon.SYNCING, StatusMessage.SYNCING, RelativeTime.Hours(2)), line)
    }

    @Test fun `sincronizando manda sobre un fallo anterior`() {
        val line = formatStatus(SyncStatus(lastFailure = SyncFailure(SyncFailureKind.UNKNOWN), running = true), now)
        assertEquals(StatusIcon.SYNCING, line.icon)
    }

    @Test fun `sin conexion lleva el icono de sin conexion y no el de error`() {
        val failure = SyncFailure(SyncFailureKind.OFFLINE, "UnknownHostException: x")
        val line = formatStatus(SyncStatus(lastSuccessAt = ago(Duration.ofMinutes(10)), lastFailure = failure), now)
        assertEquals(StatusLine(StatusIcon.OFFLINE, StatusMessage.OFFLINE, RelativeTime.Minutes(10), failure), line)
    }

    @Test fun `sin conexion y sin ninguna sync buena no inventa una hora`() {
        val line = formatStatus(SyncStatus(lastFailure = SyncFailure(SyncFailureKind.OFFLINE)), now)
        assertEquals(StatusIcon.OFFLINE, line.icon)
        assertNull(line.relative)
    }

    @Test fun `cada clase de fallo tiene su mensaje y el resto de errores llevan el icono de error`() {
        val expected = mapOf(
            SyncFailureKind.UNAUTHORIZED to StatusMessage.UNAUTHORIZED,
            SyncFailureKind.KEY_CHANGED to StatusMessage.KEY_CHANGED,
            SyncFailureKind.SERVER_ERROR to StatusMessage.SERVER_ERROR,
            SyncFailureKind.TOO_LARGE to StatusMessage.TOO_LARGE,
            SyncFailureKind.UNSUPPORTED_FORMAT to StatusMessage.UNSUPPORTED_FORMAT,
            SyncFailureKind.UNKNOWN to StatusMessage.UNKNOWN_ERROR,
        )
        for ((kind, message) in expected) {
            val failure = SyncFailure(kind, "detalle técnico")
            val line = formatStatus(SyncStatus(lastSuccessAt = ago(Duration.ofDays(2)), lastFailure = failure), now)
            assertEquals(StatusIcon.ERROR, line.icon, kind.toString())
            assertEquals(message, line.message, kind.toString())
            assertEquals(RelativeTime.Days(2), line.relative)
            assertEquals(failure, line.failure)
        }
        assertEquals(SyncFailureKind.entries.toSet() - SyncFailureKind.OFFLINE, expected.keys)
    }

    @Test fun `una sync de hace mas de una semana da la fecha`() {
        val last = ago(Duration.ofDays(30))
        assertEquals(RelativeTime.Date(last), formatStatus(SyncStatus(lastSuccessAt = last), now).relative)
    }
}
