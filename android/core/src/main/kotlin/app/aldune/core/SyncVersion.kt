package app.aldune.core

import java.time.OffsetDateTime

/** Versión de una nota en el almacén: quién y cuándo. */
data class SyncVersion(val updatedAt: OffsetDateTime, val deviceId: String) {
    companion object {
        fun of(envelope: SyncEnvelope) = SyncVersion(envelope.updatedAt, envelope.deviceId)

        /**
         * Orden total y estable de Core (`SyncVersion.Compare`): primero el instante de `updatedAt`
         * (sin importar el desfase) y, en empate, `deviceId` por unidades UTF-16 una a una
         * (`String.CompareOrdinal`; `String.compareTo` de Java hace exactamente eso: ni cultural
         * ni por punto de código). Devuelve solo -1, 0 o 1.
         */
        fun compare(left: SyncVersion, right: SyncVersion): Int {
            val time = left.updatedAt.toInstant().compareTo(right.updatedAt.toInstant())
            return Integer.signum(if (time != 0) time else left.deviceId.compareTo(right.deviceId))
        }
    }
}
