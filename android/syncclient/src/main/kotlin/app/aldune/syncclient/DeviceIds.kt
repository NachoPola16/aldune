package app.aldune.syncclient

import java.util.UUID

/**
 * El identificador de esta instalación en la sync: 32 hex en minúsculas, el `Guid.ToString("N")` de Core.
 * Se genera **una vez** (lo guarda `app`) y no cambia ni al desconectar: las bases y las versiones firmadas con él
 * quedarían huérfanas, y las notas ya subidas se verían como de otro equipo.
 */
object DeviceIds {
    private val FORMAT = Regex("[0-9a-f]{32}")

    fun generate(): String = UUID.randomUUID().toString().replace("-", "")

    fun isValid(deviceId: String): Boolean = FORMAT.matches(deviceId)
}
