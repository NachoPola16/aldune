package app.aldune.syncclient

import app.aldune.core.ProfileCodes
import app.aldune.domain.NoteState
import app.aldune.domain.Tombstone
import app.aldune.sync.SyncScope
import java.util.Base64
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertContentEquals
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertIs
import kotlin.test.assertTrue

class ProfileInputTest {
    private val key = ByteArray(32) { (it * 3).toByte() }
    private val noteId = UUID.fromString(ID1)

    private fun code(
        scope: Int = ProfileCodes.SCOPE_ALL, transport: Int? = ProfileCodes.TRANSPORT_SERVER, url: String? = "https://notas.example.com/aldune/",
        name: String? = "Casa", tag: String? = null, ids: List<UUID> = emptyList(),
    ) = ProfileCodes.encode(key, scope, ids, name, transport, url, tag)

    @Test
    fun `un codigo con usuario@ en la url se rechaza como invalido`() {
        // ProfileCodes.encode ya lo rechaza, así que se fabrica el documento a mano como lo haría un atacante.
        fun b64(t: String) = Base64.getUrlEncoder().withoutPadding().encodeToString(t.toByteArray(Charsets.UTF_8))
        val doc = """{"Version":2,"Key":"${app.aldune.core.SyncKeyFormat.encode(key)}","Scope":0,"NoteIds":[],"Tag":null,""" +
            """"ProfileName":null,"Transport":1,"ServerUrl":"https://host-bueno.com@atacante.tld/"}"""
        assertEquals(ProfileParseError.INVALID, rejected("aldune-profile-v2:" + b64(doc)))
    }

    @Test
    fun `las credenciales no se imprimen en toString`() {
        assertEquals("Token(***)", ProfileCredentials.Token("secreto-123").toString())
        val basic = ProfileCredentials.Basic("nacho", "clave-123").toString()
        assertTrue("clave-123" !in basic && "nacho" in basic, basic)
    }

    private fun rejected(text: String) = assertIs<ProfileParse.Rejected>(parseProfileInput(text)).error

    // --- parseProfileInput ---

    @Test fun `un codigo de servidor pide el token y trae todo`() {
        val parsed = assertIs<ProfileParse.Server>(parseProfileInput(code(scope = ProfileCodes.SCOPE_TAG, tag = "viaje")))
        assertEquals("https://notas.example.com/aldune/", parsed.url)
        assertContentEquals(key, parsed.profile.key)
        assertEquals("Casa", parsed.profile.profileName)
        assertEquals(SyncScope.Tag("viaje"), parsed.scope)
    }

    @Test fun `un codigo de WebDAV pide usuario y contrasena`() {
        val parsed = assertIs<ProfileParse.WebDav>(parseProfileInput(code(transport = ProfileCodes.TRANSPORT_WEBDAV, url = "https://nube.example.com/dav/")))
        assertEquals("https://nube.example.com/dav/", parsed.url)
    }

    @Test fun `un codigo sin transporte pide elegir`() {
        val parsed = assertIs<ProfileParse.NeedsTransport>(parseProfileInput(code(transport = null, url = null)))
        assertContentEquals(key, parsed.profile.key)
    }

    @Test fun `el alcance de notas elegidas trae sus ids`() {
        val parsed = assertIs<ProfileParse.Server>(parseProfileInput(code(scope = ProfileCodes.SCOPE_CHOSEN, ids = listOf(noteId))))
        assertEquals(SyncScope.SelectedNotes(setOf(noteId)), parsed.scope)
        assertEquals(SyncScope.All, assertIs<ProfileParse.Server>(parseProfileInput(code())).scope)
    }

    @Test fun `un codigo de carpeta compartida no vale en el movil`() {
        assertEquals(ProfileParseError.FOLDER_NOT_SUPPORTED, rejected(code(transport = ProfileCodes.TRANSPORT_FOLDER, url = null)))
    }

    @Test fun `vacio o solo espacios es vacio`() {
        for (text in listOf("", "   ", "\n\t \r\n", "\"\"", "<>", "\u200B")) assertEquals(ProfileParseError.EMPTY, rejected(text), text)
    }

    @Test fun `otra cosa no es un codigo`() {
        for (text in listOf("hola", "https://example.com", "ALDUNE_SYNC_TOKEN=abc", "Mi código: " + code(), "aldune-profile-v1:abc", "profile:" + code())) {
            assertEquals(ProfileParseError.NOT_A_CODE, rejected(text), text)
        }
    }

    @Test fun `un codigo roto es invalido`() {
        val prefix = "aldune-profile-v2:"
        fun raw(json: String) = prefix + Base64.getUrlEncoder().withoutPadding().encodeToString(json.toByteArray())
        val goodKey = app.aldune.core.SyncKeyFormat.encode(key)
        val cases = mapOf(
            "sin payload" to prefix,
            "base64 roto" to prefix + "%%%%",
            "no es JSON" to raw("esto no es json"),
            "JSON de otro tipo" to raw("[1,2]"),
            "versión no soportada" to raw("""{"Version":9,"Key":"$goodKey","Scope":0}"""),
            "clave mala" to raw("""{"Version":2,"Key":"abc","Scope":0}"""),
            "sin clave" to raw("""{"Version":2,"Scope":0}"""),
            "alcance fuera de rango" to raw("""{"Version":2,"Key":"$goodKey","Scope":7}"""),
            "servidor sin url" to raw("""{"Version":2,"Key":"$goodKey","Scope":0,"Transport":1}"""),
            "truncado" to code().dropLast(12),
        )
        for ((name, text) in cases) assertEquals(ProfileParseError.INVALID, rejected(text), name)
    }

