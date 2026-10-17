package app.aldune.text

/** Un tema de color de nota: paleta oscura y clara. Port de `NoteTheme`. */
data class NoteTheme(
    val id: String = "",
    val name: String = "",
    val darkColors: List<String> = emptyList(),
    val lightColors: List<String> = emptyList(),
    val isBuiltIn: Boolean = false,
)

/** Qué paleta usar al dar color a una nota nueva. El orden del enum es el que usan los vectores (0 claro, 1 oscuro, 2 ambos). */
enum class NoteTone { Light, Dark, Both }

/** Regla de reparto (0 rotar evitando vecinas, 1 rotar, 2 más distinto, 3 fijo). */
enum class NoteColorAssignment { RotateAvoidNeighbors, Rotate, MostDistinct, Fixed }
