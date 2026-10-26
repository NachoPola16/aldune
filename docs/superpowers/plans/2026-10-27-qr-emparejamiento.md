# Emparejar por QR Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Windows muestra el código de perfil como QR y Android lo escanea para rellenar el campo del código, sin cambiar el formato de sync.

**Architecture:** Windows genera la matriz de módulos con QRCoder (lógica pura en `Aldune.Core`) y la pinta en una ventana propia con cuenta atrás de 2 min. Android abre una `QrScanActivity` (CameraX + decodificador ZXing `core` en `android/syncclient`) que devuelve el código a «Añadir perfil», por el mismo camino que «Pegar».

**Tech Stack:** C# / WPF / xUnit; QRCoder 1.8.1 (MIT). Kotlin / Compose; CameraX 1.6.2; `com.google.zxing:core` 3.5.4 (Apache-2.0).

**Spec:** `docs/superpowers/specs/2026-10-27-qr-emparejamiento-design.md`

## Global Constraints

- El token nunca va dentro del QR; el QR transporta solo el código `aldune-profile-v2:…` de siempre. Cero cambios de formato de sync.
- Textos de interfaz de Windows siempre con `Strings.T(en, es, de, fr, pt)`; los de Android en `android/strings.tsv` y `python android/scripts/gen_strings.py` (`--check` debe pasar). Cinco idiomas: en, es, de, fr, pt-BR.
- Comentarios en español, explicando el porqué.
- Los `.cs`/`.xaml` llevan BOM UTF-8; no mezclar dos BOM.
- Android: `minSdk` 26, `targetSdk` 36; sin Google Play Services ni ML Kit ni binarios nativos (F-Droid/IzzyOnDroid).
- El QR de Windows es siempre negro sobre blanco con zona de silencio de 4 módulos, también en los aspectos oscuros; no se guarda en disco ni va al portapapeles; la ventana se cierra sola a los 120 s.
- Android: la cámara se pide solo al pulsar «Escanear QR»; `uses-feature camera` con `required="false"`; no se guardan ni se envían fotogramas; la pantalla del escáner lleva `FLAG_SECURE`.
- Commits de `NachoPola16 <nachopolac@gmail.com>`, sin `Co-Authored-By`; `git add` nombrando ficheros. No hacer push, tag ni release sin preguntar.
- Nunca ejecutar la app de desarrollo contra `%LOCALAPPDATA%\Aldune`; no capturar la pantalla de inicio ni las notificaciones del móvil.

## Review Focus

1. Un perfil con **muchas notas elegidas** (código de ~700+ caracteres): tiene que dar un QR válido, no «demasiado grande»; y uno de verdad enorme (>2.300 bytes) debe decir «demasiado grande» sin lanzar excepción. (Task 1)
2. Un **código que no es de perfil** pasado a `ShareQr.Build` (vacío, otro prefijo): error claro, nunca un QR con otra cosa. (Task 1)
3. **QR con colores invertidos** (claro sobre oscuro) y con **stride de fila mayor que el ancho** (lo normal en CameraX): el decodificador tiene que leerlos. (Task 3)
4. Un **QR que no es de Aldune** (una web, un Wi-Fi) o una imagen sin QR: la pantalla de escaneo lo ignora y sigue buscando, sin cerrar ni rellenar el campo. (Tasks 3 y 4)
5. **Permiso de cámara denegado** o sin cámara: la pantalla lo explica y deja volver a «Pegar», sin cuelgue ni pantalla negra. (Task 4)

---

### Task 1: `ShareQr` en Core (matriz de módulos)

**Files:**
- Modify: `src/Aldune.Core/Aldune.Core.csproj` (añadir QRCoder)
- Create: `src/Aldune.Core/ShareQr.cs`
- Test: `tests/Aldune.Core.Tests/ShareQrTests.cs`

**Interfaces:**
- Produces: `ShareQrResult` (`bool[,]? Modules`, `bool TooLarge`, `int Size`) y `ShareQr.Build(string code) : ShareQrResult`. `Modules[y, x]` es `true` si el módulo es negro; **ya incluye la zona de silencio de 4 módulos** (comprobado con QRCoder 1.8.1: un código de 28 caracteres da 33×33 = 25 + 2×4).

- [ ] **Step 1: Write the failing tests**

