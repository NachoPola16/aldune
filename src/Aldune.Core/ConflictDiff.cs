namespace Aldune.Core;

/// <summary>Qué cambia entre dos versiones de una nota en conflicto: la primera línea distinta y cuántas
/// hay en total. Una línea que falta en una de las dos cuenta como vacía.</summary>
public sealed record ConflictDiffSummary(int Line, string WinnerLine, string LosingLine, int DifferingLines);

public static class ConflictDiff
{
    /// <summary>Compara línea a línea (por posición, sin alinear: basta para decir "dónde"). Null si el texto
    /// es el mismo; los finales de línea de Windows y de Unix cuentan igual.</summary>
    public static ConflictDiffSummary? Summarize(string winnerText, string losingText)
    {
        var winner = winnerText.Replace("\r\n", "\n").Split('\n');
        var losing = losingText.Replace("\r\n", "\n").Split('\n');

        int first = 0, count = 0;
        string firstWinner = "", firstLosing = "";
        for (int i = 0; i < Math.Max(winner.Length, losing.Length); i++)
        {
            string w = i < winner.Length ? winner[i] : "";
            string l = i < losing.Length ? losing[i] : "";
            if (w == l) continue;
            if (count++ == 0) { first = i + 1; firstWinner = w; firstLosing = l; }
        }
        return count == 0 ? null : new ConflictDiffSummary(first, firstWinner, firstLosing, count);
    }

    /// <summary>Recorta una línea para mostrarla en una fila: sin espacios en los extremos y con «…» si se pasa.</summary>
    public static string Clip(string line, int max)
    {
        var trimmed = line.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
    }
}
