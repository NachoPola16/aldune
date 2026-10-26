package app.aldune.core

import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.jupiter.api.Assertions.assertArrayEquals
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.DynamicTest
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.TestFactory
import java.util.Base64
import java.util.UUID

class ProfileCodeTest {
    private val key = Vectors.keyBytes

    private fun nullable(e: kotlinx.serialization.json.JsonElement?): String? =
        if (e == null || e is JsonNull) null else e.jsonPrimitive.content

    private fun assertMatches(expected: kotlinx.serialization.json.JsonObject, data: SyncShareCodeData) {
        assertArrayEquals(key, data.key)
        assertEquals(expected.getValue("scope").jsonPrimitive.content.toInt(), data.scope)
        assertEquals(expected.getValue("noteIds").jsonArray.map { UUID.fromString(it.jsonPrimitive.content) }, data.noteIds)
        assertEquals(nullable(expected["profileName"]), data.profileName)
        assertEquals(nullable(expected["transport"])?.toInt(), data.transport)
        assertEquals(nullable(expected["serverUrl"]), data.serverUrl)
        assertEquals(nullable(expected["tag"]), data.tag)
    }

    private fun fromBase64Url(text: String) = String(Base64.getUrlDecoder().decode(text), Charsets.UTF_8)

    @TestFactory
    fun `decodifica los codigos de perfil de los vectores`() = Vectors.objects("shareCodes").mapIndexed { i, v ->
        DynamicTest.dynamicTest("código ${i + 1}") {
            assertTrue(ProfileCodes.isShareCode(v.str("code")))
            assertMatches(v.getValue("expected").jsonObject, ProfileCodes.decode(v.str("code")))
        }
    }

    @TestFactory
    fun `codificar los datos esperados reproduce exactamente el codigo de C#`() = Vectors.objects("shareCodes").mapIndexed { i, v ->
        DynamicTest.dynamicTest("código ${i + 1}") {
            val d = ProfileCodes.decode(v.str("code"))
            assertEquals(v.str("code"), ProfileCodes.encode(d.key, d.scope, d.noteIds, d.profileName, d.transport, d.serverUrl, d.tag))
        }
    }

    @Test
    fun `el codigo lleva claves en PascalCase y base64url sin relleno`() {
        val code = ProfileCodes.encode(key, ProfileCodes.SCOPE_ALL, emptyList(), "Casa", ProfileCodes.TRANSPORT_SERVER, "https://sync.example.com/")
        assertTrue(code.startsWith("aldune-profile-v2:"))
        val payload = code.removePrefix("aldune-profile-v2:")
        assertFalse(payload.contains('=') || payload.contains('+') || payload.contains('/'))
        val json = kotlinx.serialization.json.Json.parseToJsonElement(fromBase64Url(payload)).jsonObject
        assertEquals(listOf("Version", "Key", "Scope", "NoteIds", "Tag", "ProfileName", "Transport", "ServerUrl"), json.keys.toList())
        assertEquals("2", json.getValue("Version").jsonPrimitive.content)
        assertEquals(SyncKeyFormat.encode(key), json.getValue("Key").jsonPrimitive.content)
    }

    @Test
    fun `ida y vuelta con notas elegidas, etiqueta y webdav`() {
        val ids = listOf(UUID.randomUUID(), UUID.randomUUID(), ids0())
        val code = ProfileCodes.encode(key, ProfileCodes.SCOPE_CHOSEN, ids + ids, "  Mi perfil ñ  ",
            ProfileCodes.TRANSPORT_WEBDAV, "https://cloud.example.com/dav/", "trabajo")
        val d = ProfileCodes.decode(code)
        assertEquals(ids, d.noteIds) // sin duplicados y en orden
        assertEquals("Mi perfil ñ", d.profileName)
        assertEquals(ProfileCodes.TRANSPORT_WEBDAV, d.transport)
        assertEquals("trabajo", d.tag)
    }

    private fun ids0() = UUID.fromString("5c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f")

