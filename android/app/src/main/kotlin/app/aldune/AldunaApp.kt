package app.aldune

import android.app.Application

/** El contenedor es uno por proceso: la interfaz y el trabajador de segundo plano de la sync lo comparten (una sola base, una sola sync). */
class AldunaApp : Application() {
    val container: AppContainer by lazy { AppContainer(this) }
}