```csharp
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class ShareQrTests
{
    private const string Prefix = "aldune-profile-v2:";
    private static string CodeOf(int payloadChars) => Prefix + new string('a', payloadChars);

    [Fact]
    public void Build_ARealSizedCode_GivesASquareMatrixWithQuietZone()
    {
        var result = ShareQr.Build(CodeOf(230));

        Assert.False(result.TooLarge);
        var m = result.Modules!;
        Assert.Equal(m.GetLength(0), m.GetLength(1));
        Assert.Equal(result.Size, m.GetLength(0));
        Assert.True(result.Size >= 21 + 8);
        // La zona de silencio (4 módulos) es blanca y el primer módulo del localizador (esquina superior izquierda) es negro.
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < result.Size; j++)
                Assert.False(m[i, j]);
        Assert.True(m[4, 4]);
    }

    [Fact]
    public void Build_IsDeterministic()
    {
        var a = ShareQr.Build(CodeOf(300)).Modules!;
        var b = ShareQr.Build(CodeOf(300)).Modules!;
        Assert.Equal(a.Length, b.Length);
        for (int y = 0; y < a.GetLength(0); y++)
            for (int x = 0; x < a.GetLength(1); x++)
                Assert.Equal(a[y, x], b[y, x]);
    }

    [Fact]
    public void Build_AProfileWithManyChosenNotes_StillFitsInOneQr()
    {
        // ~40 notas elegidas ≈ 700 caracteres de más: no puede salir «demasiado grande».
        var result = ShareQr.Build(CodeOf(250 + 40 * 50));
        Assert.False(result.TooLarge);
    }

    [Fact]
    public void Build_AHugeCode_ReportsTooLargeInsteadOfThrowing()
    {
        var result = ShareQr.Build(CodeOf(3000));
        Assert.True(result.TooLarge);
        Assert.Null(result.Modules);
        Assert.Equal(0, result.Size);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://example.com")]
    [InlineData("aldune-profile-v1:abc")]
    public void Build_ANonProfileCode_Throws(string text)
    {
        Assert.Throws<ArgumentException>(() => ShareQr.Build(text));
    }

    [Fact]
    public void Build_NullThrows() => Assert.Throws<ArgumentNullException>(() => ShareQr.Build(null!));

    [Fact]
    public void Build_TrimsAndIgnoresPrefixCase()
    {
        var plain = ShareQr.Build(CodeOf(100)).Modules!;
        var padded = ShareQr.Build("  " + CodeOf(100) + "\n").Modules!;
        Assert.Equal(plain.GetLength(0), padded.GetLength(0));
        Assert.NotNull(ShareQr.Build("ALDUNE-PROFILE-V2:" + new string('a', 100)).Modules);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Aldune.Core.Tests --filter "FullyQualifiedName~ShareQrTests"`
Expected: no compila (`ShareQr` no existe).

- [ ] **Step 3: Add the dependency and the implementation**

En `src/Aldune.Core/Aldune.Core.csproj`, junto a los otros `PackageReference`:

```xml
<PackageReference Include="QRCoder" Version="1.8.1" />
```

`src/Aldune.Core/ShareQr.cs` (con BOM UTF-8):

```csharp
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
```

Nota: el test de espacios en blanco (`"   "`) debe lanzar `ArgumentException`: tras `Trim()` queda `""`, que no empieza por el prefijo. El de `null` lanza `ArgumentNullException` (subclase de `ArgumentException`, pero el test usa el tipo exacto).

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/Aldune.Core.Tests --filter "FullyQualifiedName~ShareQrTests"`
Expected: PASS (11 casos).

- [ ] **Step 5: Commit**

```bash
git add src/Aldune.Core/Aldune.Core.csproj src/Aldune.Core/ShareQr.cs tests/Aldune.Core.Tests/ShareQrTests.cs
git commit -m "Core: ShareQr, la matriz de modulos del QR de un codigo de perfil"
```

---

### Task 2: Windows, «Mostrar QR» (ventana y botón)

**Files:**
- Create: `src/Aldune/Windowing/ShareQrWindow.cs` (ventana en código, sin XAML; con BOM)
- Modify: `src/Aldune/Resources/Strings.cs` (cadenas nuevas, junto a `SyncShareProfileButton`)
- Modify: `src/Aldune/Windowing/SettingsWindow.xaml` (botón tras `SyncShareProfileButton`, ~línea 848)
- Modify: `src/Aldune/Windowing/SettingsWindow.xaml.cs` (manejador junto a `OnSyncShareProfileClick`, ~línea 1205)

**Interfaces:**
- Consumes: `ShareQr.Build(string) : ShareQrResult` (Task 1), `SyncService.GetOrCreateShareCode()` (ya existe).
- Produces: `ShareQrWindow(Window owner, ShareQrResult qr)` (`ShowDialog()`).

- [ ] **Step 1: Add the strings**

En `Strings.cs`, tras `SyncShareProfileButton`:

```csharp
    public static string SyncShowQrButton => T("Show QR", "Mostrar QR", "QR anzeigen", "Afficher le QR", "Mostrar QR");
    public static string QrWindowTitle => T("Profile QR", "QR del perfil", "Profil-QR", "QR du profil", "QR do perfil");
    public static string QrWindowHint => T("On your phone: Aldune → Settings → Sync → Add profile → Scan QR.",
        "En el móvil: Aldune → Ajustes → Sincronización → Añadir perfil → Escanear QR.",
        "Auf dem Handy: Aldune → Einstellungen → Synchronisierung → Profil hinzufügen → QR scannen.",
        "Sur le téléphone : Aldune → Réglages → Synchronisation → Ajouter un profil → Scanner le QR.",
        "No celular: Aldune → Configurações → Sincronização → Adicionar perfil → Escanear QR.");
    public static string QrWindowWarning => T("This QR contains the key to your notes. Show it only to your own devices.",
        "Este QR lleva la clave de tus notas. Enséñalo solo a tus propios dispositivos.",
        "Dieser QR enthält den Schlüssel zu deinen Notizen. Zeige ihn nur deinen eigenen Geräten.",
        "Ce QR contient la clé de tes notes. Ne le montre qu'à tes propres appareils.",
        "Este QR contém a chave das suas notas. Mostre-o apenas aos seus próprios dispositivos.");
    public static string QrWindowCountdown(int seconds) => T($"Closes in {seconds} s", $"Se cierra en {seconds} s",
        $"Schließt in {seconds} s", $"Se ferme dans {seconds} s", $"Fecha em {seconds} s");
    public static string QrWindowClose => T("Close", "Cerrar", "Schließen", "Fermer", "Fechar");
    public static string QrTooLargeStatus => T("This profile is too large for a QR. Copy the code instead.",
        "Este perfil es demasiado grande para un QR. Copia el código.",
        "Dieses Profil ist zu groß für einen QR. Kopiere stattdessen den Code.",
        "Ce profil est trop grand pour un QR. Copie plutôt le code.",
        "Este perfil é grande demais para um QR. Copie o código.");
