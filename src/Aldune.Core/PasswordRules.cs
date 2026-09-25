namespace Aldune.Core;

public enum PasswordProblem { None, Empty, TooShort, Mismatch }

/// <summary>
/// Reglas de la contraseña de una nota protegida. Al elegir una nueva (hay confirmación) se exige un
/// mínimo y que las dos coincidan; al desbloquear solo se rechaza la vacía: la que vale es la que se
/// eligió en su día, y un mínimo aquí dejaría fuera una nota protegida con reglas anteriores.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 4;

    /// <param name="confirmation">La repetición, o null si se está desbloqueando.</param>
    public static PasswordProblem Check(string password, string? confirmation)
    {
        if (password.Length == 0) return PasswordProblem.Empty;
        if (confirmation is null) return PasswordProblem.None;
        if (password.Length < MinLength) return PasswordProblem.TooShort;
        return password == confirmation ? PasswordProblem.None : PasswordProblem.Mismatch;
    }
}
