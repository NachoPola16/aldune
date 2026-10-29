package app.aldune.domain

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test

class ReorderPlanTest {
    private val ids = listOf("a", "b", "c", "d")

    @Test fun moverAlPrincipioSoloTieneVecinaDeDetras() {
        assertEquals(ReorderPlan.Neighbours(before = null, after = "a"), ReorderPlan.neighbours(ids, from = 2, to = 0))
    }

    @Test fun moverAlFinalSoloTieneVecinaDeDelante() {
        assertEquals(ReorderPlan.Neighbours(before = "d", after = null), ReorderPlan.neighbours(ids, from = 0, to = 3))
    }

    @Test fun moverAlMedioTieneLasDos() {
        // «a» baja a la posición 2: queda b, c, a, d.
        assertEquals(ReorderPlan.Neighbours(before = "c", after = "d"), ReorderPlan.neighbours(ids, from = 0, to = 2))
        // «d» sube a la posición 1: queda a, d, b, c.
        assertEquals(ReorderPlan.Neighbours(before = "a", after = "b"), ReorderPlan.neighbours(ids, from = 3, to = 1))
    }

    @Test fun soltarDondeEstabaNoEsUnMovimiento() {
        assertNull(ReorderPlan.neighbours(ids, from = 1, to = 1))
    }

    @Test fun unaSolaNotaNoSePuedeMover() {
        assertNull(ReorderPlan.neighbours(listOf("a"), from = 0, to = 0))
    }

    @Test fun indicesFueraDeRangoNoSonUnMovimiento() {
        assertNull(ReorderPlan.neighbours(ids, from = 4, to = 0))
        assertNull(ReorderPlan.neighbours(ids, from = 0, to = -1))
        assertNull(ReorderPlan.neighbours(ids, from = 0, to = 4))
    }

    // ───── Subir / bajar una posición (la alternativa al arrastre) ─────

    @Test fun subirUnaPosicionDaLasVecinasDelNuevoSitio() {
        // «c» sube: queda a, c, b, d.
        assertEquals(ReorderPlan.Step(ReorderPlan.Neighbours(before = "a", after = "b"), position = 2), ReorderPlan.step(ids, "c", -1))
    }

    @Test fun bajarUnaPosicionDaLasVecinasDelNuevoSitio() {
        // «b» baja: queda a, c, b, d.
        assertEquals(ReorderPlan.Step(ReorderPlan.Neighbours(before = "c", after = "d"), position = 3), ReorderPlan.step(ids, "b", +1))
    }

    @Test fun subirLaPrimeraOBajarLaUltimaNoSePuede() {
        assertNull(ReorderPlan.step(ids, "a", -1))
        assertNull(ReorderPlan.step(ids, "d", +1))
    }

    @Test fun unaNotaQueNoEstaEnLaListaNoSeMueve() {
        assertNull(ReorderPlan.step(ids, "zzz", -1))
    }

    @Test fun lasFlechasSoloSeOfrecenSiLaListaSePuedeReordenarYLaNotaTieneHueco() {
        assertEquals(setOf(ReorderPlan.Direction.DOWN), ReorderPlan.available(ids, "a", reorderable = true))
        assertEquals(setOf(ReorderPlan.Direction.UP), ReorderPlan.available(ids, "d", reorderable = true))
        assertEquals(setOf(ReorderPlan.Direction.UP, ReorderPlan.Direction.DOWN), ReorderPlan.available(ids, "b", reorderable = true))
        // Con un filtro (búsqueda, etiqueta, otro orden) las vecinas que se ven no son las reales: no se ofrece nada.
        assertEquals(emptySet<ReorderPlan.Direction>(), ReorderPlan.available(ids, "b", reorderable = false))
        assertEquals(emptySet<ReorderPlan.Direction>(), ReorderPlan.available(listOf("a"), "a", reorderable = true))
    }
}
