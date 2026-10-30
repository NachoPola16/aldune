package app.aldune.audio

import android.content.Context
import android.media.AudioAttributes
import android.media.AudioManager
import android.media.SoundPool
import android.app.NotificationManager
import android.os.Build
import app.aldune.R
import app.aldune.domain.SoundChoice
import app.aldune.domain.SoundEvent
import app.aldune.domain.SoundPlan
import app.aldune.domain.SoundSettings
import app.aldune.domain.TypingThrottle

/**
 * Los sonidos de papel y pluma de la app. Son sonidos de interfaz (`USAGE_ASSISTANCE_SONIFICATION`) y respetan el modo del teléfono:
 * en silencio o vibración no suena nada, ni con «No molestar» si no deja pasar los sonidos del sistema. Nunca lanza: si algo falla, la
 * app se queda en silencio.
 */
class UiSounds(context: Context, private val settings: () -> SoundSettings) {
    private val audio = context.applicationContext.getSystemService(Context.AUDIO_SERVICE) as AudioManager
    private val notifications = context.applicationContext.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
    private val typing = TypingThrottle()
    private val pool: SoundPool = SoundPool.Builder()
        .setMaxStreams(4)
        .setAudioAttributes(
            AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_ASSISTANCE_SONIFICATION).setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION).build(),
        )
        .build()

    // Se cargan al primer uso de cada uno: con los sonidos apagados (lo de fábrica) no se decodifica nada.
    private val loaded = HashMap<SoundChoice, Int>()
    private val ready = HashSet<Int>()

    init {
        pool.setOnLoadCompleteListener { _, id, status -> if (status == 0) synchronized(ready) { ready.add(id) } }
    }

    private val context = context.applicationContext

    /** Por qué el teléfono no dejaría oír un sonido de interfaz ahora mismo, o `null` si sonaría. */
    enum class Silence { RINGER, DO_NOT_DISTURB }

    fun silencedBy(): Silence? {
        if (audio.ringerMode != AudioManager.RINGER_MODE_NORMAL) return Silence.RINGER
        return when (notifications.currentInterruptionFilter) {
            NotificationManager.INTERRUPTION_FILTER_ALL, NotificationManager.INTERRUPTION_FILTER_UNKNOWN -> null
            // «Solo prioritarias» deja pasar los sonidos del sistema si el usuario marcó «Sonidos al tocar» (Android 9+).
            NotificationManager.INTERRUPTION_FILTER_PRIORITY ->
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P &&
                    notifications.notificationPolicy.priorityCategories and NotificationManager.Policy.PRIORITY_CATEGORY_SYSTEM != 0) null
                else Silence.DO_NOT_DISTURB
            else -> Silence.DO_NOT_DISTURB
        }
    }

    fun play(event: SoundEvent, nowMs: Long = System.currentTimeMillis()) {
        val current = settings()
        val choice = SoundPlan.resolve(current, event) ?: return
        if (silencedBy() != null) return
        if ((event == SoundEvent.TYPING || event == SoundEvent.TEXT_DELETED) && !typing.tryAcquire(nowMs)) return
        try {
            val id = loaded.getOrPut(choice) { pool.load(context, resourceOf(choice) ?: return, 1) }
            if (synchronized(ready) { id in ready }) pool.play(id, current.volumeFactor, current.volumeFactor, 1, 0, 1f)
        } catch (_: RuntimeException) {
            // Sin audio disponible: silencio, nunca un fallo.
        }
    }

    /** Para el botón «Probar» de Ajustes: suena aunque el interruptor general esté apagado, para oír qué hay elegido. */
    fun preview(choice: SoundChoice, volume: Float): Silence? {
        if (choice == SoundChoice.NONE) return null
        silencedBy()?.let { return it }
        try {
            val id = loaded.getOrPut(choice) { pool.load(context, resourceOf(choice) ?: return null, 1) }
            if (synchronized(ready) { id in ready }) pool.play(id, volume, volume, 1, 0, 1f)
        } catch (_: RuntimeException) {
        }
        return null
    }

    /** Decodifica los sonidos de antemano (al encender los sonidos o al abrir la app con ellos encendidos): así el primero ya suena. */
    fun preload() {
        if (!settings().enabled) return
        for (choice in SoundChoice.entries) {
            val res = resourceOf(choice) ?: continue
            loaded.getOrPut(choice) { pool.load(context, res, 1) }
        }
    }

    fun release() = pool.release()

    private fun resourceOf(choice: SoundChoice): Int? = when (choice) {
        SoundChoice.PAGE_TURN -> R.raw.aldune_pagina
        SoundChoice.PAPER_FOLD -> R.raw.aldune_pliegue
        SoundChoice.PEN_TAP -> R.raw.aldune_toque
        SoundChoice.PAPER_CRUMPLE -> R.raw.aldune_arruga
        SoundChoice.PEN_SCRATCH -> R.raw.aldune_rasguno
        SoundChoice.ERASER -> R.raw.aldune_goma
        SoundChoice.NONE -> null
    }
}