```

- [ ] **Step 2: Write the window**

`src/Aldune/Windowing/ShareQrWindow.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Aldune.Core;
using Aldune.Resources;

namespace Aldune.Windowing;

/// <summary>
/// Enseña el QR de un código de perfil. Lleva la clave de cifrado, así que: va siempre en negro sobre blanco (los lectores no leen bien un QR
/// claro sobre oscuro), avisa, no se guarda en ningún sitio y se cierra solo a los <see cref="LifetimeSeconds"/> segundos.
/// </summary>
internal sealed class ShareQrWindow : Window
{
    private const int LifetimeSeconds = 120;
    private const int TargetPixels = 340;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TextBlock _countdown = new();
    private int _remaining = LifetimeSeconds;

    public ShareQrWindow(Window owner, ShareQrResult qr)
    {
        if (qr.Modules is null) throw new ArgumentException("There is no QR to show.", nameof(qr));

        Owner = owner;
        Title = Strings.QrWindowTitle;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "AlduneGroundBrush");

        var panel = new StackPanel { Margin = new Thickness(24), Width = TargetPixels + 8 };
        panel.Children.Add(Text(Strings.QrWindowHint, 13, "AlduneTextStrongBrush", new Thickness(0, 0, 0, 12)));

        // Módulos de un número entero de píxeles y sin suavizado: con escalas fraccionarias aparecerían líneas entre módulos.
        int pixelsPerModule = Math.Max(2, TargetPixels / qr.Size);
        var image = new Image
        {
            Source = QrDrawing.ToImage(qr.Modules),
            Width = qr.Size * pixelsPerModule,
            Height = qr.Size * pixelsPerModule,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);
        panel.Children.Add(new Border { Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Child = image });

        panel.Children.Add(Text(Strings.QrWindowWarning, 12, "AlduneHintBrush", new Thickness(0, 12, 0, 0)));
        _countdown.FontSize = 12;
        _countdown.Margin = new Thickness(0, 6, 0, 12);
        _countdown.SetResourceReference(TextBlock.ForegroundProperty, "AlduneHintBrush");
        UpdateCountdown();
        panel.Children.Add(_countdown);

        var close = new Button { Content = Strings.QrWindowClose, IsDefault = true, IsCancel = true, Padding = new Thickness(14, 6, 14, 6), HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        panel.Children.Add(close);
        Content = panel;

        _timer.Tick += (_, _) =>
        {
            _remaining--;
            if (_remaining <= 0) { Close(); return; }
            UpdateCountdown();
        };
        Loaded += (_, _) => _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private void UpdateCountdown() => _countdown.Text = Strings.QrWindowCountdown(_remaining);

    private static TextBlock Text(string text, double size, string brushKey, Thickness margin)
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = margin };
        block.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return block;
    }
}

/// <summary>La matriz de módulos como imagen vectorial: un cuadrado blanco y un rectángulo negro por módulo.</summary>
internal static class QrDrawing
{
    public static DrawingImage ToImage(bool[,] modules)
    {
        int size = modules.GetLength(0);
        var dark = new GeometryGroup();
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (modules[y, x]) dark.Children.Add(new RectangleGeometry(new Rect(x, y, 1, 1)));

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, size, size))));
        group.Children.Add(new GeometryDrawing(Brushes.Black, null, dark));
        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
