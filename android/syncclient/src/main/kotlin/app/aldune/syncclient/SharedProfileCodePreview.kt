package app.aldune.syncclient

import java.net.URI

/**
 * What the user must be told before a profile code that arrived through another app's "Share" opens "Add profile": any app can craft
 * such a code, and continuing would sync the notes with the server it names. Only the host and the name are exposed; the key never is.
 */
data class SharedProfileCodePreview(val host: String, val profileName: String?) {
    companion object {
        /** `null` when the code is invalid or names no server: nothing to confirm, so the existing flow (which reports the error) applies. */
        fun of(code: String): SharedProfileCodePreview? {
            val parsed = parseProfileInput(code) as? ProfileParse.Parsed ?: return null
            val url = (parsed as? ProfileParse.Server)?.url ?: (parsed as? ProfileParse.WebDav)?.url ?: return null
            val host = runCatching { URI(url).host }.getOrNull()?.takeIf { it.isNotEmpty() } ?: return null
            return SharedProfileCodePreview(host, parsed.profile.profileName)
        }
    }
}
