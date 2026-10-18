package app.aldune.core

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

class PasswordRulesTest {
    @Test
    fun `la vacia se rechaza siempre`() {
        assertEquals(PasswordProblem.Empty, PasswordRules.check("", null))
        assertEquals(PasswordProblem.Empty, PasswordRules.check("", ""))
    }

    @Test
    fun `al desbloquear solo se rechaza la vacia`() {
        // Sin confirmación: una nota protegida con reglas anteriores no puede quedar fuera por un mínimo.
        assertEquals(PasswordProblem.None, PasswordRules.check("a", null))
        assertEquals(PasswordProblem.None, PasswordRules.check("abc", null))
    }

    @Test
    fun `al elegir una nueva exige el minimo`() {
        assertEquals(PasswordProblem.TooShort, PasswordRules.check("abc", "abc"))
        assertEquals(PasswordProblem.None, PasswordRules.check("a".repeat(PasswordRules.MIN_LENGTH), "a".repeat(PasswordRules.MIN_LENGTH)))
    }

    @Test
    fun `al elegir una nueva exige que coincidan`() {
        assertEquals(PasswordProblem.Mismatch, PasswordRules.check("abcd", "abce"))
        assertEquals(PasswordProblem.Mismatch, PasswordRules.check("abcd", ""))
        assertEquals(PasswordProblem.None, PasswordRules.check("abcd", "abcd"))
    }

    @Test
    fun `demasiado corta gana a no coincide`() {
        assertEquals(PasswordProblem.TooShort, PasswordRules.check("ab", "xy"))
    }

    @Test
    fun `el minimo coincide con el de ProtectedNotes`() {
        assertEquals(ProtectedNotes.MIN_PASSWORD_LENGTH, PasswordRules.MIN_LENGTH)
    }
}