```

- [ ] **Step 3: Add the button and handler**

`SettingsWindow.xaml`, justo después del botón `SyncShareProfileButton`:

```xml
                            <Button x:Name="SyncShowQrButton" Content="{x:Static res:Strings.SyncShowQrButton}"
                                    Click="OnSyncShowQrClick" Margin="0,0,7,6" Padding="10,6"
                                    Style="{StaticResource RecordButtonStyle}" />
```

`SettingsWindow.xaml.cs`, tras `OnSyncShareProfileClick`:

```csharp
    private void OnSyncShowQrClick(object sender, RoutedEventArgs e)
    {
        if (_syncService is null) return;
        try
        {
            // El código no se copia al portapapeles ni se deja en el cuadro de texto: el QR es solo para enseñarlo un momento.
            var qr = ShareQr.Build(_syncService.GetOrCreateShareCode());
            if (qr.TooLarge)
            {
                SyncStatusText.Text = Strings.QrTooLargeStatus;
                return;
            }
            new ShareQrWindow(this, qr).ShowDialog();
        }
        catch (Exception ex)
        {
            SyncStatusText.Text = Strings.SyncErrorStatus(ex.Message);
        }
    }
```

- [ ] **Step 4: Build and verify with a probe**

Run: `dotnet build Aldune.slnx -c Debug` → 0 errores y solo los 4 avisos CA1416 conocidos.
Sonda desechable (fuera del repo, ver `docs/WPF_PROBES.md`): abrir `ShareQrWindow` con `ShareQr.Build("aldune-profile-v2:" + new string('a', 250))`, guardar una captura del recuadro del QR y comprobar con un decodificador (por ejemplo `zxing` en Python o el propio escáner de Android del Task 4) que lee el mismo texto; comprobar que la cuenta atrás baja y que a los 120 s se cierra (se puede probar con `LifetimeSeconds` temporalmente a 3 y volver a 120).

- [ ] **Step 5: Run the tests and commit**

Run: `dotnet test Aldune.slnx --no-build --filter "FullyQualifiedName!~SmokeTests"` → todo en verde.

```bash
git add src/Aldune/Windowing/ShareQrWindow.cs src/Aldune/Resources/Strings.cs src/Aldune/Windowing/SettingsWindow.xaml src/Aldune/Windowing/SettingsWindow.xaml.cs
git commit -m "Windows: Mostrar QR del perfil (ventana con aviso y cierre a los 2 minutos)"
```

---

### Task 3: Android, `QrDecoder` (ZXing core, en `syncclient`)

**Files:**
- Modify: `android/gradle/libs.versions.toml`, `android/syncclient/build.gradle.kts`
- Create: `android/syncclient/src/main/kotlin/app/aldune/syncclient/QrDecoder.kt`
- Test: `android/syncclient/src/test/kotlin/app/aldune/syncclient/QrDecoderTest.kt`

**Interfaces:**
- Produces: `QrDecoder.decodeText(luma: ByteArray, rowStride: Int, width: Int, height: Int): String?` — el texto del primer QR que encuentre en un plano de luminancia (Y de una imagen YUV_420_888), o `null`. Lee también QR con colores invertidos. No filtra por contenido: lo hace quien lo llama (`ProfileCodes.findShareCode`).

- [ ] **Step 1: Add the dependency**

`libs.versions.toml`: en `[versions]` `camerax = "1.6.2"` y `zxing = "3.5.4"`; en `[libraries]`:

```toml
zxing-core = { module = "com.google.zxing:core", version.ref = "zxing" }
androidx-camera-camera2 = { module = "androidx.camera:camera-camera2", version.ref = "camerax" }
androidx-camera-lifecycle = { module = "androidx.camera:camera-lifecycle", version.ref = "camerax" }
androidx-camera-view = { module = "androidx.camera:camera-view", version.ref = "camerax" }
```

`android/syncclient/build.gradle.kts`, en `dependencies`: `implementation(libs.zxing.core)`.

- [ ] **Step 2: Write the failing tests**

```kotlin
package app.aldune.syncclient

