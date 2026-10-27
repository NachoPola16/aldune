package app.aldune.core

enum class PasswordProblem { None, Empty, TooShort, Mismatch }

/**
 * Reglas de la contraseña de una nota protegida (port de `PasswordRules` de Core). Al elegir una nueva (hay confirmación) se exige un
 * mínimo y que las dos coincidan; al desbloquear solo se rechaza la vacía: la que vale es la que se eligió en su día, y un mínimo
 * aquí dejaría fuera una nota protegida con reglas anteriores.
 */
object PasswordRules {
    // Igual que Core (8): ProtectedNotes aún acepta desde 4 para no dejar fuera una nota protegida con reglas anteriores.
    const val MIN_LENGTH = 8

    /** @param confirmation la repetición, o `null` si se está desbloqueando. */
    fun check(password: String, confirmation: String?): PasswordProblem {
        if (password.isEmpty()) return PasswordProblem.Empty
        if (confirmation == null) return PasswordProblem.None
        if (password.length < MIN_LENGTH) return PasswordProblem.TooShort
        return if (password == confirmation) PasswordProblem.None else PasswordProblem.Mismatch
    }
}
