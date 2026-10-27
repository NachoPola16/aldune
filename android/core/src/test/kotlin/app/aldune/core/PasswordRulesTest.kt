package app.aldune.core

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
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
        assertEquals(PasswordProblem.TooShort, PasswordRules.check("abcdefg", "abcdefg"))
        assertEquals(PasswordProblem.None, PasswordRules.check("a".repeat(PasswordRules.MIN_LENGTH), "a".repeat(PasswordRules.MIN_LENGTH)))
    }

    @Test
    fun `al elegir una nueva exige que coincidan`() {
        assertEquals(PasswordProblem.Mismatch, PasswordRules.check("abcdefgh", "abcdefgi"))
        assertEquals(PasswordProblem.Mismatch, PasswordRules.check("abcdefgh", ""))
        assertEquals(PasswordProblem.None, PasswordRules.check("abcdefgh", "abcdefgh"))
    }

    @Test
    fun `demasiado corta gana a no coincide`() {
        assertEquals(PasswordProblem.TooShort, PasswordRules.check("ab", "xy"))
    }

    @Test
    fun `el minimo al elegir es el de Core y no baja del suelo de ProtectedNotes`() {
        assertEquals(8, PasswordRules.MIN_LENGTH)
        assertTrue(PasswordRules.MIN_LENGTH >= ProtectedNotes.MIN_PASSWORD_LENGTH)
    }
}
