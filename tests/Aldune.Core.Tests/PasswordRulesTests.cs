using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class PasswordRulesTests
{
    [Fact]
    public void Unlocking_OnlyRejectsAnEmptyPassword()
    {
        Assert.Equal(PasswordProblem.Empty, PasswordRules.Check("", confirmation: null));
        // Al desbloquear no se aplica el mínimo: decide la contraseña guardada, no la regla.
        Assert.Equal(PasswordProblem.None, PasswordRules.Check("abc", confirmation: null));
    }

    [Fact]
    public void ANewPassword_NeedsTheMinimumLength()
    {
        Assert.Equal(PasswordProblem.Empty, PasswordRules.Check("", confirmation: ""));
        Assert.Equal(PasswordProblem.TooShort, PasswordRules.Check("abcdefg", confirmation: "abcdefg"));
        Assert.Equal(PasswordProblem.None, PasswordRules.Check("abcdefgh", confirmation: "abcdefgh"));
    }

    [Fact]
    public void ANewPassword_MustMatchItsConfirmation()
    {
        Assert.Equal(PasswordProblem.Mismatch, PasswordRules.Check("abcdefgh", confirmation: "abcdefgi"));
        Assert.Equal(PasswordProblem.Mismatch, PasswordRules.Check("abcdefgh", confirmation: ""));
    }

    [Fact]
    public void TooShort_IsReportedBeforeAMismatch()
    {
        // Primero lo que hay que arreglar en la primera caja; si no, se pediría repetir una
        // contraseña que luego tampoco valdría.
        Assert.Equal(PasswordProblem.TooShort, PasswordRules.Check("ab", confirmation: "xy"));
    }

    [Fact]
    public void SpacesCount_AndAreNotTrimmed()
    {
        Assert.Equal(PasswordProblem.None, PasswordRules.Check("        ", confirmation: "        "));
        Assert.Equal(PasswordProblem.Mismatch, PasswordRules.Check("abcdefgh ", confirmation: "abcdefgh"));
    }
}
