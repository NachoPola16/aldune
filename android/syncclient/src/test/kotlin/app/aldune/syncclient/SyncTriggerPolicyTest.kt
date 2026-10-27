package app.aldune.syncclient

import java.time.Duration
import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals

/** Una prueba por fila de la tabla de la spec F5 §4.4, más los cruces (fallos, pasada en curso, ediciones concurrentes). */
class SyncTriggerPolicyTest {
    private val clock = TestClock(Instant.parse("2026-03-01T10:00:00Z"))
    private val policy = SyncTriggerPolicy(clock, profileConfigured = true)

    private val ok = SyncOutcome.Success()
    private val failed = SyncOutcome.Failure(SyncFailure(SyncFailureKind.OFFLINE))

    private fun at(seconds: Long) = clock.current.plusSeconds(seconds)
    private fun send(event: SyncEvent) = policy.onEvent(event)

    /** Lanza una pasada y la termina con [outcome] tras [seconds]. */
    private fun runPass(outcome: SyncOutcome = ok, seconds: Long = 1): SyncDecision {
        assertEquals(SyncDecision.RunNow, send(SyncEvent.ManualSync))
        clock.advanceSeconds(seconds)
        return send(SyncEvent.SyncFinished(outcome))
    }

    // --- sin perfil ---

    @Test fun `sin perfil configurado nunca hace nada`() {
        val none = SyncTriggerPolicy(clock)
        for (event in listOf(SyncEvent.AppOpened, SyncEvent.Foreground, SyncEvent.NoteOpened, SyncEvent.LocalChange, SyncEvent.PullToRefresh, SyncEvent.ManualSync, SyncEvent.Periodic, SyncEvent.TimerFired)) {
            assertEquals(SyncDecision.Nothing, none.onEvent(event), event.toString())
        }
    }

    @Test fun `al quitar el perfil se olvida todo lo pendiente`() {
        send(SyncEvent.LocalChange)
        policy.setProfileConfigured(false)
        assertEquals(SyncDecision.Nothing, send(SyncEvent.ManualSync))
        policy.setProfileConfigured(true)
        clock.advanceSeconds(10)
        assertEquals(SyncDecision.Nothing, send(SyncEvent.TimerFired))
        assertEquals(SyncDecision.RunNow, send(SyncEvent.AppOpened))
    }

    // --- una fila por evento ---

    @Test fun `abrir la app sincroniza ahora`() = assertEquals(SyncDecision.RunNow, send(SyncEvent.AppOpened))

    @Test fun `volver a primer plano sincroniza si nunca se intento`() = assertEquals(SyncDecision.RunNow, send(SyncEvent.Foreground))

    @Test fun `volver a primer plano no sincroniza si la ultima pasada intentada fue hace 30 s o menos`() {
        runPass(seconds = 0)
        clock.advanceSeconds(29)
        assertEquals(SyncDecision.Nothing, send(SyncEvent.Foreground))
    }

