package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

/** Twin of `WhatsNewPlanTests` in `Aldune.Core.Tests`. */
class WhatsNewPlanTest {
    private val catalog = listOf(18, 19, 20)

    @Test fun `una instalacion nueva no ensena nada`() = assertEquals(emptyList<Int>(), WhatsNewPlan.toShow(0, true, 20, catalog))

    @Test fun `tras actualizar se listan las versiones posteriores a la vista, la mas nueva primero`() =
        assertEquals(listOf(19, 18), WhatsNewPlan.toShow(17, false, 19, catalog))

    @Test fun `lo ya visto no vuelve a salir`() = assertEquals(emptyList<Int>(), WhatsNewPlan.toShow(20, false, 20, catalog))

    @Test fun `una instalacion que nunca guardo version lista todo desde la base`() =
        assertEquals(listOf(20, 19, 18), WhatsNewPlan.toShow(0, false, 20, catalog))

    @Test fun `una version mas nueva que la que corre no se lista y bajar de version no ensena nada`() {
        assertEquals(listOf(18), WhatsNewPlan.toShow(17, false, 18, catalog))
        assertEquals(emptyList<Int>(), WhatsNewPlan.toShow(20, false, 18, catalog))
    }
}