    @Test fun `se limpia lo que anade copiar de un chat o un correo`() {
        val c = code()
        val mid = c.length / 2
        val variants = mapOf(
            "espacios alrededor" to "   $c   ",
            "salto de línea final" to "$c\n",
            "partido en líneas" to c.chunked(20).joinToString("\n"),
            "partido con CRLF y sangría" to c.chunked(30).joinToString("\r\n  "),
            "tabuladores dentro" to c.substring(0, mid) + "\t" + c.substring(mid),
            "espacio duro dentro" to c.substring(0, mid) + "\u00A0" + c.substring(mid),
            "ancho cero dentro" to c.substring(0, mid) + "\u200B" + c.substring(mid) + "\uFEFF",
            "entre comillas" to "\"$c\"",
            "entre comillas tipográficas" to "“$c”",
            "entre comillas simples" to "'$c'",
            "entre ángulos" to "<$c>",
            "entre comillas invertidas" to "`$c`",
            "comillas y espacios" to "  \"  ${c.chunked(25).joinToString(" ")} \"  \n",
            "prefijo en mayúsculas" to c.replaceFirst("aldune-profile-v2:", "ALDUNE-PROFILE-V2:"),
        )
        for ((name, text) in variants) {
            val parsed = assertIs<ProfileParse.Server>(parseProfileInput(text), name)
            assertContentEquals(key, parsed.profile.key, name)
        }
    }

    @Test fun `el resultado no deja la clave en su texto`() {
        val shown = parseProfileInput(code()).toString()
        val hex = key.joinToString("") { "%02x".format(it) }
        assertTrue(hex !in shown && app.aldune.core.SyncKeyFormat.encode(key) !in shown)
    }

    @Test
    fun `http solo es de riesgo fuera de la red local`() {
        for (safe in listOf("https://notas.example.com/", "http://192.168.1.73:8100/", "http://10.0.0.5/", "http://172.16.0.1/", "http://172.31.255.1/",
            "http://localhost:8087/", "http://127.0.0.1/", "http://nas.local/", "http://nas/", "http://169.254.1.1/", "http://[::1]/"))
            assertTrue(!isCleartextOutsideLan(safe), safe)
        for (risky in listOf("http://notas.example.com/", "http://8.8.8.8/", "http://172.32.0.1/", "http://192.169.1.1/", "http://localhost.evil.com/"))
            assertTrue(isCleartextOutsideLan(risky), risky)
    }

    // --- validateCredentials ---

    private fun valid(check: CredentialsCheck) = assertIs<CredentialsCheck.Valid>(check)
    private fun errors(check: CredentialsCheck) = assertIs<CredentialsCheck.Invalid>(check).errors

    @Test fun `un token y una url correctos son validos y salen recortados`() {
        val ok = valid(validateCredentials("  https://notas.example.com/  ", ProfileCredentials.Token("  abc123 \n")))
        assertEquals("https://notas.example.com/", ok.url)
        assertEquals(ProfileCredentials.Token("abc123"), ok.credentials)
    }

    @Test fun `la linea entera del env se queda con el valor`() {
        for (pasted in listOf("ALDUNE_SYNC_TOKEN=abc123", "  ALDUNE_SYNC_TOKEN=abc123\n", "ALDUNE_SYNC_TOKEN= abc123 ")) {
            assertEquals(ProfileCredentials.Token("abc123"), valid(validateCredentials("http://x", ProfileCredentials.Token(pasted))).credentials, pasted)
        }
        assertEquals(setOf(CredentialsError.TOKEN_EMPTY), errors(validateCredentials("http://x", ProfileCredentials.Token("ALDUNE_SYNC_TOKEN="))))
    }

    @Test fun `http se admite porque el servidor de casa va por la LAN`() {
        valid(validateCredentials("http://192.168.1.50:8080/", ProfileCredentials.Token("t")))
        valid(validateCredentials("HTTPS://Host.example.com", ProfileCredentials.Token("t")))
    }

    @Test fun `las urls que no son absolutas http o https se rechazan`() {
        for (url in listOf("notas.example.com", "ftp://x.com/", "file:///c:/x", "/relativa", "http://", "https:///ruta", "http://con espacio.com", "javascript:alert(1)", "mailto:a@b.c")) {
            assertEquals(setOf(CredentialsError.URL_INVALID), errors(validateCredentials(url, ProfileCredentials.Token("t"))), url)
        }
        assertEquals(setOf(CredentialsError.URL_EMPTY), errors(validateCredentials("   ", ProfileCredentials.Token("t"))))
    }

