package app.aldune.text

import java.util.Locale

/** El color uniforme que anula el de cada nota cuando el usuario lo activa. Port de `NoteDisplayColor`. */
object NoteDisplayColor {
    fun isActive(uniform: String?): Boolean = uniform != null && uniform.length == 7 && OklchColor.fromHex(uniform) != null

    fun resolve(color: String, uniform: String?): String = if (isActive(uniform)) uniform!!.uppercase(Locale.ROOT) else color
}
