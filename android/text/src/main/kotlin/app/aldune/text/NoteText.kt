package app.aldune.text

/** Una nota es un único texto cuya primera línea es el título; esto lo parte y lo une. Port de `NoteText`. */
object NoteText {
    private const val NEW_LINE = "\r\n"

    fun split(text: String): TitleBody {
        if (text.isEmpty()) return TitleBody("", "")

        val firstBreak = text.indexOf('\n')
        if (firstBreak < 0) return TitleBody(text.trimEndCr(), "")

        return TitleBody(text.substring(0, firstBreak).trimEndCr(), text.substring(firstBreak + 1))
    }

    fun join(title: String, body: String): String {
        if (body.isEmpty()) return title
        return title + NEW_LINE + body
    }
}

/** `(string Title, string Body)` de `NoteText.Split`. */
data class TitleBody(val title: String, val body: String)
