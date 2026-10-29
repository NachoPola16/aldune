package app.aldune.domain

import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertIs
import kotlin.test.assertTrue

class ReminderNotificationContentTest {
    private fun note(
        text: String,
        id: String = "n1",
        isProtected: Boolean = false,
        tags: List<String> = emptyList(),
        protectedJson: String? = null,
    ) = Note(
        id = id, text = text, lineEnding = LineEnding.LF, color = "#EBD38B", createdAt = Instant.EPOCH, updatedAt = Instant.EPOCH,
        state = NoteState.ACTIVE, tags = tags, isProtected = isProtected, protectedContentJson = protectedJson,
    )

    private fun build(n: Note) = ReminderNotificationContent.of(n, placeholder = "Nota nueva")

    @Test
    fun `una nota normal lleva titulo y tareas pendientes`() {
        val content = assertIs<ReminderNotificationContent.Open>(build(note("# Compra\n☐ leche\n☑ pan\n☐ huevos\nsuelto")))
        assertEquals("n1", content.noteId)
        assertEquals("Compra", content.title)
        assertEquals(listOf("leche", "huevos"), content.pendingTasks)
    }

    @Test
    fun `una nota sin texto usa el relleno`() {
        assertEquals("Nota nueva", assertIs<ReminderNotificationContent.Open>(build(note(""))).title)
    }

    @Test
    fun `como mucho tres tareas`() {
        val content = assertIs<ReminderNotificationContent.Open>(build(note("t\n☐ 1\n☐ 2\n☐ 3\n☐ 4\n☐ 5")))
        assertEquals(listOf("1", "2", "3"), content.pendingTasks)
        assertEquals(3, ReminderNotificationContent.MAX_TASKS)
    }

    @Test
    fun `cada tarea se recorta a 120 caracteres contando los puntos suspensivos`() {
        val long = "x".repeat(300)
        val task = assertIs<ReminderNotificationContent.Open>(build(note("t\n☐ $long"))).pendingTasks.single()
        assertEquals(ReminderNotificationContent.MAX_TASK_LENGTH, task.length)
        assertTrue(task.endsWith("…"))
        val exact = "y".repeat(120)
        assertEquals(exact, assertIs<ReminderNotificationContent.Open>(build(note("t\n☐ $exact"))).pendingTasks.single())
    }

    @Test
    fun `el recorte no parte un par sustituto`() {
        val task = assertIs<ReminderNotificationContent.Open>(build(note("t\n☐ " + "😀".repeat(100)))).pendingTasks.single()
        assertTrue(task.length <= 120)
        assertFalse(task.dropLast(1).last().isHighSurrogate())
    }

    @Test
    fun `un titulo enorme tambien se recorta`() {
        val title = assertIs<ReminderNotificationContent.Open>(build(note("z".repeat(500)))).title
        assertEquals(ReminderNotificationContent.MAX_TITLE_LENGTH, title.length)
    }

    @Test
    fun `una protegida con nombre visible lo lleva en Locked`() {
        val named = note("secreto", isProtected = true).copy(protectedTitle = "Banco")
        assertEquals(ReminderNotificationContent.Locked("n1", "Banco"), build(named))
    }

    @Test
    fun `una protegida es Locked y solo lleva el id`() {
        assertEquals(ReminderNotificationContent.Locked("n1"), build(note("secreto", isProtected = true)))
    }

    @Test
    fun `ningun rastro de una protegida llega al resultado`() {
        val marker = "MARCADOR-SECRETO"
        // Texto en memoria (como una nota desbloqueada), etiquetas y JSON cifrado: nada de eso puede salir.
        val variants = listOf(
            note("$marker\n☐ $marker", isProtected = true, tags = listOf(marker), protectedJson = """{"cipherText":"$marker"}"""),
            note("# $marker", isProtected = true),
            note("", isProtected = true, tags = listOf(marker, "otra"), protectedJson = marker),
            note("t\n☐ $marker\n☐ $marker\n☐ $marker\n☐ $marker", isProtected = true, tags = listOf(marker)),
        )
        for (n in variants) {
            val content = build(n)
            assertIs<ReminderNotificationContent.Locked>(content)
            assertFalse(content.toString().contains(marker), content.toString())
        }
    }

    @Test
    fun `una nota que no es protegida con el marcador si lo lleva (el test de arriba no es trivial)`() {
        val content = build(note("MARCADOR\n☐ MARCADOR-TAREA"))
        assertTrue(content.toString().contains("MARCADOR"))
    }
}