    @Test
    fun `no distingue mayusculas en el prefijo`() {
        val vector = Vectors.objects("shareCodes").first().str("code").removePrefix("aldune-profile-v2:")
        val code = "ALDUNE-PROFILE-V2:" + vector
        assertTrue(ProfileCodes.isShareCode(code))
        val d = ProfileCodes.decode(code)
        assertEquals("Casa", d.profileName)
        assertArrayEquals(key, d.key)
        // La versión del documento tiene que cuadrar con el prefijo.
        val asV1 = fromBase64Url(vector).replace("\"Version\":2", "\"Version\":1")
        val enc = Base64.getUrlEncoder().withoutPadding().encodeToString(asV1.toByteArray(Charsets.UTF_8))
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode("aldune-profile-v2:" + enc) }
    }

    @Test
    fun `tolera espacios alrededor del codigo`() {
        val code = Vectors.objects("shareCodes").first().str("code")
        assertEquals("Casa", ProfileCodes.decode("  $code \n").profileName)
    }

    @Test
    fun `findShareCode saca el codigo de un mensaje con mas texto`() {
        val code = Vectors.objects("shareCodes").first().str("code")
        assertEquals(code, ProfileCodes.findShareCode(code))
        assertEquals(code, ProfileCodes.findShareCode("  $code \n"))
        assertEquals(code, ProfileCodes.findShareCode("Mira, este es el codigo: $code gracias!"))
        assertEquals(code.replaceFirst("aldune-profile-v2:", "ALDUNE-PROFILE-V2:"), ProfileCodes.findShareCode(code.replaceFirst("aldune-profile-v2:", "ALDUNE-PROFILE-V2:")))
    }

    @Test
    fun `findShareCode no confunde un texto normal con un codigo`() {
        assertNull(ProfileCodes.findShareCode(null))
        assertNull(ProfileCodes.findShareCode(""))
        assertNull(ProfileCodes.findShareCode("comprar leche"))
        assertNull(ProfileCodes.findShareCode("aldune-profile-v2:"))
        assertNull(ProfileCodes.findShareCode("el prefijo aldune-profile-v2: a secas no vale"))
    }

    @Test
    fun `lo que no es un codigo de perfil se rechaza`() {
        assertFalse(ProfileCodes.isShareCode("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8"))
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode("otra-cosa:abc") }
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode("aldune-profile-v2:%%%") }
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode("aldune-profile-v2:" + b64("{}")) }
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode("aldune-profile-v2:" + b64("no json")) }
    }

    private fun b64(s: String) = Base64.getUrlEncoder().withoutPadding().encodeToString(s.toByteArray(Charsets.UTF_8))

    private fun doc(vararg overrides: Pair<String, String>): String {
        val base = linkedMapOf(
            "Version" to "2", "Key" to "\"${SyncKeyFormat.encode(key)}\"", "Scope" to "0", "NoteIds" to "[]",
            "Tag" to "null", "ProfileName" to "null", "Transport" to "null", "ServerUrl" to "null",
        )
        base.putAll(overrides)
        return "aldune-profile-v2:" + b64(base.entries.joinToString(",", "{", "}") { "\"${it.key}\":${it.value}" })
    }

    @Test
    fun `valida alcance, transporte, clave y servidor como Core`() {
        ProfileCodes.decode(doc()) // el documento base es válido
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode(doc("Scope" to "7")) }
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode(doc("Transport" to "9")) }
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode(doc("Key" to "\"\"")) }
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode(doc("Key" to "\"AAAA\"")) }
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode(doc("NoteIds" to "[\"no-es-guid\"]")) }
        // Servidor o WebDAV sin URL válida no sirven.
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode(doc("Transport" to "1")) }
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode(doc("Transport" to "2", "ServerUrl" to "\"ftp://x.com/\"")) }
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode(doc("ServerUrl" to "\"no es una url\"")) }
        // Carpeta (0) no necesita URL.
        assertNull(ProfileCodes.decode(doc("Transport" to "0")).serverUrl)
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode(doc("ProfileName" to "\"${"x".repeat(121)}\"")) }
        assertEquals(120, ProfileCodes.decode(doc("ProfileName" to "\"${"x".repeat(120)}\"")).profileName?.length)
    }

    @Test
    fun `las claves del documento distinguen mayusculas como Core (camelCase no vale)`() {
        val camel = "aldune-profile-v2:" + b64("""{"version":2,"key":"${SyncKeyFormat.encode(key)}","scope":0}""")
        assertThrows(SyncFormatException::class.java) { ProfileCodes.decode(camel) }
    }

    @Test
    fun `normaliza el nombre y la url al codificar`() {
        val d = ProfileCodes.decode(ProfileCodes.encode(key, 0, emptyList(), "   ", 1, "  HTTPS://Sync.Example.com  "))
        assertNull(d.profileName)
        assertEquals("https://sync.example.com/", d.serverUrl)
        assertThrows(SyncFormatException::class.java) { ProfileCodes.encode(key, 0, emptyList(), null, 1, "ftp://x/") }
        assertThrows(IllegalArgumentException::class.java) { ProfileCodes.encode(ByteArray(8), 0, emptyList()) }
    }

    @Test
    fun `no incluye nunca el token`() {
        val code = ProfileCodes.encode(key, 0, emptyList(), "Casa", 1, "https://sync.example.com/")
        assertFalse(fromBase64Url(code.removePrefix("aldune-profile-v2:")).contains("oken", ignoreCase = true))
    }
}
