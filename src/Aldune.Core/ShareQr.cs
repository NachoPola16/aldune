using QRCoder;
using QRCoder.Exceptions;

namespace Aldune.Core;

/// <summary>
/// El QR de un código de perfil: la matriz de módulos (negro = <c>true</c>), ya con la zona de silencio de 4 módulos que exigen los
/// lectores. <see cref="Modules"/> es <c>null</c> si el código no cabe en un QR; entonces la interfaz ofrece copiarlo, como siempre.
/// </summary>
public sealed record ShareQrResult(bool[,]? Modules)
{
    public bool TooLarge => Modules is null;

    public int Size => Modules?.GetLength(0) ?? 0;
}

public static class ShareQr
{
    private const string Prefix = "aldune-profile-v2:";

    /// <summary>
    /// Nivel de corrección M: aguanta un reflejo o una mota en el monitor y deja sitio para unas ~2.300 bytes. Solo códigos de perfil: es un
    /// error de programa pasar otra cosa, porque el QR enseña en pantalla una clave que no debe mezclarse con nada más.
    /// </summary>
    public static ShareQrResult Build(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        code = code.Trim();
        if (!code.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Not an Aldune profile code.", nameof(code));

        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(code, QRCodeGenerator.ECCLevel.M);
            var matrix = data.ModuleMatrix;
            int size = matrix.Count;
            var modules = new bool[size, size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    modules[y, x] = matrix[y][x];
            return new ShareQrResult(modules);
        }
        catch (DataTooLongException)
        {
            return new ShareQrResult((bool[,]?)null);
        }
    }
}
