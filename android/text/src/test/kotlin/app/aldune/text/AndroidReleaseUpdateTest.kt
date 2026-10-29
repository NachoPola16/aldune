package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test

class AndroidReleaseUpdateTest {
    private fun release(tag: String, draft: Boolean = false, pre: Boolean = false) = """{"tag_name":"$tag","draft":$draft,"prerelease":$pre}"""
    private fun list(vararg items: String) = items.joinToString(",", "[", "]")

    @Test fun `avisa de la mas alta de Android e ignora las de Windows`() {
        val json = list(release("v2.0.0"), release("android-v1.0.1"), release("android-v1.0.0"))
        assertEquals(AndroidReleaseUpdate.Update("1.0.1", "android-v1.0.1"), AndroidReleaseUpdate.latest(json, "1.0.0"))
    }

    @Test fun `sin version nueva no avisa`() {
        assertNull(AndroidReleaseUpdate.latest(list(release("android-v1.0.1")), "1.0.1"))
        assertNull(AndroidReleaseUpdate.latest(list(release("android-v1.0.0")), "1.0.1"))
    }

    @Test fun `ignora borradores, pre-releases y sufijos rc`() {
        val json = list(release("android-v1.1.0", draft = true), release("android-v1.2.0", pre = true), release("android-v1.3.0-rc1"))
        assertNull(AndroidReleaseUpdate.latest(json, "1.0.0"))
    }

    @Test fun `compara por numeros y no por texto`() {
        assertEquals("1.10.0", AndroidReleaseUpdate.latest(list(release("android-v1.9.0"), release("android-v1.10.0")), "1.0.0")?.version)
    }

    @Test fun `una version actual con sufijo cuenta por sus numeros y un json roto no avisa`() {
        assertEquals("1.0.1", AndroidReleaseUpdate.latest(list(release("android-v1.0.1")), "1.0.0-rc9")?.version)
        assertNull(AndroidReleaseUpdate.latest("no es json", "1.0.0"))
        assertNull(AndroidReleaseUpdate.latest("{}", "1.0.0"))
        assertNull(AndroidReleaseUpdate.latest("[{\"tag_name\":\"android-v9.0.0\"}]", "1.0.0"))
    }

    @Test fun `un tag guardado solo avisa mientras siga siendo mas nuevo que la version instalada`() {
        assertEquals("1.0.2", AndroidReleaseUpdate.fromTag("android-v1.0.2", "1.0.1")?.version)
        assertNull(AndroidReleaseUpdate.fromTag("android-v1.0.2", "1.0.2"))
        assertNull(AndroidReleaseUpdate.fromTag("v2.0.0", "1.0.0"))
        assertNull(AndroidReleaseUpdate.fromTag(null, "1.0.0"))
    }
}
