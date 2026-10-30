namespace Aldune.Core;

/// <summary>Frena el sonido de escribir: como mucho uno cada 70 ms y solo con caracteres imprimibles.</summary>
public sealed class TypingThrottle
{
    public const int MinIntervalMs = 70;

    private long _lastMs = long.MinValue;

    public bool TryAcquire(long nowMs)
    {
        if (_lastMs != long.MinValue && nowMs - _lastMs < MinIntervalMs) return false;
        _lastMs = nowMs;
        return true;
    }

    /// <summary>Borrar, intro, tabulador y demás teclas de control no suenan como escritura.</summary>
    public static bool IsPrintable(char c) => !char.IsControl(c);
}

/// <summary>Ajuste de volumen de un WAV PCM de 16 bits sin pasar por la API de audio (SoundPlayer no tiene volumen).</summary>
public static class SoundWav
{
    private const int HeaderBytes = 44;

    /// <summary>Copia del WAV con las muestras multiplicadas por <paramref name="factor"/>. A volumen completo
    /// devuelve el mismo arreglo. Se asume la cabecera canónica de 44 bytes, la que genera <c>generar-sonidos.py</c>.</summary>
    public static byte[] Scale(byte[] wav, double factor)
    {
        if (factor >= 1.0 || wav.Length <= HeaderBytes) return wav;
        factor = Math.Max(0, factor);
        var result = (byte[])wav.Clone();
        for (var i = HeaderBytes; i + 1 < result.Length; i += 2)
        {
            var sample = (short)(result[i] | (result[i + 1] << 8));
            var scaled = (short)Math.Round(sample * factor);
            result[i] = (byte)(scaled & 0xFF);
            result[i + 1] = (byte)((scaled >> 8) & 0xFF);
        }
        return result;
    }
}
