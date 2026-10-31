package app.aldune.ui

import androidx.annotation.StringRes
import app.aldune.R
import app.aldune.domain.CompletedTasksPolicy

/** El texto del elemento de menú que cicla el ajuste de tareas hechas de una nota; el propio texto dice el estado, sin avisos. */
@StringRes
fun doneTasksPolicyLabel(policy: Int): Int = when (CompletedTasksPolicy.normalize(policy)) {
    CompletedTasksPolicy.KEEP_IN_PLACE -> R.string.done_tasks_keep
    CompletedTasksPolicy.MOVE_TO_END -> R.string.done_tasks_end
    else -> R.string.done_tasks_default
}