    @Test fun `volver a primer plano cuenta desde el intento y sincroniza pasados mas de 30 s`() {
        assertEquals(SyncDecision.RunNow, send(SyncEvent.AppOpened))
        send(SyncEvent.SyncFinished(ok))
        clock.advanceSeconds(30)
        assertEquals(SyncDecision.Nothing, send(SyncEvent.Foreground), "30 s justos todavía no")
        clock.advanceSeconds(1)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.Foreground))
    }

    @Test fun `abrir una nota sincroniza si nunca se intento`() = assertEquals(SyncDecision.RunNow, send(SyncEvent.NoteOpened))

    @Test fun `abrir una nota sigue la misma regla de 30 s que volver a primer plano`() {
        assertEquals(SyncDecision.RunNow, send(SyncEvent.AppOpened))
        send(SyncEvent.SyncFinished(ok))
        clock.advanceSeconds(30)
        assertEquals(SyncDecision.Nothing, send(SyncEvent.NoteOpened), "30 s justos todavía no")
        clock.advanceSeconds(1)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.NoteOpened))
    }

    @Test fun `la ultima pasada intentada cuenta aunque haya fallado`() {
        runPass(failed)
        clock.advanceSeconds(70) // fuera de la espera de 60 s del fallo, pero el intento fue hace 71 s: ya vale
        assertEquals(SyncDecision.RunNow, send(SyncEvent.Foreground))
    }

    @Test fun `un cambio local se programa 5 s despues`() {
        assertEquals(SyncDecision.RunAt(at(5)), send(SyncEvent.LocalChange))
    }

    @Test fun `cada cambio nuevo retrasa la pasada`() {
        assertEquals(SyncDecision.RunAt(at(5)), send(SyncEvent.LocalChange))
        clock.advanceSeconds(3)
        assertEquals(SyncDecision.RunAt(at(5)), send(SyncEvent.LocalChange)) // 3 + 5 desde el inicio
        clock.advanceSeconds(2) // el primer temporizador vence (t=5), pero el cambio de t=3 lo retrasó a t=8
        assertEquals(SyncDecision.RunAt(Instant.parse("2026-03-01T10:00:08Z")), send(SyncEvent.TimerFired))
        clock.advanceSeconds(3)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.TimerFired))
    }

    @Test fun `el temporizador sin nada pendiente no hace nada`() = assertEquals(SyncDecision.Nothing, send(SyncEvent.TimerFired))

    @Test fun `tirar para actualizar y sincronizar a mano son inmediatos y sin limite`() {
        assertEquals(SyncDecision.RunNow, send(SyncEvent.PullToRefresh))
        send(SyncEvent.SyncFinished(ok))
        assertEquals(SyncDecision.RunNow, send(SyncEvent.PullToRefresh))
        send(SyncEvent.SyncFinished(ok))
        assertEquals(SyncDecision.RunNow, send(SyncEvent.ManualSync))
    }

    @Test fun `la periodica sincroniza ahora`() = assertEquals(SyncDecision.RunNow, send(SyncEvent.Periodic))

    // --- una sola pasada a la vez ---

    @Test fun `con una pasada en curso un evento no lanza otra y deja una sola repeticion`() {
        assertEquals(SyncDecision.RunNow, send(SyncEvent.AppOpened))
        for (event in listOf(SyncEvent.Foreground, SyncEvent.Periodic, SyncEvent.AppOpened, SyncEvent.ManualSync, SyncEvent.PullToRefresh)) {
            assertEquals(SyncDecision.Nothing, send(event), event.toString())
        }
        clock.advanceSeconds(2)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.SyncFinished(ok)))
        assertEquals(SyncDecision.Nothing, send(SyncEvent.SyncFinished(ok)), "la repetición no deja otra detrás")
    }

    @Test fun `sin eventos durante la pasada no hay repeticion`() {
        send(SyncEvent.AppOpened)
        assertEquals(SyncDecision.Nothing, send(SyncEvent.SyncFinished(ok)))
    }

    @Test fun `un cambio local durante la pasada pide otra al acabar, respetando los 5 s`() {
        send(SyncEvent.AppOpened)
        clock.advanceSeconds(1)
        assertEquals(SyncDecision.Nothing, send(SyncEvent.LocalChange)) // pendiente para t=6
        clock.advanceSeconds(1)
        assertEquals(SyncDecision.RunAt(at(4)), send(SyncEvent.SyncFinished(ok))) // t=2 ahora; vence en t=6
        clock.advanceSeconds(4)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.TimerFired))
    }

    @Test fun `un cambio local durante una pasada larga se repite ya al acabar si su hora pasó`() {
        send(SyncEvent.AppOpened)
        send(SyncEvent.LocalChange)
        clock.advanceSeconds(20)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.SyncFinished(ok)))
    }

    // --- ediciones concurrentes ---

    @Test fun `si hubo notas saltadas por edicion concurrente pide otra pasada en 5 s`() {
        send(SyncEvent.AppOpened)
        clock.advanceSeconds(2)
        assertEquals(SyncDecision.RunAt(at(5)), send(SyncEvent.SyncFinished(SyncOutcome.Success(skippedConcurrent = 1))))
        clock.advanceSeconds(5)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.TimerFired))
    }

    // --- tras un fallo ---

    @Test fun `tras un fallo las automaticas esperan 60 s desde el fallo`() {
        runPass(failed)
        for (event in listOf(SyncEvent.AppOpened, SyncEvent.Foreground, SyncEvent.Periodic)) {
            clock.advanceSeconds(10)
            assertEquals(SyncDecision.Nothing, send(event), event.toString())
        }
        clock.advanceSeconds(50) // 80 s desde el fallo
        assertEquals(SyncDecision.RunNow, send(SyncEvent.Periodic))
    }

    @Test fun `a los 60 s justos del fallo ya se puede`() {
        runPass(failed)
        clock.advanceSeconds(59)
        assertEquals(SyncDecision.Nothing, send(SyncEvent.AppOpened))
        clock.advanceSeconds(1)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.AppOpened))
    }

    @Test fun `las manuales nunca esperan tras un fallo`() {
        runPass(failed)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.PullToRefresh))
        send(SyncEvent.SyncFinished(failed))
        assertEquals(SyncDecision.RunNow, send(SyncEvent.ManualSync))
    }

    @Test fun `un cambio local tras un fallo espera a que acabe la espera`() {
        runPass(failed)
        clock.advanceSeconds(10)
        assertEquals(SyncDecision.RunAt(at(50)), send(SyncEvent.LocalChange)) // el fallo fue en t=1: hasta t=61 desde el inicio → +50 desde ahora
    }

    @Test fun `un exito levanta la espera`() {
        runPass(failed)
        clock.advanceSeconds(1)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.ManualSync))
        send(SyncEvent.SyncFinished(ok))
        assertEquals(SyncDecision.RunNow, send(SyncEvent.Periodic))
    }

    @Test fun `una automatica que esperaba durante una pasada que falla se descarta, sin reintento`() {
        send(SyncEvent.AppOpened)
        send(SyncEvent.Periodic)
        assertEquals(SyncDecision.Nothing, send(SyncEvent.SyncFinished(failed)))
    }

    @Test fun `una manual que esperaba durante una pasada que falla se hace igualmente`() {
        send(SyncEvent.AppOpened)
        send(SyncEvent.ManualSync)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.SyncFinished(failed)))
    }

    @Test fun `un cambio local pendiente cuando falla la pasada se vuelve a armar pasados 60 s`() {
        send(SyncEvent.AppOpened)
        send(SyncEvent.LocalChange)
        clock.advanceSeconds(1)
        assertEquals(SyncDecision.RunAt(at(60)), send(SyncEvent.SyncFinished(failed)))
        clock.advanceSeconds(60)
        assertEquals(SyncDecision.RunNow, send(SyncEvent.TimerFired))
    }

    @Test fun `tras el fallo no hay reintento creciente y sin eventos no se lanza nada`() {
        runPass(failed)
        clock.advanceSeconds(3600)
        assertEquals(SyncDecision.Nothing, send(SyncEvent.TimerFired))
    }

    @Test fun `el tiempo entre intentos usa una duracion fija`() {
        assertEquals(Duration.ofSeconds(60), SyncTriggerPolicy.FAILURE_COOLDOWN)
        assertEquals(Duration.ofSeconds(5), SyncTriggerPolicy.LOCAL_CHANGE_DELAY)
        assertEquals(Duration.ofSeconds(30), SyncTriggerPolicy.FOREGROUND_MIN_GAP)
    }
}
