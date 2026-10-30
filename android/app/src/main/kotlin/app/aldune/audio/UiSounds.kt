package app.aldune.audio

import android.content.Context
import android.media.AudioAttributes
import android.media.AudioManager
import android.media.SoundPool
import app.aldune.R
import app.aldune.domain.SoundChoice
import app.aldune.domain.SoundEvent
import app.aldune.domain.SoundPlan
import app.aldune.domain.SoundSettings
import app.aldune.domain.TypingThrottle

/**
 * Los sonidos de papel y pluma de la app. Son sonidos de interfaz (`USAGE_ASSISTANCE_SONIFICATION`) y respetan el modo del teléfono:
 * en silencio o vibración no suena nada. Nunca lanza: si algo falla, la app se queda en silencio.
 */
class UiSounds(context: Context, private val settings: () -> SoundSettings) {
    private val audio = context.applicationContext.getSystemService(Context.AUDIO_SERVICE) as AudioManager
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

    fun play(event: SoundEvent, nowMs: Long = System.currentTimeMillis()) {
        val current = settings()
        val choice = SoundPlan.resolve(current, event) ?: return
        if (audio.ringerMode != AudioManager.RINGER_MODE_NORMAL) return
        if (event == SoundEvent.TYPING && !typing.tryAcquire(nowMs)) return
        try {
            val id = loaded.getOrPut(choice) { pool.load(context, resourceOf(choice) ?: return, 1) }
            if (synchronized(ready) { id in ready }) pool.play(id, current.volumeFactor, current.volumeFactor, 1, 0, 1f)
        } catch (_: RuntimeException) {
            // Sin audio disponible: silencio, nunca un fallo.
        }
    }

    /** Para el botón «Probar» de Ajustes: suena aunque el interruptor general esté apagado, para oír qué hay elegido. */
    fun preview(choice: SoundChoice, volume: Float) {
        if (choice == SoundChoice.NONE || audio.ringerMode != AudioManager.RINGER_MODE_NORMAL) return
        try {
            val id = loaded.getOrPut(choice) { pool.load(context, resourceOf(choice) ?: return, 1) }
            if (synchronized(ready) { id in ready }) pool.play(id, volume, volume, 1, 0, 1f)
        } catch (_: RuntimeException) {
        }
    }

    /** Decodifica los cinco sonidos de antemano (al encender los sonidos o al abrir la app con ellos encendidos): así el primero ya suena. */
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
        SoundChoice.NONE -> null
    }
}
