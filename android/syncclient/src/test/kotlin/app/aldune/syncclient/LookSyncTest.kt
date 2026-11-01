package app.aldune.syncclient

import java.time.Instant
import java.util.Base64
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertNotEquals
import kotlin.test.assertNotNull
import kotlin.test.assertNull
import kotlin.test.assertTrue
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.boolean
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive

class LookSyncTest {
    private val key = ByteArray(32) { (it + 1).toByte() }
    private val t0 = Instant.parse("2026-11-01T12:00:00Z")

    private val base = LookValues(
        appearance = 17, aspectColors = null, squareCorners = null, noteColorAsStripe = false, syncSignal = null,
        uniformNoteColor = null, noteTheme = "classic", newNoteTone = 0, colorAssignment = 0, fixedNoteColor = null,
    )

    // Sealed by SyncLook.Seal of Core (C#) with the key 1..32, stamp 2026-11-01T12:00:00Z and device "device-cs".
    private val fromCSharp = "eyJDIjoiSHNieXlNNDZYUzVUVlx1MDAyQjdiRHI0OTZFcGlPSHl3TUVXNTdMeVNkVURHUVx1MDAyQmlqWm1kSk5ta2JMRDBCazVBSUVzSnRRMk45YlppazNNZ1lOdHpLeHZ5YmpSXHUwMDJCMVVOR3E1QW5pVWhEL0VRZC9BZGpuL3pqVVluZlJ1Zi9jdFRXUS9JemF2bFgxMEVIRk8wRHIvQ2tcdTAwMkJocms2OTZNZTRsaGxNaDFXaTJIcDUyVVpvTTRaMjQ5Y0JvMVhcdTAwMkJ3cmwxRm0xWmdYelFxeDRqSHFSQ3VzNXEySVhuVVFmMG5SM1pDZnJsbURiVS90ajdSSHpZblVTL01yZFFESlc4ZzhJellIS0pmbUhmYmhoOHBEWnAwY0hrcmkzZWkvdXd4QkhKSW9hbHlyWldySkdpbEE5cTNST0FoVVU4Tm12UGhyN3VCRTRQODZWbnpWUktQb0d4VHdDQUU2NGZnYU85TUNWY0FaRGRUTWtiSEk1emRoNHVlZk83SlVNTUVSWG5UakkxQWFjWnB4UFZpM28iLCJOIjoiUHdaU1RneDQ2dWtqVjhsOSIsIlQiOiJRakRadUZMV2lcdTAwMkJCZEVjXHUwMDJCRmF6XHUwMDJCL3l3PT0ifQ=="

    @Test fun `abre un documento sellado por C#`() {
        val doc = LookSync.open(key, Base64.getDecoder().decode(fromCSharp))
        assertNotNull(doc)
        assertEquals(t0, doc.stamp)
        assertEquals("device-cs", doc.device)
        assertEquals(17, doc.look.getValue("appearance").jsonPrimitive.int)
        assertEquals(true, doc.look.getValue("squareCorners").jsonPrimitive.boolean)
        assertEquals(1, doc.look.getValue("newNoteTone").jsonPrimitive.int)
    }

    @Test fun `aplicar lo que escribio C# cambia solo lo que dice`() {
        val doc = LookSync.open(key, Base64.getDecoder().decode(fromCSharp))!!
        val applied = LookSync.apply(base.copy(appearance = 0), doc.look)
        assertEquals(17, applied.appearance)
        assertEquals(true, applied.squareCorners)
        assertEquals(1, applied.newNoteTone)
        assertEquals(2, applied.colorAssignment)
    }

    @Test fun `sellar y abrir da lo mismo`() {
        val blob = LookSync.seal(key, t0, "me", LookSync.capture(base.copy(appearance = 3)))
        val doc = LookSync.open(key, blob)!!
        assertEquals(t0, doc.stamp)
        assertEquals("me", doc.device)
        assertEquals(3, LookSync.apply(base, doc.look).appearance)
    }

    @Test fun `el blob no revela nada y solo abre con la clave`() {
        val blob = LookSync.seal(key, t0, "device-1", LookSync.capture(base))
        val text = String(blob, Charsets.UTF_8)
        assertFalse(text.contains("appearance"))
        assertFalse(text.contains("device-1"))
        assertNull(LookSync.open(ByteArray(32) { 9 }, blob))
    }

    @Test fun `lo que no es un documento no es nada`() {
        assertNull(LookSync.open(key, byteArrayOf(1, 2, 3)))
        assertNull(LookSync.open(key, "{}".toByteArray()))
        assertNull(LookSync.open(key, ByteArray(0)))
    }

    @Test fun `un campo que falta, sobra o no vale deja el ajuste como estaba`() {
        val look = Json.parseToJsonElement("""{"newNoteTone":"x","colorAssignment":99,"appearance":999,"someFutureField":42,"uniformNoteColor":"red"}""").jsonObject
        val applied = LookSync.apply(base.copy(newNoteTone = 1, colorAssignment = 3), look)
        assertEquals(base.copy(newNoteTone = 1, colorAssignment = 3), applied)
    }

    @Test fun `null explicito vacia lo que puede estar vacio`() {
        val look = Json.parseToJsonElement("""{"squareCorners":null,"uniformNoteColor":null,"aspectColors":null}""").jsonObject
        val applied = LookSync.apply(base.copy(squareCorners = true, uniformNoteColor = "#112233", aspectColors = mapOf("a" to mapOf("b" to "#000000"))), look)
        assertNull(applied.squareCorners)
        assertNull(applied.uniformNoteColor)
        assertNull(applied.aspectColors)
    }

    @Test fun `los colores validos entran en mayusculas`() {
        val look = Json.parseToJsonElement("""{"uniformNoteColor":"#abcdef","aspectColors":{"bash":{"bg":"#0a0b0c","x":"nope"},"junk":5}}""").jsonObject
        val applied = LookSync.apply(base, look)
        assertEquals("#ABCDEF", applied.uniformNoteColor)
        assertEquals(mapOf("bash" to mapOf("bg" to "#0A0B0C")), applied.aspectColors)
    }

    @Test fun `el hash cambia con el aspecto y no con otra cosa`() {
        assertEquals(LookSync.hash(base), LookSync.hash(base.copy()))
        assertNotEquals(LookSync.hash(base), LookSync.hash(base.copy(appearance = 1)))
    }

    @Test fun `las cuatro ramas de la regla`() {
        assertEquals(LookAction.PUSH, LookSync.decide("new", "old", t0, t0.plusSeconds(3600)))
        assertEquals(LookAction.PULL, LookSync.decide("same", "same", t0, t0.plusSeconds(60)))
        assertEquals(LookAction.NONE, LookSync.decide("same", "same", t0, t0))
        assertEquals(LookAction.NONE, LookSync.decide("same", "same", t0, null))
        assertEquals(LookAction.PULL, LookSync.decide("mine", null, null, t0))
        assertEquals(LookAction.PUSH, LookSync.decide("mine", null, null, null))
    }
}