    @Test fun `un token vacio o en blanco se rechaza y se avisan todos los fallos a la vez`() {
        assertEquals(setOf(CredentialsError.TOKEN_EMPTY), errors(validateCredentials("http://x", ProfileCredentials.Token("  \n"))))
        assertEquals(setOf(CredentialsError.URL_EMPTY, CredentialsError.TOKEN_EMPTY), errors(validateCredentials("", ProfileCredentials.Token(""))))
    }

    @Test fun `WebDAV exige usuario y contrasena, y solo recorta el usuario`() {
        val ok = valid(validateCredentials("https://nube.example.com/dav/", ProfileCredentials.Basic("  ana ", " clave con espacios ")))
        assertEquals(ProfileCredentials.Basic("ana", " clave con espacios "), ok.credentials)
        assertEquals(setOf(CredentialsError.USERNAME_EMPTY), errors(validateCredentials("http://x", ProfileCredentials.Basic("  ", "p"))))
        assertEquals(setOf(CredentialsError.PASSWORD_EMPTY), errors(validateCredentials("http://x", ProfileCredentials.Basic("ana", ""))))
        assertEquals(
            setOf(CredentialsError.URL_INVALID, CredentialsError.USERNAME_EMPTY, CredentialsError.PASSWORD_EMPTY),
            errors(validateCredentials("nube", ProfileCredentials.Basic("", ""))),
        )
    }

    // --- primera conexión ---

    private val store = InMemoryLocalSyncStore()

    @Test fun `sin notas ni lapidas se conecta sin preguntar`() {
        assertEquals(FirstConnectDecision.Proceed(dropLocalTombstones = false), firstConnectDecision(store.snapshot()))
    }

    @Test fun `con notas en cualquier estado pregunta con el numero`() {
        store.notes.put(note(ID1, state = NoteState.ACTIVE))
        assertEquals(FirstConnectDecision.AskUser(1), firstConnectDecision(store.snapshot()))
        store.notes.put(note(ID2, state = NoteState.ARCHIVED))
        store.notes.put(note(ID3, state = NoteState.TRASHED))
        assertEquals(FirstConnectDecision.AskUser(3), firstConnectDecision(store.snapshot()))
    }

    @Test fun `una sola nota en la papelera ya pregunta`() {
        store.notes.put(note(ID1, state = NoteState.TRASHED))
        assertEquals(FirstConnectDecision.AskUser(1), firstConnectDecision(store.snapshot()))
    }

    @Test fun `lapidas sin notas no preguntan y se descartan en silencio`() {
        store.notes.putTombstone(Tombstone(ID1, T0, Tombstone.LOCAL_DEVICE))
        val decision = firstConnectDecision(store.snapshot())
        assertEquals(FirstConnectDecision.Proceed(dropLocalTombstones = true), decision)
        completeFirstConnect(store, decision)
        assertTrue(store.snapshot().tombstones.isEmpty())
    }

    @Test fun `una nota creada entre la foto y el final de la conexion no se pierde al descartar lapidas`() {
        store.notes.putTombstone(Tombstone(ID1, T0, Tombstone.LOCAL_DEVICE))
        val decision = firstConnectDecision(store.snapshot())
        assertEquals(FirstConnectDecision.Proceed(dropLocalTombstones = true), decision)

        // El usuario escribe una nota mientras se guardaban los secretos del perfil.
        store.notes.put(note(ID2))
        completeFirstConnect(store, decision)

        val snapshot = store.snapshot()
        assertEquals(listOf(ID2), snapshot.notes.map { it.id })
        assertTrue(snapshot.tombstones.isEmpty())
    }

    @Test fun `subir conserva las notas y olvida las bases de un perfil anterior`() {
        store.notes.put(note(ID1))
        store.commit(LocalCommit(emptyList(), mapOf(ID1 to app.aldune.sync.SyncBase(T0.toWire(), "viejo")), emptyMap()))
        completeFirstConnect(store, firstConnectDecision(store.snapshot()), FirstConnectChoice.UPLOAD)
        assertEquals(listOf(ID1), store.snapshot().notes.map { it.id })
        assertTrue(store.snapshot().bases.isEmpty())
    }

    @Test fun `descartar borra todo lo local sin dejar lapidas`() {
        store.notes.put(note(ID1))
        store.notes.put(note(ID2))
        store.notes.delete(ID2, T0)
        completeFirstConnect(store, firstConnectDecision(store.snapshot()), FirstConnectChoice.DISCARD)
        val snapshot = store.snapshot()
        assertTrue(snapshot.notes.isEmpty() && snapshot.tombstones.isEmpty() && snapshot.bases.isEmpty())
    }

    @Test fun `con notas locales hace falta la eleccion`() {
        store.notes.put(note(ID1))
        assertFailsWith<IllegalArgumentException> { completeFirstConnect(store, firstConnectDecision(store.snapshot())) }
        assertEquals(1, store.snapshot().notes.size)
    }

    @Test fun `tras desconectar y volver a conectar con notas locales vuelve a preguntar`() {
        store.notes.put(note(ID1))
        store.forgetSyncState()
        assertEquals(FirstConnectDecision.AskUser(1), firstConnectDecision(store.snapshot()))
    }
}
