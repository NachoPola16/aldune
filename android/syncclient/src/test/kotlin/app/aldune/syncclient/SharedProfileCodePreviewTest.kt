package app.aldune.syncclient

import app.aldune.core.ProfileCodes
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class SharedProfileCodePreviewTest {
    private val key = ByteArray(32) { it.toByte() }

    private fun code(transport: Int?, url: String?, name: String?) =
        ProfileCodes.encode(key, ProfileCodes.SCOPE_ALL, emptyList(), name, transport, url, null)

    @Test
    fun `a server code shows its host and profile name`() {
        val p = SharedProfileCodePreview.of(code(ProfileCodes.TRANSPORT_SERVER, "https://Notas.Example.com:8443/aldune/", "Casa"))
        assertEquals(SharedProfileCodePreview("notas.example.com", "Casa"), p)
    }

    @Test
    fun `a webdav code without a name has a null name`() {
        val p = SharedProfileCodePreview.of(code(ProfileCodes.TRANSPORT_WEBDAV, "https://dav.example.org/x/", null))
        assertEquals(SharedProfileCodePreview("dav.example.org", null), p)
    }

    @Test
    fun `an odd host is shown as it is, not trusted or rewritten`() {
        val p = SharedProfileCodePreview.of(code(ProfileCodes.TRANSPORT_SERVER, "http://192.168.1.5/", "x"))
        assertEquals("192.168.1.5", p?.host)
    }

    @Test
    fun `invalid or hostless codes give no preview so the current flow applies`() {
        assertNull(SharedProfileCodePreview.of("aldune-profile-v2:not-a-code"))
        assertNull(SharedProfileCodePreview.of("hello"))
        assertNull(SharedProfileCodePreview.of(code(null, null, "Old")))
    }
}