import com.google.zxing.BarcodeFormat
import com.google.zxing.EncodeHintType
import com.google.zxing.qrcode.QRCodeWriter
import com.google.zxing.qrcode.decoder.ErrorCorrectionLevel
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class QrDecoderTest {
    private val code = "aldune-profile-v2:" + "eyJWZXJzaW9uIjoyfQ".repeat(14)

    /** Un QR dibujado como lo vería la cámara: plano de luminancia con `stride` bytes por fila (negro 0, blanco 255). */
    private fun lumaOf(text: String, size: Int = 480, stride: Int = size, inverted: Boolean = false): ByteArray {
        val hints = mapOf(EncodeHintType.MARGIN to 4, EncodeHintType.ERROR_CORRECTION to ErrorCorrectionLevel.M)
        val matrix = QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, size, size, hints)
        val white = (if (inverted) 0 else 255).toByte()
        val black = (if (inverted) 255 else 0).toByte()
        val out = ByteArray(stride * size) { 127 }
        for (y in 0 until size) for (x in 0 until size) out[y * stride + x] = if (matrix[x, y]) black else white
        return out
    }

    @Test fun `lee un codigo de perfil de tamano normal`() {
        assertEquals(code, QrDecoder.decodeText(lumaOf(code), 480, 480, 480))
    }

    @Test fun `lee un codigo de perfil con muchas notas`() {
        val big = "aldune-profile-v2:" + "abcdefghijklmnopqrstuvwxyz0123456789-_".repeat(20)
        assertEquals(big, QrDecoder.decodeText(lumaOf(big, size = 900), 900, 900, 900))
    }

    @Test fun `lee con el stride de fila mayor que el ancho, como entrega CameraX`() {
        val luma = lumaOf(code, size = 480, stride = 512)
        assertEquals(code, QrDecoder.decodeText(luma, rowStride = 512, width = 480, height = 480))
    }

    @Test fun `lee un QR con los colores invertidos`() {
        assertEquals(code, QrDecoder.decodeText(lumaOf(code, inverted = true), 480, 480, 480))
    }

    @Test fun `devuelve el texto de cualquier QR, sin filtrar por contenido`() {
        assertEquals("https://example.com", QrDecoder.decodeText(lumaOf("https://example.com"), 480, 480, 480))
    }

    @Test fun `una imagen sin QR da null`() {
        assertNull(QrDecoder.decodeText(ByteArray(480 * 480) { 255.toByte() }, 480, 480, 480))
    }

    @Test fun `medidas imposibles dan null en vez de fallar`() {
        assertNull(QrDecoder.decodeText(ByteArray(10), 480, 480, 480)) // buffer demasiado corto
        assertNull(QrDecoder.decodeText(ByteArray(100), 5, 10, 10)) // stride menor que el ancho
        assertNull(QrDecoder.decodeText(ByteArray(0), 0, 0, 0))
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run (desde `android/`, con `JAVA_HOME` del JDK 21): `./gradlew :syncclient:test --tests "*QrDecoderTest*"`
Expected: no compila (`QrDecoder` no existe).

- [ ] **Step 4: Implement**

```kotlin
package app.aldune.syncclient

import com.google.zxing.BarcodeFormat
import com.google.zxing.BinaryBitmap
import com.google.zxing.ChecksumException
import com.google.zxing.DecodeHintType
import com.google.zxing.FormatException
import com.google.zxing.LuminanceSource
import com.google.zxing.MultiFormatReader
import com.google.zxing.NotFoundException
import com.google.zxing.PlanarYUVLuminanceSource
import com.google.zxing.common.HybridBinarizer

/**
 * Lee un QR de un fotograma de la cámara: el plano Y (luminancia) de una imagen YUV_420_888, que CameraX entrega con `rowStride` bytes por
 * fila (casi siempre mayor que el ancho). Solo QR, y con un segundo intento invertido: algunos monitores en modo oscuro lo enseñan claro
 * sobre oscuro. No decide si el texto es de Aldune; eso lo hace quien lo usa (`ProfileCodes.findShareCode`).
 */
object QrDecoder {
    private val hints = mapOf(
        DecodeHintType.POSSIBLE_FORMATS to listOf(BarcodeFormat.QR_CODE),
        DecodeHintType.TRY_HARDER to true,
    )

    fun decodeText(luma: ByteArray, rowStride: Int, width: Int, height: Int): String? {
        if (width <= 0 || height <= 0 || rowStride < width || luma.size < rowStride * (height - 1) + width) return null
        val source = PlanarYUVLuminanceSource(luma, rowStride, height, 0, 0, width, height, false)
        return read(source) ?: read(source.invert())
    }

    // Un lector por llamada: MultiFormatReader guarda estado y el análisis de CameraX puede ir en otro hilo.
    private fun read(source: LuminanceSource): String? = try {
        MultiFormatReader().decode(BinaryBitmap(HybridBinarizer(source)), hints).text
    } catch (e: NotFoundException) {
        null
    } catch (e: ChecksumException) {
        null
    } catch (e: FormatException) {
        null
    }
}
```

- [ ] **Step 5: Run and commit**

Run: `./gradlew :syncclient:test` → verde (incluye los de siempre).

```bash
git add android/gradle/libs.versions.toml android/syncclient/build.gradle.kts android/syncclient/src/main/kotlin/app/aldune/syncclient/QrDecoder.kt android/syncclient/src/test/kotlin/app/aldune/syncclient/QrDecoderTest.kt
git commit -m "Android: QrDecoder (ZXing core) para leer un QR de un fotograma de la camara"
```

---

### Task 4: Android, `QrScanActivity` (cámara, permiso y resultado)

**Files:**
- Modify: `android/app/build.gradle.kts` (dependencias CameraX), `android/app/src/main/AndroidManifest.xml`, `android/strings.tsv` (+ regenerar XML)
- Create: `android/app/src/main/kotlin/app/aldune/ui/QrScanActivity.kt`

**Interfaces:**
- Consumes: `QrDecoder.decodeText(...)` (Task 3), `ProfileCodes.findShareCode(String?)` (ya existe en `android/core`).
- Produces: `ScanQrContract : ActivityResultContract<Unit, String?>` — devuelve el código de perfil leído, o `null` si el usuario cancela o no hay permiso.

- [ ] **Step 1: Add dependencies, permission and activity**

`app/build.gradle.kts`, en `dependencies`:

```kotlin
    implementation(libs.androidx.camera.camera2)
    implementation(libs.androidx.camera.lifecycle)
    implementation(libs.androidx.camera.view)
```

`AndroidManifest.xml`, antes de `<application>` (junto al resto de `uses-permission`):

```xml
    <uses-permission android:name="android.permission.CAMERA" />
    <!-- La cámara solo sirve para escanear el QR de «Añadir perfil»: sin ella la app funciona igual (se pega el código). -->
    <uses-feature android:name="android.hardware.camera" android:required="false" />
```

Y dentro de `<application>`, tras `MainActivity`:

```xml
        <activity android:name=".ui.QrScanActivity" android:exported="false" android:screenOrientation="portrait" />
```

- [ ] **Step 2: Add the strings**

Añadir a `android/strings.tsv` (columnas `key`, en, es, de, fr, pt-BR, separadas por tabuladores) y ejecutar `python android/scripts/gen_strings.py`:

```
scan_qr	Scan QR	Escanear QR	QR scannen	Scanner le QR	Escanear QR
scan_qr_title	Point the camera at the QR shown in Aldune on your PC.	Apunta la cámara al QR que enseña Aldune en tu PC.	Richte die Kamera auf den QR, den Aldune an deinem PC anzeigt.	Pointe la caméra vers le QR affiché par Aldune sur ton PC.	Aponte a câmera para o QR que o Aldune mostra no seu PC.
scan_qr_not_aldune	That QR is not an Aldune profile code.	Ese QR no es un código de perfil de Aldune.	Dieser QR ist kein Aldune-Profilcode.	Ce QR n'est pas un code de profil Aldune.	Esse QR não é um código de perfil do Aldune.
scan_qr_denied	Aldune needs the camera only to read this QR. You can allow it in the system settings, or paste the code instead.	Aldune solo necesita la cámara para leer este QR. Puedes permitirla en los ajustes del sistema, o pegar el código.	Aldune braucht die Kamera nur, um diesen QR zu lesen. Du kannst sie in den Systemeinstellungen erlauben oder stattdessen den Code einfügen.	Aldune n'utilise la caméra que pour lire ce QR. Tu peux l'autoriser dans les réglages du système, ou coller le code.	O Aldune só precisa da câmera para ler este QR. Você pode permiti-la nas configurações do sistema ou colar o código.
scan_qr_back	Back	Volver	Zurück	Retour	Voltar
```

Comprobar: `python android/scripts/gen_strings.py --check`.

- [ ] **Step 3: Write the activity**

`QrScanActivity.kt`:

```kotlin
package app.aldune.ui

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Bundle
import android.view.WindowManager
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContract
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import app.aldune.R
import app.aldune.core.ProfileCodes
import app.aldune.data.SettingsStore
import app.aldune.syncclient.QrDecoder
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong

/** Pide el escáner y devuelve el código de perfil leído, o `null` si se cancela o no hay permiso. */
class ScanQrContract : ActivityResultContract<Unit, String?>() {
    override fun createIntent(context: Context, input: Unit): Intent = Intent(context, QrScanActivity::class.java)

    override fun parseResult(resultCode: Int, intent: Intent?): String? =
        if (resultCode == Activity.RESULT_OK) intent?.getStringExtra(QrScanActivity.EXTRA_CODE) else null
}

/**
 * La pantalla del escáner de «Añadir perfil». El QR lleva la clave del perfil, así que: `FLAG_SECURE` (ni capturas ni miniatura en Recientes),
 * ningún fotograma se guarda ni sale de aquí, y la cámara solo está abierta mientras esta pantalla está delante.
 */
class QrScanActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
        val settings = SettingsStore(this).load()
        setContent {
            AldunaTheme(settings.appearance, settings.aspectColors) {
                Surface(Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
                    ScanScreen(onCode = { code ->
                        setResult(RESULT_OK, Intent().putExtra(EXTRA_CODE, code))
                        finish()
                    }, onBack = { finish() })
                }
            }
        }
    }

    companion object {
        const val EXTRA_CODE = "profile_code"
    }
}

@Composable
private fun ScanScreen(onCode: (String) -> Unit, onBack: () -> Unit) {
    val context = LocalContext.current
    var granted by remember { mutableStateOf(ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) }
    var asked by remember { mutableStateOf(false) }
    val permission = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { ok -> granted = ok; asked = true }
    LaunchedEffect(Unit) { if (!granted) permission.launch(Manifest.permission.CAMERA) }
    when {
        granted -> CameraPreview(onCode, onBack)
        asked -> Column(Modifier.fillMaxSize().padding(24.dp), verticalArrangement = Arrangement.Center, horizontalAlignment = Alignment.CenterHorizontally) {
            Text(stringResource(R.string.scan_qr_denied), style = MaterialTheme.typography.bodyLarge)
            Button(onClick = onBack, Modifier.padding(top = 16.dp)) { Text(stringResource(R.string.scan_qr_back)) }
        }
        else -> Box(Modifier.fillMaxSize())
    }
}

@Composable
private fun CameraPreview(onCode: (String) -> Unit, onBack: () -> Unit) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val notAldune = stringResource(R.string.scan_qr_not_aldune)
    val done = remember { AtomicBoolean(false) }
    val lastWarning = remember { AtomicLong(0) }
    val executor = remember { Executors.newSingleThreadExecutor() }
    val main = remember { ContextCompat.getMainExecutor(context) }
    var provider by remember { mutableStateOf<ProcessCameraProvider?>(null) }
    // La cámara solo está abierta mientras esta pantalla está delante: al salir se desenlaza y se para el hilo de análisis.
    DisposableEffect(Unit) {
        onDispose {
            provider?.unbindAll()
            executor.shutdown()
        }
    }
    Box(Modifier.fillMaxSize()) {
        AndroidView(
            factory = { ctx ->
                val view = PreviewView(ctx)
                val future = ProcessCameraProvider.getInstance(ctx)
                future.addListener({
                    val p = future.get()
                    provider = p
                    val analysis = ImageAnalysis.Builder().setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST).build()
                    analysis.setAnalyzer(executor) { image ->
                        analyze(
                            image, done,
                            // El análisis va en su hilo: el resultado y el aviso se pasan al hilo principal.
                            onCode = { code -> main.execute { onCode(code) } },
                            onOther = {
                                // Un QR que no es de Aldune se ignora y se sigue buscando; el aviso se limita a uno cada 3 s.
                                val now = System.currentTimeMillis()
                                if (now - lastWarning.get() > 3_000) {
                                    lastWarning.set(now)
                                    main.execute { Toast.makeText(ctx, notAldune, Toast.LENGTH_SHORT).show() }
                                }
                            },
                        )
                    }
                    val preview = Preview.Builder().build().also { it.surfaceProvider = view.surfaceProvider }
                    p.unbindAll()
                    p.bindToLifecycle(lifecycleOwner, CameraSelector.DEFAULT_BACK_CAMERA, preview, analysis)
                }, main)
                view
            },
            modifier = Modifier.fillMaxSize(),
        )
        Text(
            stringResource(R.string.scan_qr_title), Modifier.align(Alignment.TopCenter).padding(24.dp),
            color = androidx.compose.ui.graphics.Color.White, style = MaterialTheme.typography.titleMedium,
        )
        Button(onClick = onBack, Modifier.align(Alignment.BottomCenter).padding(24.dp)) { Text(stringResource(R.string.scan_qr_back)) }
    }
}

private fun analyze(image: ImageProxy, done: AtomicBoolean, onCode: (String) -> Unit, onOther: (String) -> Unit) {
    try {
        if (done.get()) return
        val plane = image.planes[0]
        val buffer = plane.buffer
        val bytes = ByteArray(buffer.remaining()).also { buffer.get(it) }
        val text = QrDecoder.decodeText(bytes, plane.rowStride, image.width, image.height) ?: return
        val code = ProfileCodes.findShareCode(text)
        // `done` evita devolver dos veces si llegan dos fotogramas con el código antes de que se cierre la pantalla.
        if (code == null) onOther(text) else if (done.compareAndSet(false, true)) onCode(code)
    } finally {
        image.close()
    }
}
```

- [ ] **Step 4: Build**

Run: `./gradlew :app:assembleDebug` (desde `android/`) → compila sin errores ni avisos nuevos.

- [ ] **Step 5: Commit**

```bash
git add android/app/build.gradle.kts android/app/src/main/AndroidManifest.xml android/strings.tsv android/app/src/main/res/values/strings.xml android/app/src/main/res/values-de/strings.xml android/app/src/main/res/values-es/strings.xml android/app/src/main/res/values-fr/strings.xml android/app/src/main/res/values-pt-rBR/strings.xml android/app/src/main/kotlin/app/aldune/ui/QrScanActivity.kt
git commit -m "Android: QrScanActivity, escaner de QR con CameraX (permiso al pulsar, FLAG_SECURE, sin guardar fotogramas)"
```

---

### Task 5: Android, botón «Escanear QR» en «Añadir perfil»

**Files:**
- Modify: `android/app/src/main/kotlin/app/aldune/ui/SyncUi.kt` (`ConnectForm`, junto al botón «Pegar», ~línea 205-215)

**Interfaces:**
- Consumes: `ScanQrContract` (Task 4). El resultado va al mismo estado `code` que usa «Pegar».

- [ ] **Step 1: Wire the button**

En `ConnectForm`, junto a `val clipboard = LocalClipboard.current`:

```kotlin
    val scanner = rememberLauncherForActivityResult(ScanQrContract()) { scanned ->
        // Igual que «Pegar»: solo rellena el campo; validar y continuar sigue siendo de «Continuar».
        if (!scanned.isNullOrBlank()) { code = scanned; errorText = null }
    }
```

Y en la fila de botones (después del `OutlinedButton` de «Pegar», dentro de un `Row` con `Arrangement.spacedBy(8.dp)` que agrupe ambos):

```kotlin
            OutlinedButton(onClick = { scanner.launch(Unit) }) { Text(stringResource(R.string.scan_qr)) }
```

Imports: `androidx.activity.compose.rememberLauncherForActivityResult` y `androidx.compose.foundation.layout.Row` si no estaban (`ScanQrContract` está en el mismo paquete, `app.aldune.ui`).

- [ ] **Step 2: Build and run the full Android tests**

Run: `./gradlew :app:assembleDebug` y `./gradlew test` → verde.

- [ ] **Step 3: Verify on a device**

Con el PC mostrando el QR de Task 2 y **el Pixel** (cámara real; no capturar su pantalla de inicio ni notificaciones): instalar la debug (`adb install -r`; es otra app, `…aldune.debug`), Ajustes → Sincronización → «Escanear QR». Comprobar: (a) pide el permiso al pulsar; (b) lee el QR y el campo queda relleno con el código; (c) **denegar** el permiso muestra la explicación y «Volver»; (d) apuntar a un QR cualquiera (una web) muestra «Ese QR no es un código de perfil» y sigue escaneando; (e) en Recientes no se ve la cámara (FLAG_SECURE); (f) al volver atrás la cámara se libera.
Desinstalar la debug del Pixel al terminar.

- [ ] **Step 4: Commit**

```bash
git add android/app/src/main/kotlin/app/aldune/ui/SyncUi.kt
git commit -m "Android: boton Escanear QR junto a Pegar en Anadir perfil"
```

---

### Task 6: Documentación, versiones y revisión de seguridad

**Files:**
- Modify: `docs/STATUS.md`, `docs/SYNC.md` (paso a paso: unirse con QR), `README.md` si menciona cómo unir el móvil
- Modify: `src/Aldune/Aldune.csproj` (1.7.16) y `android/app/build.gradle.kts` (`versionCode` 12, `0.1.0-rc12`) — **solo si se va a publicar**; si no, dejarlo.

- [ ] **Step 1: Update the docs**

Añadir a `docs/STATUS.md` una entrada «QR de emparejamiento (fecha)»: qué hace, las dos dependencias nuevas (QRCoder en Core, CameraX + ZXing core en Android), la decisión de no dividir el QR y que el token no viaja. En `docs/SYNC.md`, en el paso a paso de Android, añadir «Ajustes → Sincronización → Escanear QR» como alternativa a pegar. Marcar la spec del QR como «Estado: implementada».

- [ ] **Step 2: Security review**

Lanzar el agente `seguridad` sobre el diff (`git diff main..` o la rama) con foco en: `ShareQrWindow` (que el código no quede en portapapeles, texto ni logs; cierre a los 120 s), `QrScanActivity` (ciclo de vida de la cámara, `FLAG_SECURE`, que ningún fotograma ni el código se escriba en logs ni se guarde), el manifiesto (`exported=false`, permiso solo `CAMERA`), y las dependencias nuevas (versiones fijadas, licencias MIT/Apache-2.0). Corregir lo que salga antes de publicar.

- [ ] **Step 3: Full test run and commit**

Run: `dotnet test Aldune.slnx -c Release --filter "FullyQualifiedName!~SmokeTests"` y, desde `android/`, `./gradlew test` y `python scripts/gen_strings.py --check` → todo verde.

```bash
git add docs/STATUS.md docs/SYNC.md docs/superpowers/specs/2026-10-27-qr-emparejamiento-design.md
git commit -m "Docs: QR de emparejamiento (STATUS, SYNC y estado de la spec)"
```

- [ ] **Step 4: Release (solo con confirmación del usuario)**

Publicar es hacia fuera: preguntar antes. Si se confirma, seguir `docs/RELEASING.md` (Windows: tag `v1.7.16` y servidor de sync; Android: `android-v0.1.0-rc12`, firma con `Proyectos\_herramientas\publicar-rc11.ps1` adaptado a rc12 y su huella de siempre `a10d33e76b3e3bc82333f44815824d54e6f99f6e7b2183cacae212c9dd348d37`).
