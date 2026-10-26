package app.aldune.syncclient

import java.time.Duration
import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class BackgroundStatusTest {
    private val now = Instant.parse("2026-10-27T12:00:00Z")
    private fun ago(duration: Duration) = BackgroundRun(now.minus(duration), failure = null)

    @Test fun `apagado no dice nada de las pasadas`() {
        val line = formatBackground(enabled = false, last = ago(Duration.ofMinutes(5)), restricted = false, now = now)
        assertEquals(BackgroundMessage.DISABLED, line.message)
        assertNull(line.relative)
    }

    @Test fun `apagado ni siquiera avisa de la restriccion del sistema`() {
        assertEquals(false, formatBackground(enabled = false, last = null, restricted = true, now = now).restricted)
    }

    @Test fun `encendido y sin ninguna pasada todavia`() {
        val line = formatBackground(enabled = true, last = null, restricted = false, now = now)
        assertEquals(BackgroundMessage.NEVER_RAN, line.message)
        assertNull(line.relative)
    }

    @Test fun `una pasada buena dice hace cuanto`() {
        val line = formatBackground(enabled = true, last = ago(Duration.ofMinutes(12)), restricted = false, now = now)
        assertEquals(BackgroundMessage.OK, line.message)
        assertEquals(RelativeTime.Minutes(12), line.relative)
    }

    @Test fun `sin conexion no es un error que exija nada al usuario`() {
        val last = BackgroundRun(now.minus(Duration.ofHours(2)), SyncFailureKind.OFFLINE)
        val line = formatBackground(enabled = true, last = last, restricted = false, now = now)
        assertEquals(BackgroundMessage.OFFLINE, line.message)
        assertEquals(RelativeTime.Hours(2), line.relative)
        assertNull(line.failure)
    }

    @Test fun `un fallo que exige actuar se senala con su tipo`() {
        for (kind in listOf(SyncFailureKind.UNAUTHORIZED, SyncFailureKind.KEY_CHANGED, SyncFailureKind.SERVER_ERROR, SyncFailureKind.TOO_LARGE, SyncFailureKind.UNSUPPORTED_FORMAT, SyncFailureKind.UNKNOWN)) {
            val line = formatBackground(true, BackgroundRun(now.minus(Duration.ofMinutes(20)), kind), restricted = false, now = now)
            assertEquals(BackgroundMessage.FAILED, line.message, kind.name)
            assertEquals(kind, line.failure, kind.name)
            assertEquals(RelativeTime.Minutes(20), line.relative)
        }
    }

    @Test fun `la restriccion del sistema se avisa aparte de la ultima pasada`() {
        val line = formatBackground(enabled = true, last = ago(Duration.ofMinutes(1)), restricted = true, now = now)
        assertEquals(true, line.restricted)
        assertEquals(BackgroundMessage.OK, line.message)
    }
}
