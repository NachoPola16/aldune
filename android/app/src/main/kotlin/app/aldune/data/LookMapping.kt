package app.aldune.data

import app.aldune.syncclient.LookValues
import app.aldune.text.NoteThemes

/** The look inside [AppSettings] and back: what the look document (`LookSync`) carries, in the names of Windows' configuration file. */
object LookMapping {
    fun toValues(s: AppSettings) = LookValues(
        appearance = s.appearance,
        aspectColors = s.aspectColors.takeIf { it.isNotEmpty() },
        squareCorners = s.squareCorners,
        // Android stores «fill the card with the note colour» (null = what the look decides); the document says «colour as a stripe», its inverse.
        noteColorAsStripe = s.fillNoteColor == false,
        syncSignal = s.showSyncSignal,
        uniformNoteColor = s.uniformNoteColor,
        noteTheme = s.noteThemeId,
        newNoteTone = if (s.darkNotes) 1 else 0,
        colorAssignment = s.colorRule,
        fixedNoteColor = s.fixedNoteColor,
    )

    fun apply(s: AppSettings, v: LookValues) = s.copy(
        appearance = v.appearance,
        aspectColors = v.aspectColors ?: emptyMap(),
        squareCorners = v.squareCorners,
        // Stripe means «do not fill»; not stripe leaves the look to decide (an explicit «fill» on this device stays as it was).
        fillNoteColor = if (v.noteColorAsStripe) false else s.fillNoteColor.takeIf { it == true },
        showSyncSignal = v.syncSignal,
        uniformNoteColor = v.uniformNoteColor,
        noteThemeId = v.noteTheme ?: NoteThemes.CLASSIC_ID,
        darkNotes = v.newNoteTone == 1,
        colorRule = v.colorAssignment,
        fixedNoteColor = v.fixedNoteColor,
    )
}
