# Aspectos retro en Android: plan de implementación

> **Para quien lo ejecute:** usa superpowers:subagent-driven-development (recomendado) o superpowers:executing-plans. Los pasos son casillas (`- [ ]`).
> Repo de Aldune, comentarios en español explicando el porqué, commits solo como `NachoPola16 <nachopolac@gmail.com>`, sin `Co-Authored-By`, `git add` con ficheros
> explícitos. Los ficheros que lleven `\` se escriben con Write/Edit, no con heredoc de Bash (se come las barras). JDK 21: `export JAVA_HOME="C:/Program Files/Eclipse Adoptium/jdk-21.0.12.101-hotspot"`
> y `./gradlew` desde `android/`. Si Gradle da un error raro de compilación incremental (p. ej. «Unresolved reference» en un módulo que no has tocado), repite el comando antes de investigar.

**Objetivo:** elegir en Android los aspectos retro de Windows (XP claro, XP oscuro, Telecomunicaciones claro y oscuro, Bash, Fósforo) con sus colores personalizables y fuente monoespaciada donde toca, con paletas idénticas a las del PC.

**Arquitectura:** port literal de `ColorFit`, `ColorMix`, `AspectCatalog` y `AspectPalettes` al módulo `text` (Kotlin puro); `AppPalette.forMode` acepta los modos 5-10 y los colores elegidos; los vectores de `tests/vectors/text-color.json` (los genera Core) fijan la igualdad con Windows; los ajustes guardan `aspectColors` en `SharedPreferences` y Ajustes enseña los huecos.

**Tecnología:** Kotlin, Jetpack Compose (Material 3), JUnit 5 en `text`, xUnit en `Aldune.Core.Tests`.

**Spec:** `docs/superpowers/specs/2026-10-25-aldune-android-aspectos-retro-design.md`. Referencia en Windows: `src/Aldune.Core/{ColorFit,ColorMix,AspectCatalog,AspectPalettes,AppPalette}.cs`.

## Restricciones globales

- Nada de la «piel» de Windows (relieve, degradados, franjas, prefijos, prompt, esquinas rectas, señal de sync, color único): fuera de alcance.
- No se toca el formato de sync (4), los prefijos `aldune-profile-*` ni los vectores de sync. Solo se **añaden** casos a `text-color.json`.
- Los números de modo son los de `AppearanceMode` de Core: Oscuro 0, Claro 1, Sistema 2, Pastel 3, Medianoche 4, XP claro 5, XP oscuro 6, Telecom claro 7, Telecom oscuro 8, Bash 9, Fósforo 10.
- Ids de aspecto estables (no cambiar nunca): `xp-light`, `xp-dark`, `telecom-light`, `telecom-dark`, `bash`, `phosphor`.
- Un `appearance` fuera de rango o un JSON de `aspectColors` ilegible se ignoran (valor por defecto); una instalación existente carga sin migración.
- Textos de interfaz en los cinco idiomas con `android/strings.tsv` y `python android/scripts/gen_strings.py` (luego `--check`). Se reutilizan los textos de `src/Aldune/Resources/Strings.cs`.
- Los `.kt` nuevos sin BOM; los `.cs` existentes conservan su BOM.
- No se publica ninguna versión sin confirmación del usuario. La sonda en el Pixel usa la versión de depuración (`io.github.nachopola16.aldune.debug`), nunca la app real; avisar al usuario antes de tocar su pantalla.

## Review Focus

Lo que la spec implica y ningún test de tarea cubre por sí solo; cada línea tiene su test en la tarea indicada:

1. Un color elegido sin matiz (gris, blanco, negro) no puede teñir toda la paleta de un matiz inventado → Task 3 (`GreyChoices`).
2. Colores extremos (blanco, negro, amarillo puro) en todos los huecos siguen dejando leer el texto → Task 3.
3. Un hex inválido, vacío o con minúsculas en `aspectColors` cae en el valor de fábrica y no rompe la paleta → Task 2 y Task 5.
4. Un `appearance` guardado por una versión nueva y leído por una antigua (5-10) cae en Sistema, no en un fallo → Task 5.
5. `isLight` de los modos retro decide la barra de estado y el widget: XP claro y Telecom claro son claros, el resto oscuros → Task 3.

---

## Mapa de ficheros

Crear (módulo `text`, `android/text/src/main/kotlin/app/aldune/text/`):
- `ColorMix.kt`, `ColorFit.kt`: utilidades de color.
- `AspectCatalog.kt`: los 6 aspectos (huecos, muestras, paletas de Bash, tema sugerido) y `resolve`.
- `AspectPalettes.kt`: `derive(mode, colores)`.
- `AspectColorsJson.kt`: (de)serialización pura de `aspectColors` para `SharedPreferences`.

Tests en `android/text/src/test/kotlin/app/aldune/text/`: `ColorFitTest.kt`, `AspectCatalogTest.kt`, `AspectPalettesTest.kt`, `AspectColorsJsonTest.kt`; y se amplían `Operations.kt` (vectores).

Modificar:
- `AppPalette.kt`: constantes 5-10, `forMode(..., aspectColors)`, `isLight`.
- Core (C#): `tests/Aldune.Core.Tests/TextVectorsTests.cs` (casos y operaciones nuevas), `tests/vectors/text-color.json` y `tests/vectors/README.md`.
- App: `data/SettingsStore.kt`, `ui/AldunaTheme.kt`, `MainActivity.kt`, `widget/DockWidget.kt`, `ui/SettingsScreen.kt`, `ui/NotesViewModel.kt` (si hace falta), `strings.tsv`.
- Crear en la app: `ui/AspectSection.kt` (los huecos de color) y, si hace falta, `ui/Typography.kt`.

---

### Task 1: ColorMix y ColorFit

**Files:**
- Create: `android/text/src/main/kotlin/app/aldune/text/ColorMix.kt`, `ColorFit.kt`
- Test: `android/text/src/test/kotlin/app/aldune/text/ColorFitTest.kt`

**Interfaces:**
- Consumes: `OklchColor.fromHex(String?): OklchColor?`, `OklchColor(l, c, h).toHex()`, `OklchColor.parseHex(String?): Int?` (internal), `NoteColorContrast.luminance(String?): Double?`.
- Produces: `ColorMix.toward(from: String, to: String, amount: Double): String`; `ColorFit.ratio(a, b): Double`, `ColorFit.background(background, text, minimum): String`, `ColorFit.foreground(foreground, background, minimum): String`, `ColorFit.lighter(color, amount): String`, `ColorFit.darker(color, amount): String`.

- [ ] **Step 1: Escribir los tests que fallan**

`ColorFitTest.kt` (JUnit 5, como `CustomColorTest`):

```kotlin
package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class ColorFitTest {
    @Test fun `ratio de blanco y negro es 21 y un color invalido da 1`() {
        assertEquals(21.0, ColorFit.ratio("#FFFFFF", "#000000"), 0.0001)
        assertEquals(1.0, ColorFit.ratio("rojo", "#000000"), 0.0)
    }

    @Test fun `un color que ya se lee se devuelve en mayusculas sin tocarlo`() =
        assertEquals("#1E1A14", ColorFit.foreground("#1e1a14", "#FFFFFF", 4.5))

    @Test fun `un fondo demasiado claro para texto blanco se oscurece hasta el minimo`() {
        val fitted = ColorFit.background("#FFFF00", "#FFFFFF", 4.5)
        assertTrue(ColorFit.ratio(fitted, "#FFFFFF") >= 4.5)
    }

    @Test fun `un texto oscuro sobre fondo oscuro se aclara`() {
        val fitted = ColorFit.foreground("#101010", "#202020", 4.5)
        assertTrue(ColorFit.ratio(fitted, "#202020") >= 4.5)
    }

    @Test fun `lighter y darker mueven la claridad y se quedan en 0-1`() {
        assertEquals("#FFFFFF", ColorFit.lighter("#FFFFFF", 0.5))
        assertEquals("#000000", ColorFit.darker("#000000", 0.5))
        assertEquals("nope", ColorFit.lighter("nope", 0.1))
    }

    @Test fun `toward mezcla en sRGB y redondea hacia arriba la mitad`() {
        assertEquals("#000000", ColorMix.toward("#000000", "#FFFFFF", 0.0))
        assertEquals("#FFFFFF", ColorMix.toward("#000000", "#FFFFFF", 1.0))
        assertEquals("#808080", ColorMix.toward("#000000", "#FFFFFF", 0.5)) // 127.5 → 128
        assertEquals("#000000", ColorMix.toward("#000000", "#FFFFFF", -3.0))
        assertEquals("rojo", ColorMix.toward("rojo", "#FFFFFF", 0.5))
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `./gradlew :text:test -q` — Expected: error de compilación `Unresolved reference 'ColorFit'`.

- [ ] **Step 3: Implementar**

`ColorMix.kt`:

```kotlin
package app.aldune.text

/** Mezcla lineal en sRGB entre dos `#RRGGBB` (port de `ColorMix` de Core): la de las maquetas; en OKLCH no saldría igual. */
object ColorMix {
    /** `amount` 0 = [from], 1 = [to]. Con un color inválido devuelve [from] tal cual. */
    fun toward(from: String, to: String, amount: Double): String {
        val a = OklchColor.parseHex(from) ?: return from
        val b = OklchColor.parseHex(to) ?: return from
        val t = amount.coerceIn(0.0, 1.0)
        // Core redondea la mitad hacia arriba (MidpointRounding.AwayFromZero); los canales nunca son negativos.
        fun channel(x: Int, y: Int) = Math.floor(x + (y - x) * t + 0.5).toInt()
        return "#%02X%02X%02X".format(
            channel((a shr 16) and 0xFF, (b shr 16) and 0xFF),
            channel((a shr 8) and 0xFF, (b shr 8) and 0xFF),
            channel(a and 0xFF, b and 0xFF),
        )
    }
}
```

`ColorFit.kt`:

```kotlin
package app.aldune.text

import java.util.Locale

/**
 * Un color elegido por el usuario tiene que seguir dejando leer el texto que va encima o al lado (port de `ColorFit` de Core): se mueve su
 * claridad OKLCH lo justo, conservando matiz y croma, que es lo que el usuario está eligiendo.
 */
object ColorFit {
    /** Contraste WCAG entre dos `#RRGGBB`; 1 si alguno no es válido. */
    fun ratio(a: String, b: String): Double {
        val la = NoteColorContrast.luminance(a) ?: return 1.0
        val lb = NoteColorContrast.luminance(b) ?: return 1.0
        return (maxOf(la, lb) + 0.05) / (minOf(la, lb) + 0.05)
    }

    /** Mueve el fondo hasta que [text] se lea con al menos [minimum]. */
    fun background(background: String, text: String, minimum: Double): String = adjust(background, text, minimum)

    /** Mueve el texto hasta que se lea sobre [background] con al menos [minimum]. */
    fun foreground(foreground: String, background: String, minimum: Double): String = adjust(foreground, background, minimum)

    fun lighter(color: String, amount: Double): String = shift(color, amount)

    fun darker(color: String, amount: Double): String = shift(color, -amount)

    private fun shift(color: String, amount: Double): String =
        OklchColor.fromHex(color)?.let { it.copy(l = (it.l + amount).coerceIn(0.0, 1.0)).toHex() } ?: color

    private fun adjust(movable: String, fixedColor: String, minimum: Double): String {
        val color = OklchColor.fromHex(movable) ?: return movable
        val fixedLuminance = NoteColorContrast.luminance(fixedColor) ?: return movable
        if (ratio(movable, fixedColor) >= minimum) return movable.uppercase(Locale.ROOT)

        // Contra un color claro se oscurece; contra uno oscuro se aclara. 0.18 es la luminancia en la que el negro y el blanco dan el mismo contraste.
        val darken = fixedLuminance > 0.18
        var low = if (darken) 0.0 else color.l
        var high = if (darken) color.l else 1.0
        var best = OklchColor(if (darken) 0.0 else 1.0, color.c, color.h).toHex()
        // Búsqueda binaria de la claridad más cercana a la elegida que todavía llega al mínimo.
        repeat(30) {
            val middle = (low + high) / 2
            val candidate = color.copy(l = middle).toHex()
            val readable = ratio(candidate, fixedColor) >= minimum
            if (darken) {
                if (readable) { best = candidate; low = middle } else high = middle
            } else {
                if (readable) { best = candidate; high = middle } else low = middle
            }
        }
        return best
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `./gradlew :text:test -q` — Expected: sin errores; `text/build/test-results/test/TEST-app.aldune.text.ColorFitTest.xml` con `failures="0"`.

- [ ] **Step 5: Commit**

```
git add android/text/src/main/kotlin/app/aldune/text/ColorMix.kt android/text/src/main/kotlin/app/aldune/text/ColorFit.kt android/text/src/test/kotlin/app/aldune/text/ColorFitTest.kt
git commit -m "Android: ColorMix y ColorFit, port de Core"
```

---

### Task 2: AspectCatalog

**Files:**
- Create: `android/text/src/main/kotlin/app/aldune/text/AspectCatalog.kt`
- Test: `android/text/src/test/kotlin/app/aldune/text/AspectCatalogTest.kt`

**Interfaces:**
- Consumes: `NoteDisplayColor.isActive(String?): Boolean`, `NoteThemes.XP_ID`, `NoteThemes.SERENE_ID`.
- Produces:
  - `data class AspectColorSlot(val id: String, val default: String, val suggestions: List<String>)`
  - `data class AspectPreset(val id: String, val name: String, val colors: Map<String, String>)`
  - `data class AspectDefinition(val mode: Int, val id: String, val slots: List<AspectColorSlot>, val presets: List<AspectPreset>, val suggestedThemeId: String?)`
  - `object AspectCatalog { val retro: List<AspectDefinition>; fun forMode(mode: Int): AspectDefinition?; fun resolve(mode: Int, chosen: Map<String, String>?): Map<String, String> }`
  - Constantes de modo en `AppPalette` (se añaden en esta tarea, ver Step 3): `XP_LIGHT = 5`, `XP_DARK = 6`, `TELECOM_LIGHT = 7`, `TELECOM_DARK = 8`, `BASH = 9`, `PHOSPHOR = 10`.

- [ ] **Step 1: Escribir los tests que fallan** (espejo de `AspectCatalogTests.cs`)

```kotlin
package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class AspectCatalogTest {
    @Test fun `los retro son los seis de la spec con los numeros 5 a 10`() {
        assertEquals(listOf(5, 6, 7, 8, 9, 10), AspectCatalog.retro.map { it.mode })
        assertEquals(
            listOf("xp-light", "xp-dark", "telecom-light", "telecom-dark", "bash", "phosphor"),
            AspectCatalog.retro.map { it.id },
        )
    }

    @Test fun `los aspectos de siempre no tienen entrada en el catalogo`() {
        for (mode in 0..4) assertNull(AspectCatalog.forMode(mode))
    }

    @Test fun `cada hueco tiene un valor por defecto y muestras validos`() {
        for (aspect in AspectCatalog.retro) for (slot in aspect.slots) {
            assertTrue(NoteDisplayColor.isActive(slot.default), "${aspect.id}.${slot.id}")
            for (s in slot.suggestions) assertTrue(NoteDisplayColor.isActive(s), "${aspect.id}.${slot.id} $s")
        }
    }

    @Test fun `las paletas de bash rellenan todos los huecos`() {
        val bash = AspectCatalog.forMode(AppPalette.BASH)!!
        assertEquals(listOf("gruvbox", "ubuntu", "tango"), bash.presets.map { it.id })
        for (preset in bash.presets) assertEquals(bash.slots.map { it.id }.sorted(), preset.colors.keys.sorted())
    }

    @Test fun `resolve sin nada elegido da los valores de fabrica`() {
        val colors = AspectCatalog.resolve(AppPalette.XP_LIGHT, null)
        assertEquals("#0055E5", colors["titleBar"])
        assertEquals("#3C9A3C", colors["accent"])
    }

    @Test fun `resolve conserva lo valido, pone en mayusculas e ignora el resto`() {
        val colors = AspectCatalog.resolve(
            AppPalette.XP_LIGHT, mapOf("titleBar" to "#5a7a2e", "accent" to "rojo", "unknown" to "#123456"),
        )
        assertEquals("#5A7A2E", colors["titleBar"])
        assertEquals("#3C9A3C", colors["accent"])
        assertFalse(colors.containsKey("unknown"))
    }

    @Test fun `resolve de un aspecto de siempre da un mapa vacio`() = assertEquals(emptyMap<String, String>(), AspectCatalog.resolve(AppPalette.DARK, mapOf("accent" to "#FF0000")))

    @Test fun `los temas sugeridos siguen la spec`() {
        assertEquals(NoteThemes.XP_ID, AspectCatalog.forMode(AppPalette.XP_LIGHT)!!.suggestedThemeId)
        assertNull(AspectCatalog.forMode(AppPalette.TELECOM_LIGHT)!!.suggestedThemeId)
        for (mode in listOf(AppPalette.XP_DARK, AppPalette.TELECOM_DARK, AppPalette.BASH, AppPalette.PHOSPHOR))
            assertEquals(NoteThemes.SERENE_ID, AspectCatalog.forMode(mode)!!.suggestedThemeId)
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `./gradlew :text:test -q` — Expected: `Unresolved reference 'AspectCatalog'` / `'XP_LIGHT'`.

- [ ] **Step 3: Implementar**

En `AppPalette.kt`, junto a `MIDNIGHT`, añadir:

```kotlin
    /** Los aspectos retro de Windows (XP, Telecomunicaciones, Bash, Fósforo): su paleta se deriva en `AspectPalettes`. */
    const val XP_LIGHT = 5
    const val XP_DARK = 6
    const val TELECOM_LIGHT = 7
    const val TELECOM_DARK = 8
    const val BASH = 9
    const val PHOSPHOR = 10
```

`AspectCatalog.kt` (valores copiados literales de `AspectCatalog.cs`):

```kotlin
package app.aldune.text

import java.util.Locale

/** Un color que el usuario puede cambiar en un aspecto, con muestras de un toque. */
data class AspectColorSlot(val id: String, val default: String, val suggestions: List<String>)

/** Varios huecos de una vez (las paletas de terminal de Bash). */
data class AspectPreset(val id: String, val name: String, val colors: Map<String, String>)

/** `id` es la clave estable de `aspectColors`: no cambiar nunca. `suggestedThemeId` es el tema de notas que el aspecto propone, o `null`. */
data class AspectDefinition(
    val mode: Int, val id: String, val slots: List<AspectColorSlot>, val presets: List<AspectPreset>, val suggestedThemeId: String?,
)

/** Los aspectos retro y sus colores personalizables (port de `AspectCatalog` de Core). */
object AspectCatalog {
    val retro: List<AspectDefinition> = listOf(
        AspectDefinition(
            AppPalette.XP_LIGHT, "xp-light",
            listOf(
                AspectColorSlot("titleBar", "#0055E5", listOf("#0055E5", "#5A7A2E", "#8C8CA8", "#A8452A")), // Luna azul, oliva, plata, teja
                AspectColorSlot("accent", "#3C9A3C", listOf("#3C9A3C", "#0055E5", "#C46A1C")),
            ),
            emptyList(), NoteThemes.XP_ID,
        ),
        AspectDefinition(
            AppPalette.XP_DARK, "xp-dark",
            listOf(
                AspectColorSlot("titleBar", "#1C4FA8", listOf("#1C4FA8", "#008080", "#6B2E8A", "#8A2E2E")),
                AspectColorSlot("accent", "#4C86E8", listOf("#4C86E8", "#2EA8A8", "#C9A227")),
            ),
            emptyList(), NoteThemes.SERENE_ID,
        ),
        AspectDefinition(
            AppPalette.TELECOM_LIGHT, "telecom-light",
            listOf(
                AspectColorSlot("ink", "#1C2A22", listOf("#1C2A22", "#1F2A44", "#3A2418")),
                AspectColorSlot("accent", "#2F7A3C", listOf("#2F7A3C", "#1F57B5", "#9C1F6E")),
            ),
            emptyList(), null,
        ),
        AspectDefinition(
            AppPalette.TELECOM_DARK, "telecom-dark",
            listOf(
                AspectColorSlot("panel", "#171C21", listOf("#171C21", "#10161C", "#1B1B1B")),
                AspectColorSlot("trace", "#F2D338", listOf("#F2D338", "#3FD0E0", "#7CFC7C")),
            ),
            emptyList(), NoteThemes.SERENE_ID,
        ),
        AspectDefinition(
            AppPalette.BASH, "bash",
            listOf(
                AspectColorSlot("background", "#282828", listOf("#282828", "#300A24", "#1E1E1E")),
                AspectColorSlot("user", "#B8BB26", listOf("#B8BB26", "#8AE234", "#FABD2F", "#8EC07C")),
                AspectColorSlot("path", "#83A598", listOf("#83A598", "#729FCF", "#D3869B", "#FE8019")),
                AspectColorSlot("accent", "#D79921", listOf("#D79921", "#458588", "#98971A", "#B16286")),
            ),
            listOf(
                // Gruvbox por defecto: cálido y apagado, nada de colores fuertes (decidido con el usuario en Windows).
                AspectPreset("gruvbox", "Gruvbox", mapOf("background" to "#282828", "user" to "#B8BB26", "path" to "#83A598", "accent" to "#D79921")),
                AspectPreset("ubuntu", "Ubuntu", mapOf("background" to "#300A24", "user" to "#8AE234", "path" to "#729FCF", "accent" to "#E95420")),
                AspectPreset("tango", "Tango", mapOf("background" to "#1E1E1E", "user" to "#8AE234", "path" to "#729FCF", "accent" to "#3465A4")),
            ),
            NoteThemes.SERENE_ID,
        ),
        AspectDefinition(
            AppPalette.PHOSPHOR, "phosphor",
            listOf(
                AspectColorSlot("phosphor", "#A8E6B4", listOf("#A8E6B4", "#FFB547", "#8EC9FF", "#E8E8E8")), // verde, ámbar, azul, blanco
                AspectColorSlot("details", "#D9A441", listOf("#D9A441", "#A8E6B4", "#E8E8E8")),
            ),
            emptyList(), NoteThemes.SERENE_ID,
        ),
    )

    fun forMode(mode: Int): AspectDefinition? = retro.firstOrNull { it.mode == mode }

    /**
     * Colores efectivos de un aspecto: los elegidos que sean `#RRGGBB` válidos (en mayúsculas) y, para el resto, los de fábrica. Vacío para los
     * aspectos que no tienen huecos.
     */
    fun resolve(mode: Int, chosen: Map<String, String>?): Map<String, String> {
        val aspect = forMode(mode) ?: return emptyMap()
        val result = linkedMapOf<String, String>()
        for (slot in aspect.slots) {
            val value = chosen?.get(slot.id)
            result[slot.id] = if (value != null && NoteDisplayColor.isActive(value)) value.uppercase(Locale.ROOT) else slot.default
        }
        return result
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `./gradlew :text:test -q` — Expected: `AspectCatalogTest` con `failures="0"`.

- [ ] **Step 5: Commit**

```
git add android/text/src/main/kotlin/app/aldune/text/AspectCatalog.kt android/text/src/main/kotlin/app/aldune/text/AppPalette.kt android/text/src/test/kotlin/app/aldune/text/AspectCatalogTest.kt
git commit -m "Android: catalogo de aspectos retro, port de Core"
```

---

### Task 3: AspectPalettes y AppPalette.forMode

**Files:**
- Create: `android/text/src/main/kotlin/app/aldune/text/AspectPalettes.kt`
- Modify: `android/text/src/main/kotlin/app/aldune/text/AppPalette.kt` (`forMode`, `isLight`, comentario de cabecera)
- Test: `android/text/src/test/kotlin/app/aldune/text/AspectPalettesTest.kt`

**Interfaces:**
- Consumes: `AppPalette.tint(source, neutralHue, neutralChroma, accentHue)` (internal), `AppPalette.light`, `AppPalette.dark`, `AspectCatalog.resolve`, `ColorFit`, `ColorMix`, `OklchColor`.
- Produces: `AspectPalettes.derive(mode: Int, colors: Map<String, String>): Map<String, String>`; `AppPalette.forMode(mode: Int, systemUsesLight: Boolean, aspectColors: Map<String, String>? = null): Map<String, String>`.

- [ ] **Step 1: Escribir los tests que fallan** (espejo de `AppPaletteTests.cs` para los retro)

```kotlin
package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class AspectPalettesTest {
    private val extremes = listOf("#FFFFFF", "#000000", "#FFFF00")

    /** Cada retro con sus colores de fábrica y con un color extremo en todos los huecos. */
    private fun palettes(): List<Pair<String, Map<String, String>>> = buildList {
        for (aspect in AspectCatalog.retro) {
            add("${aspect.id}" to AppPalette.forMode(aspect.mode, false))
            for (extreme in extremes) {
                val colors = aspect.slots.associate { it.id to extreme }
                add("${aspect.id}/$extreme" to AppPalette.forMode(aspect.mode, false, colors))
            }
        }
    }

    private fun contrast(p: Map<String, String>, fg: String, bg: String, minimum: Double, label: String) {
        val ratio = ColorFit.ratio(p.getValue(fg), p.getValue(bg))
        assertTrue(ratio >= minimum, "$label: $fg sobre $bg: %.2f < %.1f".format(ratio, minimum))
    }

    @Test fun `cada paleta define exactamente las mismas claves que Oscuro`() {
        for ((label, p) in palettes()) assertEquals(AppPalette.dark.keys.sorted(), p.keys.sorted(), label)
    }

    @Test fun `cada clave es un color hexadecimal valido`() {
        val hex = Regex("^#([0-9A-F]{6}|[0-9A-F]{8})$")
        for ((label, p) in palettes()) for ((key, value) in p) assertTrue(hex.matches(value.uppercase()), "$label $key $value")
    }

    @Test fun `el texto se lee sobre todos los fondos del chrome`() {
        for ((label, p) in palettes()) {
            for (text in listOf("Text", "TextStrong", "TextSoft", "TextWarm")) for (ground in listOf("Ground", "Surface", "Raised", "Popup"))
                contrast(p, text, ground, 4.5, label)
            contrast(p, "MutedText", "Ground", 4.5, label)
            contrast(p, "MutedText", "Surface", 4.5, label)
            contrast(p, "Hint", "Ground", 3.5, label)
            contrast(p, "DangerText", "Ground", 4.5, label)
            contrast(p, "ErrorText", "Ground", 4.5, label)
        }
    }

    @Test fun `los botones se leen`() {
        for ((label, p) in palettes()) {
            contrast(p, "OnAccent", "Accent", 4.5, label)
            contrast(p, "OnDanger", "DangerStrong", 4.5, label)
            contrast(p, "Text", "DangerBg", 4.5, label)
            contrast(p, "Text", "Hover", 4.5, label)
            contrast(p, "TextStrong", "DockPrimary", 4.5, label)
        }
    }

    @Test fun `la barra de titulo y el prompt se leen`() {
        for ((label, p) in palettes()) {
            contrast(p, "OnTitleBar", "TitleBar", 4.5, label)
            contrast(p, "OnTitleBar", "TitleBarEnd", 4.5, label)
            contrast(p, "PromptUser", "Ground", 4.5, label)
            contrast(p, "PromptPath", "Ground", 4.5, label)
        }
    }

    @Test fun `el color elegido se nota con su matiz`() {
        val xp = AppPalette.forMode(AppPalette.XP_LIGHT, false, mapOf("titleBar" to "#A8452A"))
        val chosen = OklchColor.fromHex("#A8452A")!!
        val bar = OklchColor.fromHex(xp.getValue("TitleBarEnd"))!!
        assertTrue(Math.abs(chosen.h - bar.h) < 15, "${chosen.h} -> ${bar.h}")

        val bash = AppPalette.forMode(AppPalette.BASH, false, mapOf("user" to "#8AE234"))
        val user = OklchColor.fromHex(bash.getValue("PromptUser"))!!
        assertTrue(Math.abs(OklchColor.fromHex("#8AE234")!!.h - user.h) < 15)
    }

    // Un fósforo blanco o un fondo gris no tienen matiz: la paleta no puede salir de un color inventado (Review Focus 1).
    @Test fun `una eleccion gris da una paleta gris`() {
        for ((mode, slot, grey) in listOf(Triple(AppPalette.PHOSPHOR, "phosphor", "#E8E8E8"), Triple(AppPalette.BASH, "background", "#1E1E1E"))) {
            val p = AppPalette.forMode(mode, false, mapOf(slot to grey))
            for (key in listOf("Ground", "Surface", "Raised", "Text")) assertTrue(OklchColor.fromHex(p.getValue(key))!!.c < 0.02, "$key ${p[key]}")
        }
    }

    @Test fun `el fondo elegido es el Ground`() {
        assertEquals("#282828", AppPalette.forMode(AppPalette.BASH, false).getValue("Ground"))
        assertEquals("#171C21", AppPalette.forMode(AppPalette.TELECOM_DARK, false).getValue("Ground"))
    }

    @Test fun `el boton mas del dock sigue al acento`() {
        for ((mode, slot) in listOf(
            AppPalette.XP_LIGHT to "accent", AppPalette.XP_DARK to "accent", AppPalette.TELECOM_LIGHT to "accent",
            AppPalette.TELECOM_DARK to "trace", AppPalette.BASH to "accent", AppPalette.PHOSPHOR to "details",
        )) {
            val p = AppPalette.forMode(mode, false, mapOf(slot to "#D03020"))
            val dock = OklchColor.fromHex(p.getValue("DockPrimary"))!!
            assertTrue(dock.c > 0.05 && Math.abs(OklchColor.fromHex("#D03020")!!.h - dock.h) < 15, "mode $mode: h ${dock.h} c ${dock.c}")
        }
    }

    @Test fun `los aspectos de siempre ignoran los colores de aspecto`() {
        val colors = mapOf("accent" to "#FF0000")
        for (mode in listOf(0, 1, 3, 4)) assertEquals(AppPalette.forMode(mode, false), AppPalette.forMode(mode, false, colors))
    }

    // Review Focus 5: la claridad de cada modo decide la barra de estado y el widget.
    @Test fun `isLight de los modos retro`() {
        assertTrue(AppPalette.isLight(AppPalette.XP_LIGHT, false))
        assertTrue(AppPalette.isLight(AppPalette.TELECOM_LIGHT, false))
        for (mode in listOf(AppPalette.XP_DARK, AppPalette.TELECOM_DARK, AppPalette.BASH, AppPalette.PHOSPHOR))
            assertEquals(false, AppPalette.isLight(mode, true), "mode $mode")
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `./gradlew :text:test -q` — Expected: los tests de `AspectPalettesTest` fallan (hoy `forMode` cae en Claro/Oscuro y no admite el tercer parámetro: error de compilación `Too many arguments`).

- [ ] **Step 3: Implementar**

`AspectPalettes.kt` (port literal de `AspectPalettes.cs`; el orden de las operaciones importa porque los vectores comparan hex exactos):

```kotlin
package app.aldune.text

/**
 * Paletas de los aspectos retro (port de `AspectPalettes` de Core). Se derivan de Oscuro o Claro como Pastel y Medianoche (se conserva la
 * claridad de cada clave y se cambia el matiz, `AppPalette.tint`), así que heredan su contraste ya comprobado; encima van los colores del
 * usuario, ajustados con [ColorFit] cuando no dejarían leer el texto.
 */
object AspectPalettes {
    // Fondos que se mueven juntos cuando el usuario elige el fondo (panel de osciloscopio, terminal).
    private val backgrounds = listOf(
        "Ground", "GroundDeep", "Surface", "Popup", "Raised", "RaisedHover", "Hover", "Selected", "Pressed",
        "Track", "Divider", "TitleBar", "TitleBarEnd", "DockPrimary",
    )

    // El texto principal se lee sobre estos fondos. Se ajusta uno tras otro: cada ajuste solo aleja el color del fondo.
    private val textBackgrounds = listOf("Ground", "Surface", "Raised", "Popup", "Hover", "DangerBg")

    private const val WHITE = "#FFFFFF"

    fun derive(mode: Int, c: Map<String, String>): Map<String, String> = when (mode) {
        AppPalette.XP_LIGHT -> xpLight(c.getValue("titleBar"), c.getValue("accent"))
        AppPalette.XP_DARK -> xpDark(c.getValue("titleBar"), c.getValue("accent"))
        AppPalette.TELECOM_LIGHT -> telecomLight(c.getValue("ink"), c.getValue("accent"))
        AppPalette.TELECOM_DARK -> telecomDark(c.getValue("panel"), c.getValue("trace"))
        AppPalette.BASH -> bash(c.getValue("background"), c.getValue("user"), c.getValue("path"), c.getValue("accent"))
        AppPalette.PHOSPHOR -> phosphor(c.getValue("phosphor"), c.getValue("details"))
        else -> throw IllegalArgumentException("Sin aspecto retro: $mode")
    }

    private fun xpLight(bar: String, accent: String): Map<String, String> {
        // El beige de XP (#ECE9D8): el claro de siempre teñido hacia el amarillo grisáceo.
        val p = LinkedHashMap(AppPalette.tint(AppPalette.light, neutralHue = 95.0, neutralChroma = 0.018, accentHue = hue(accent)))
        setAccent(p, accent)
        // Degradado vertical de Luna: arriba más claro, abajo el color elegido; texto blanco sobre los dos.
        p["TitleBar"] = ColorFit.background(ColorFit.lighter(bar, 0.08), WHITE, 4.5)
        p["TitleBarEnd"] = ColorFit.background(bar, WHITE, 4.5)
        p["OnTitleBar"] = WHITE
        p["BevelLight"] = WHITE
        p["BevelDark"] = "#ACA899"
        return p
    }

    private fun xpDark(bar: String, accent: String): Map<String, String> {
        // Grafito neutro, sin el marrón/naranja de Royale Noir (decidido con el usuario en Windows).
        val p = LinkedHashMap(AppPalette.tint(AppPalette.dark, neutralHue = 255.0, neutralChroma = 0.012, accentHue = hue(accent)))
        setAccent(p, accent)
        // Degradado horizontal de Windows 98: oscuro a la izquierda, el color elegido más claro a la derecha.
        p["TitleBar"] = ColorFit.background(ColorFit.darker(bar, 0.12), WHITE, 4.5)
        p["TitleBarEnd"] = ColorFit.background(ColorFit.lighter(bar, 0.08), WHITE, 4.5)
        p["OnTitleBar"] = WHITE
        p["BevelLight"] = "#6A6E76"
        p["BevelDark"] = "#121316"
        return p
    }

    private fun telecomLight(ink: String, accent: String): Map<String, String> {
        // Papel de laboratorio: el claro de siempre con un gris verdoso apenas teñido; la tinta elegida es el texto.
        val p = LinkedHashMap(AppPalette.tint(AppPalette.light, neutralHue = neutralHue(ink, 100.0), neutralChroma = 0.012, accentHue = hue(accent)))
        val text = fitText(ink, p)
        p["Text"] = text
        p["TextStrong"] = text
        p["OnTitleBar"] = text
        setAccent(p, accent)
        return p
    }

    private fun telecomDark(panel: String, trace: String): Map<String, String> {
        val p = LinkedHashMap(
            AppPalette.tint(AppPalette.dark, neutralHue = neutralHue(panel, 240.0), neutralChroma = neutralChroma(panel, 0.02), accentHue = hue(trace)),
        )
        moveBackgrounds(p, panel)
        // La traza es CH1 y el acento: un amarillo de osciloscopio que se lee sobre el panel.
        p["Channel1"] = ColorFit.foreground(trace, p.getValue("Raised"), 4.5)
        setAccent(p, trace)
        return p
    }

    private fun bash(background: String, user: String, path: String, accent: String): Map<String, String> {
        val p = LinkedHashMap(
            AppPalette.tint(AppPalette.dark, neutralHue = neutralHue(background, 60.0), neutralChroma = neutralChroma(background, 0.03), accentHue = hue(accent)),
        )
        moveBackgrounds(p, background)
        p["PromptUser"] = ColorFit.foreground(user, p.getValue("Ground"), 4.5)
        p["PromptPath"] = ColorFit.foreground(path, p.getValue("Ground"), 4.5)
        // Como en la maqueta: botón del color del acento con el texto del color de la terminal.
        p["OnAccent"] = p.getValue("Ground")
        setAccent(p, accent)
        return p
    }

    private fun phosphor(phosphor: String, details: String): Map<String, String> {
        val p = LinkedHashMap(
            AppPalette.tint(AppPalette.dark, neutralHue = neutralHue(phosphor, 150.0), neutralChroma = neutralChroma(phosphor, 0.025), accentHue = hue(details)),
        )
        // Fondos casi negros del mismo matiz que el fósforo, como el cristal de un monitor apagado.
        moveBackgrounds(p, ColorMix.toward(phosphor, "#000000", 0.9))
        val text = fitText(phosphor, p)
        p["Text"] = text
        p["TextStrong"] = text
        p["OnTitleBar"] = text
        val soft = ColorFit.foreground(ColorMix.toward(text, p.getValue("Ground"), 0.15), p.getValue("Raised"), 4.5)
        p["TextSoft"] = soft
        p["TextWarm"] = soft
        p["OnAccent"] = p.getValue("Ground")
        setAccent(p, details)
        return p
    }

    // El acento y sus acompañantes, con el texto del botón (OnAccent) legible encima. También el botón «+» del dock, cuyo glifo es TextStrong:
    // se llama después de fijar TextStrong.
    private fun setAccent(p: MutableMap<String, String>, accent: String) {
        val onAccent = p.getValue("OnAccent")
        val fitted = ColorFit.background(accent, onAccent, 4.5)
        val darkText = ColorFit.ratio(onAccent, "#000000") < ColorFit.ratio(onAccent, WHITE)
        p["DockPrimary"] = ColorFit.background(accent, p.getValue("TextStrong"), 4.5)
        p["Accent"] = fitted
        p["AccentBorder"] = fitted
        p["FieldFocus"] = fitted
        // Al pasar el dedo, un paso más lejos del texto: más claro bajo texto oscuro, más oscuro bajo claro.
        p["AccentHover"] = ColorFit.background(if (darkText) ColorFit.lighter(fitted, 0.05) else ColorFit.darker(fitted, 0.05), onAccent, 4.5)
    }

    /**
     * Pone el fondo elegido como Ground y mueve todos los fondos lo mismo en claridad, para que sigan escalonados. La claridad se limita a
     * [0.14, 0.30]: más claro, los textos del oscuro dejarían de leerse; más oscuro, los escalones se pierden contra el negro.
     */
    private fun moveBackgrounds(p: MutableMap<String, String>, ground: String) {
        val chosen = OklchColor.fromHex(ground) ?: return
        val current = OklchColor.fromHex(p.getValue("Ground")) ?: return
        val target = chosen.l.coerceIn(0.14, 0.30)
        val delta = target - current.l
        for (key in backgrounds) {
            val value = p[key] ?: continue
            if (value.length != 7) continue
            val color = OklchColor.fromHex(value) ?: continue
            p[key] = color.copy(l = (color.l + delta).coerceIn(0.0, 1.0)).toHex()
        }
        p["Ground"] = chosen.copy(l = target).toHex()
        p["TitleBar"] = p.getValue("Ground")
        p["TitleBarEnd"] = p.getValue("Ground")
        p["MutedText"] = ColorFit.foreground(ColorFit.foreground(p.getValue("MutedText"), p.getValue("Ground"), 4.5), p.getValue("Surface"), 4.5)
        p["Hint"] = ColorFit.foreground(p.getValue("Hint"), p.getValue("Ground"), 3.5)
    }

    private fun fitText(text: String, p: Map<String, String>): String {
        var result = text
        for (key in textBackgrounds) result = ColorFit.foreground(result, p.getValue(key), 4.5)
        return result
    }

    private fun hue(color: String): Double = OklchColor.fromHex(color)?.h ?: 0.0

    // Un color sin matiz (gris, blanco) no puede teñir la paleta de un matiz inventado: su «matiz» es ruido.
    private fun neutralChroma(color: String, maximum: Double): Double =
        OklchColor.fromHex(color)?.takeIf { it.c >= 0.02 }?.let { minOf(it.c, maximum) } ?: 0.0

    private fun neutralHue(color: String, fallback: Double): Double =
        OklchColor.fromHex(color)?.takeIf { it.c >= 0.02 }?.h ?: fallback
}
```

En `AppPalette.kt` sustituir `forMode` e `isLight` y la frase de la cabecera «Los aspectos retro de Windows … no se llevan a Android»:

```kotlin
    /**
     * La paleta de [mode]. Los modos retro (5-10) se derivan en [AspectPalettes] con los colores que haya elegido el usuario ([aspectColors]:
     * id de hueco → hex; lo que falta o no es válido cae en los de fábrica). Un modo desconocido cae en Claro u Oscuro según [isLight], para
     * que un valor guardado por una versión más nueva nunca deje la app sin paleta.
     */
    fun forMode(mode: Int, systemUsesLight: Boolean, aspectColors: Map<String, String>? = null): Map<String, String> = when (mode) {
        PASTEL -> pastel
        MIDNIGHT -> midnight
        in XP_LIGHT..PHOSPHOR -> AspectPalettes.derive(mode, AspectCatalog.resolve(mode, aspectColors))
        else -> if (isLight(mode, systemUsesLight)) light else dark
    }

    fun isLight(mode: Int, systemUsesLight: Boolean): Boolean = when (mode) {
        LIGHT, PASTEL, XP_LIGHT, TELECOM_LIGHT -> true
        SYSTEM -> systemUsesLight
        else -> false
    }
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `./gradlew :text:test -q` — Expected: `AspectPalettesTest` con `failures="0"`. Si un contraste falla por 0.01, **no aflojes el test**: compara el orden de operaciones con `AspectPalettes.cs` (los vectores de la Task 4 lo detectarán de todos modos).

- [ ] **Step 5: Commit**

```
git add android/text/src/main/kotlin/app/aldune/text/AspectPalettes.kt android/text/src/main/kotlin/app/aldune/text/AppPalette.kt android/text/src/test/kotlin/app/aldune/text/AspectPalettesTest.kt
git commit -m "Android: paletas de los aspectos retro y forMode con colores elegidos"
```

---

### Task 4: Vectores de Core y verificación en Kotlin

**Files:**
- Modify: `tests/Aldune.Core.Tests/TextVectorsTests.cs` (operaciones y casos), `tests/vectors/text-color.json` (regenerado), `tests/vectors/README.md`
- Modify: `android/text/src/test/kotlin/app/aldune/text/Operations.kt`

**Interfaces:**
- Consumes: `Case(op, args…)`, `Execute(op, a)`, helpers `S`, `NS`, `I`, `D`, `Array(...)` de `TextVectorsTests.cs`; `AspectCatalog.Retro`, `AspectCatalog.Resolve`, `ColorFit.*`, `ColorMix.Toward`, `AppPalette.For(mode, light, aspectColors)` de Core.
- Produces: operaciones de vector `ColorFit.Ratio`, `ColorFit.Background`, `ColorFit.Foreground`, `ColorFit.Lighter`, `ColorFit.Darker`, `ColorMix.Toward`, `AspectCatalog.Retro`, `AspectCatalog.Resolve`, y `AppPalette.For` con argumento opcional `aspectColors` (objeto id → hex).

- [ ] **Step 1: Operaciones y casos nuevos en C#**

En `Execute` de `TextVectorsTests.cs`, **junto a las líneas de `AppPalette.For`**, sustituir la de `AppPalette.For` y añadir las nuevas:

```csharp
        "ColorFit.Ratio" => ColorFit.Ratio(S(a, "a"), S(a, "b")),
        "ColorFit.Background" => ColorFit.Background(S(a, "background"), S(a, "text"), D(a, "minimum")),
        "ColorFit.Foreground" => ColorFit.Foreground(S(a, "foreground"), S(a, "background"), D(a, "minimum")),
        "ColorFit.Lighter" => ColorFit.Lighter(S(a, "color"), D(a, "amount")),
        "ColorFit.Darker" => ColorFit.Darker(S(a, "color"), D(a, "amount")),
        "ColorMix.Toward" => ColorMix.Toward(S(a, "from"), S(a, "to"), D(a, "amount")),

        "AppPalette.For" => new JsonObject(AppPalette.For((AppearanceMode)I(a, "mode"), a["windowsUsesLight"]!.GetValue<bool>(), AspectColors(a))
            .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value))),
        "AspectCatalog.Resolve" => new JsonObject(AspectCatalog.Resolve((AppearanceMode)I(a, "mode"), AspectColors(a))
            .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, (JsonNode?)p.Value))),
        "AspectCatalog.Retro" => new JsonArray(AspectCatalog.Retro.Select(aspect => (JsonNode?)new JsonObject
        {
            ["mode"] = (int)aspect.Mode,
            ["id"] = aspect.Id,
            ["suggestedThemeId"] = aspect.SuggestedThemeId,
            ["slots"] = new JsonArray(aspect.Slots.Select(s => (JsonNode?)new JsonObject
                { ["id"] = s.Id, ["default"] = s.Default, ["suggestions"] = Array(s.Suggestions) }).ToArray()),
            ["presets"] = new JsonArray(aspect.Presets.Select(p => (JsonNode?)new JsonObject
            {
                ["id"] = p.Id, ["name"] = p.Name,
                ["colors"] = new JsonObject(p.Colors.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => KeyValuePair.Create(c.Key, (JsonNode?)c.Value))),
            }).ToArray()),
        }).ToArray()),
```

Añadir el ayudante (junto a `CustomThemes(a)`):

```csharp
    /// <summary>El argumento opcional <c>aspectColors</c> (id de hueco → hex), o null si no viene.</summary>
    private static Dictionary<string, string>? AspectColors(JsonObject a) =>
        a["aspectColors"] is JsonObject o ? o.ToDictionary(p => p.Key, p => p.Value!.GetValue<string>()) : null;
```

Si `Array(...)` no acepta `IReadOnlyList<string>`, usar `Array(s.Suggestions.ToList())` (comprobar la firma del ayudante `Array` existente y adaptar la llamada, no el ayudante).

En `ColorCases()`, justo después del bucle de `AppPalette.IsLight`, **sustituir el comentario de «solo los aspectos de siempre»** por este y añadir los casos:

```csharp
        // Los aspectos retro (XP, Telecom, Bash, Phosphor): paleta con los colores de fábrica y con un color extremo en todos los huecos
        // (blanco, negro, amarillo puro), igual que los tests de contraste de Core. Android las deriva con su propio port y estos casos
        // fijan que salgan idénticas.
        foreach (var aspect in AspectCatalog.Retro)
        {
            yield return Case("AppPalette.For", ("mode", (int)aspect.Mode), ("windowsUsesLight", false));
            foreach (var extreme in new[] { "#FFFFFF", "#000000", "#FFFF00" })
            {
                var chosen = new JsonObject();
                foreach (var slot in aspect.Slots) chosen[slot.Id] = extreme;
                yield return Case("AppPalette.For", ("mode", (int)aspect.Mode), ("windowsUsesLight", false), ("aspectColors", chosen));
            }
        }
        // Un solo hueco elegido, en minúsculas, y un valor que no es un color: el resto cae en el de fábrica.
        yield return Case("AppPalette.For", ("mode", 9), ("windowsUsesLight", false), ("aspectColors", new JsonObject { ["user"] = "#8ae234", ["path"] = "rojo" }));
        yield return Case("AspectCatalog.Retro");
        foreach (var aspect in AspectCatalog.Retro)
        {
            yield return Case("AspectCatalog.Resolve", ("mode", (int)aspect.Mode));
            yield return Case("AspectCatalog.Resolve", ("mode", (int)aspect.Mode),
                ("aspectColors", new JsonObject { [aspect.Slots[0].Id] = "#5a7a2e", [aspect.Slots[^1].Id] = "rojo", ["otro"] = "#123456" }));
        }
        yield return Case("AspectCatalog.Resolve", ("mode", 0), ("aspectColors", new JsonObject { ["accent"] = "#FF0000" }));

        // ColorFit y ColorMix: entradas ordinarias, en el límite y no válidas.
        foreach (var (a, b) in new[] { ("#FFFFFF", "#000000"), ("#0055E5", "#FFFFFF"), ("#FFFF00", "#FFFFFF"), ("rojo", "#000000"), ("#282828", "#B8BB26") })
            yield return Case("ColorFit.Ratio", ("a", a), ("b", b));
        foreach (var (color, other) in new[] { ("#FFFF00", "#FFFFFF"), ("#0055E5", "#FFFFFF"), ("#FFFFFF", "#FFFFFF"), ("#101010", "#202020"), ("rojo", "#FFFFFF"), ("#e8e8e8", "#171C21") })
        {
            yield return Case("ColorFit.Background", ("background", color), ("text", other), ("minimum", 4.5));
            yield return Case("ColorFit.Foreground", ("foreground", color), ("background", other), ("minimum", 4.5));
            yield return Case("ColorFit.Foreground", ("foreground", color), ("background", other), ("minimum", 3.5));
        }
        foreach (var color in new[] { "#0055E5", "#FFFFFF", "#000000", "#A8452A", "nope" })
        {
            yield return Case("ColorFit.Lighter", ("color", color), ("amount", 0.08));
            yield return Case("ColorFit.Darker", ("color", color), ("amount", 0.12));
        }
        foreach (var amount in new[] { 0.0, 0.15, 0.5, 0.9, 1.0, -1.0, 2.0 })
            yield return Case("ColorMix.Toward", ("from", "#A8E6B4"), ("to", "#000000"), ("amount", amount));
        yield return Case("ColorMix.Toward", ("from", "nope"), ("to", "#000000"), ("amount", 0.5));
        yield return Case("ColorMix.Toward", ("from", "#000000"), ("to", "#FFFFFF"), ("amount", 0.5));
```

Si el cuerpo de `ColorCases` ya usa nombres `a`/`b` o `color` en el mismo ámbito, renombrar las variables nuevas (`first`, `second`, `sample`) para evitar el choque de nombres.

- [ ] **Step 2: Regenerar y comprobar que Core sigue en verde**

Run:
```
ALDUNE_REGEN_VECTORS=1 dotnet test tests/Aldune.Core.Tests --filter "FullyQualifiedName~RegenerateTextVectors"
dotnet test tests/Aldune.Core.Tests --nologo -v q
git diff --stat tests/vectors
```
Expected: todos los tests de Core pasan; `git diff --stat` solo muestra `text-color.json` con líneas **añadidas** (los casos antiguos no cambian: si cambian, algo se ha roto, parar y mirar). `sync-format4.json`, `text-lines.json`, `text-order.json` sin cambios. Importante: no ejecutar `ALDUNE_REGEN_VECTORS` con el filtro de `SyncVectorsTests`.

- [ ] **Step 3: Operaciones en Kotlin que comprueban los vectores**

En `android/text/src/test/kotlin/app/aldune/text/Operations.kt`, sustituir la línea de `AppPalette.For` y añadir (junto a ella):

```kotlin
        "ColorFit.Ratio" -> num(ColorFit.ratio(a.s("a"), a.s("b")))
        "ColorFit.Background" -> str(ColorFit.background(a.s("background"), a.s("text"), a.d("minimum")))
        "ColorFit.Foreground" -> str(ColorFit.foreground(a.s("foreground"), a.s("background"), a.d("minimum")))
        "ColorFit.Lighter" -> str(ColorFit.lighter(a.s("color"), a.d("amount")))
        "ColorFit.Darker" -> str(ColorFit.darker(a.s("color"), a.d("amount")))
        "ColorMix.Toward" -> str(ColorMix.toward(a.s("from"), a.s("to"), a.d("amount")))
        "AppPalette.For" -> JsonObject(
            AppPalette.forMode(a.i("mode"), a.b("windowsUsesLight"), aspectColors(a)).toSortedMap().mapValues { JsonPrimitive(it.value) },
        )
        "AspectCatalog.Resolve" -> JsonObject(
            AspectCatalog.resolve(a.i("mode"), aspectColors(a)).toSortedMap().mapValues { JsonPrimitive(it.value) },
        )
        "AspectCatalog.Retro" -> JsonArray(
            AspectCatalog.retro.map { aspect ->
                obj(
                    "mode" to num(aspect.mode), "id" to str(aspect.id), "suggestedThemeId" to strOrNull(aspect.suggestedThemeId),
                    "slots" to JsonArray(aspect.slots.map { s -> obj("id" to str(s.id), "default" to str(s.default), "suggestions" to strings(s.suggestions)) }),
                    "presets" to JsonArray(
                        aspect.presets.map { p ->
                            obj("id" to str(p.id), "name" to str(p.name), "colors" to JsonObject(p.colors.toSortedMap().mapValues { JsonPrimitive(it.value) }))
                        },
                    ),
                )
            },
        )
```

y el ayudante, junto a los demás (`num`, `str`, `strOrNull`, `bool`, `obj`, `strings`, `s`, `i`, `b`): comprobar cuáles existen en el fichero y **reutilizar** los que haya (p. ej. `a.d(...)` para `Double` quizá se llame distinto: usar el existente; si no hay, añadir `private fun JsonObject.d(key: String) = this.getValue(key).jsonPrimitive.double`):

```kotlin
    /** El argumento opcional `aspectColors` (id de hueco → hex), o `null` si no viene. */
    private fun aspectColors(a: JsonObject): Map<String, String>? =
        (a["aspectColors"] as? JsonObject)?.mapValues { (it.value as JsonPrimitive).content }
```

Para `Double` en `ColorFit.Ratio`, `num(...)` debe aceptar `Double`; si solo acepta `Int`, usar el ayudante que ya se use para `OklchColor.DistanceTo` (devuelve un `Double`) en lugar de `num`.

- [ ] **Step 4: Ejecutar los vectores en Kotlin**

Run: `./gradlew :text:test -q` — Expected: `TextVectorsTest` pasa con los casos nuevos. Un caso rojo con una diferencia de ±1 en un canal es un fallo de port (orden de operaciones, `Math.floor(x + 0.5)` frente a `Math.Round`…): comparar con el C# y arreglar el **código de Kotlin**, no el vector.

- [ ] **Step 5: README y comentarios obsoletos**

En `tests/vectors/README.md`, sustituir la frase de la «Paleta de la app (F3b …)» («Los aspectos retro (5-10 …) son de Windows y no tienen vectores de paleta: en Android no se ofrecen.») por: «`AppPalette.For` acepta además `aspectColors` (id de hueco → hex) y los modos 5-10 (aspectos retro: XP, Telecom, Bash, Fósforo); `AspectCatalog.Retro` y `AspectCatalog.Resolve` fijan el catálogo (huecos, muestras, paletas de Bash, tema sugerido); `ColorFit.*` y `ColorMix.Toward` fijan el ajuste de contraste y la mezcla.». En `AppPalette.kt` ya se cambió la cabecera en la Task 3; comprobar que no queda la frase «no se llevan a Android».

- [ ] **Step 6: Commit**

```
git add tests/Aldune.Core.Tests/TextVectorsTests.cs tests/vectors/text-color.json tests/vectors/README.md android/text/src/test/kotlin/app/aldune/text/Operations.kt
git commit -m "Vectores de los aspectos retro, ColorFit y ColorMix; Android los verifica"
```

---

### Task 5: Ajustes: aspecto 0-10 y colores elegidos

**Files:**
- Create: `android/text/src/main/kotlin/app/aldune/text/AspectColorsJson.kt`
- Test: `android/text/src/test/kotlin/app/aldune/text/AspectColorsJsonTest.kt`
- Modify: `android/app/src/main/kotlin/app/aldune/data/SettingsStore.kt`

**Interfaces:**
- Consumes: `AppPalette.DARK`, `AppPalette.PHOSPHOR`, `AppPalette.SYSTEM`; `kotlinx.serialization.json` (ya lo usa el módulo `text`/`core`; comprobar que `text/build.gradle.kts` lo tiene y, si no, añadirlo como en `core`).
- Produces: `AspectColorsJson.parse(json: String?): Map<String, Map<String, String>>` (lo ilegible → vacío; un valor que no es `#RRGGBB` se descarta); `AspectColorsJson.write(colors: Map<String, Map<String, String>>): String`; `AppSettings.aspectColors: Map<String, Map<String, String>>` (por defecto vacío); `AppSettings.appearance` acepta 0-10.

- [ ] **Step 1: Escribir los tests que fallan**

```kotlin
package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

class AspectColorsJsonTest {
    @Test fun `ida y vuelta conserva lo elegido`() {
        val colors = mapOf("bash" to mapOf("accent" to "#D79921"), "xp-light" to mapOf("titleBar" to "#A8452A"))
        assertEquals(colors, AspectColorsJson.parse(AspectColorsJson.write(colors)))
    }

    @Test fun `la forma es la de AspectColors de settings json de Windows`() =
        assertEquals("""{"bash":{"accent":"#D79921"}}""", AspectColorsJson.write(mapOf("bash" to mapOf("accent" to "#D79921"))))

    // Review Focus 3: un valor roto cae en el de fábrica (aquí, se descarta) y no rompe nada.
    @Test fun `un json ilegible, vacio o nulo da un mapa vacio`() {
        assertEquals(emptyMap<String, Map<String, String>>(), AspectColorsJson.parse(null))
        assertEquals(emptyMap<String, Map<String, String>>(), AspectColorsJson.parse(""))
        assertEquals(emptyMap<String, Map<String, String>>(), AspectColorsJson.parse("{no es json"))
        assertEquals(emptyMap<String, Map<String, String>>(), AspectColorsJson.parse("[1,2]"))
    }

    @Test fun `descarta lo que no es un color y conserva el resto`() {
        val json = """{"bash":{"accent":"rojo","user":"#8ae234","path":5},"xp-light":"x"}"""
        assertEquals(mapOf("bash" to mapOf("user" to "#8AE234")), AspectColorsJson.parse(json))
    }
}
```

- [ ] **Step 2: Ejecutar y ver que falla**

Run: `./gradlew :text:test -q` — Expected: `Unresolved reference 'AspectColorsJson'`.

- [ ] **Step 3: Implementar**

`AspectColorsJson.kt`:

```kotlin
package app.aldune.text

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put
import java.util.Locale

/**
 * `aspectColors` en `SharedPreferences`: la misma forma que `AspectColors` de `settings.json` en Windows (`{"bash":{"accent":"#D79921"}}`:
 * id de aspecto → id de hueco → `#RRGGBB`). Lo ilegible o lo que no es un color se descarta: un valor roto cae en el de fábrica.
 */
object AspectColorsJson {
    fun parse(json: String?): Map<String, Map<String, String>> {
        if (json.isNullOrBlank()) return emptyMap()
        val root = runCatching { Json.parseToJsonElement(json) }.getOrNull() as? JsonObject ?: return emptyMap()
        val result = linkedMapOf<String, Map<String, String>>()
        for ((aspect, slots) in root) {
            val chosen = linkedMapOf<String, String>()
            for ((slot, value) in (slots as? JsonObject).orEmpty()) {
                val hex = (value as? JsonPrimitive)?.takeIf { it.isString }?.content
                if (hex != null && NoteDisplayColor.isActive(hex)) chosen[slot] = hex.uppercase(Locale.ROOT)
            }
            if (chosen.isNotEmpty()) result[aspect] = chosen
        }
        return result
    }

    fun write(colors: Map<String, Map<String, String>>): String = buildJsonObject {
        for ((aspect, slots) in colors) put(aspect, buildJsonObject { for ((slot, hex) in slots) put(slot, hex) })
    }.toString()
}
```

En `SettingsStore.kt`: añadir en `AppSettings`

```kotlin
    /** Los colores que el usuario eligió para cada aspecto retro (id de aspecto → id de hueco → `#RRGGBB`); vacío = los de fábrica. */
    val aspectColors: Map<String, Map<String, String>> = emptyMap(),
```

y cambiar el comentario de `appearance` a «… Medianoche 4, XP claro 5, XP oscuro 6, Telecom claro 7, Telecom oscuro 8, Bash 9, Fósforo 10». En `load()`: `appearance = prefs.getInt(APPEARANCE, d.appearance).takeIf { it in AppPalette.DARK..AppPalette.PHOSPHOR } ?: d.appearance` y `aspectColors = AspectColorsJson.parse(prefs.getString(ASPECT_COLORS, null))`; en `save()`: `.putString(ASPECT_COLORS, AspectColorsJson.write(settings.aspectColors))`; constante `const val ASPECT_COLORS = "aspectColors"`. Importar `app.aldune.text.AspectColorsJson`.

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `./gradlew :text:test :app:assembleDebug -q` — Expected: `AspectColorsJsonTest` verde y la app compila. Review Focus 4 (un valor 5-10 leído por una versión **antigua** cae en Sistema) lo garantiza el `takeIf` que ya tenía esa versión (`DARK..MIDNIGHT` → `d.appearance`); no necesita código, pero **verifícalo a mano** en la Task 8 instalando la rc4 sobre una configuración con `appearance=9` si el usuario lo pide.

- [ ] **Step 5: Commit**

```
git add android/text/src/main/kotlin/app/aldune/text/AspectColorsJson.kt android/text/src/test/kotlin/app/aldune/text/AspectColorsJsonTest.kt android/app/src/main/kotlin/app/aldune/data/SettingsStore.kt
git commit -m "Android: ajustes con aspectos 0-10 y colores elegidos por aspecto"
```

---

### Task 6: Tema de Compose, widget y fuente

**Files:**
- Modify: `android/app/src/main/kotlin/app/aldune/ui/AldunaTheme.kt`, `android/app/src/main/kotlin/app/aldune/MainActivity.kt`, `android/app/src/main/kotlin/app/aldune/widget/DockWidget.kt`
- Create (si hace falta): `android/app/src/main/kotlin/app/aldune/ui/Typography.kt`

**Interfaces:**
- Consumes: `AppPalette.forMode(mode, systemLight, aspectColors)`, `AppSettings.appearance`, `AppSettings.aspectColors`; `AspectCatalog.forMode(mode)?.id`.
- Produces: `AldunaTheme(appearance: Int, aspectColors: Map<String, Map<String, String>>, content)`; `fun isMonospaceAspect(appearance: Int): Boolean`.

- [ ] **Step 1: Pasar los colores elegidos al tema**

En `AldunaTheme.kt`: nuevo parámetro `aspectColors: Map<String, Map<String, String>> = emptyMap()`; calcular los del aspecto activo y pasarlos:

```kotlin
@Composable
fun AldunaTheme(appearance: Int = APPEARANCE_SYSTEM, aspectColors: Map<String, Map<String, String>> = emptyMap(), content: @Composable () -> Unit) {
    val systemLight = !isSystemInDarkTheme()
    val chosen = AspectCatalog.forMode(appearance)?.let { aspectColors[it.id] }
    val scheme = remember(appearance, systemLight, chosen) {
        val palette = AppPalette.forMode(appearance, systemLight, chosen)
        // … (el resto igual)
```

y `MaterialTheme(colorScheme = scheme, typography = typographyFor(appearance), content = content)`. Importar `app.aldune.text.AspectCatalog`. Actualizar el comentario de `APPEARANCE_SYSTEM` con los números 5-10.

- [ ] **Step 2: Fuente monoespaciada**

`Typography.kt`:

```kotlin
package app.aldune.ui

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import app.aldune.text.AppPalette

/** Bash, Telecomunicaciones (claro y oscuro) y Fósforo usan fuente monoespaciada, como Cascadia Mono en Windows. XP y los demás, la de siempre. */
internal fun isMonospaceAspect(appearance: Int): Boolean =
    appearance == AppPalette.TELECOM_LIGHT || appearance == AppPalette.TELECOM_DARK ||
        appearance == AppPalette.BASH || appearance == AppPalette.PHOSPHOR

/** La tipografía de Material con la familia monoespaciada del sistema en los aspectos que la piden. */
internal fun typographyFor(appearance: Int): Typography {
    val base = Typography()
    if (!isMonospaceAspect(appearance)) return base
    fun TextStyle.mono() = copy(fontFamily = FontFamily.Monospace)
    return base.copy(
        displayLarge = base.displayLarge.mono(), displayMedium = base.displayMedium.mono(), displaySmall = base.displaySmall.mono(),
        headlineLarge = base.headlineLarge.mono(), headlineMedium = base.headlineMedium.mono(), headlineSmall = base.headlineSmall.mono(),
        titleLarge = base.titleLarge.mono(), titleMedium = base.titleMedium.mono(), titleSmall = base.titleSmall.mono(),
        bodyLarge = base.bodyLarge.mono(), bodyMedium = base.bodyMedium.mono(), bodySmall = base.bodySmall.mono(),
        labelLarge = base.labelLarge.mono(), labelMedium = base.labelMedium.mono(), labelSmall = base.labelSmall.mono(),
    )
}
```

El editor de notas: buscar en `NoteEditorPane.kt` el `TextStyle` del campo de texto (`BasicTextField`/`textStyle`) y, si fija su propia `fontFamily` o usa `LocalTextStyle`, comprobar que hereda la tipografía del tema; si no hereda, hacer que lo haga (`MaterialTheme.typography.bodyLarge` en lugar de un estilo literal). Verificarlo en la sonda (Task 8).

- [ ] **Step 3: MainActivity y widget**

`MainActivity.kt:81`: `AldunaTheme(settings.appearance, settings.aspectColors) {`. En `DockWidget.kt`, `colors(appearance)` pasa a `colors(settings.appearance, settings.aspectColors)` y a `AppPalette.forMode(appearance, systemUsesLight = false, AspectCatalog.forMode(appearance)?.let { aspectColors[it.id] })`. Importar `AspectCatalog`.

- [ ] **Step 4: Compilar y probar**

Run: `./gradlew test :app:assembleDebug :app:lintDebug -q` — Expected: sin errores; `lint` sin avisos nuevos.

- [ ] **Step 5: Commit**

```
git add android/app/src/main/kotlin/app/aldune/ui/AldunaTheme.kt android/app/src/main/kotlin/app/aldune/ui/Typography.kt android/app/src/main/kotlin/app/aldune/MainActivity.kt android/app/src/main/kotlin/app/aldune/widget/DockWidget.kt
git commit -m "Android: el tema y el widget usan los aspectos retro; fuente monoespaciada donde toca"
```

---

### Task 7: Pantalla de Ajustes: aspectos, colores y tema sugerido

**Files:**
- Create: `android/app/src/main/kotlin/app/aldune/ui/AspectSection.kt`
- Modify: `android/app/src/main/kotlin/app/aldune/ui/SettingsScreen.kt`, `android/strings.tsv` (y los `values*/strings.xml` generados)

**Interfaces:**
- Consumes: `AspectCatalog.retro`, `AspectCatalog.forMode`, `AspectDefinition`, `AspectColorSlot`, `AspectPreset`, `AspectCatalog.resolve`, `CustomColorDialog(initial: String, onDismiss: () -> Unit, onApply: (String) -> Unit)` (ya existe), `AppSettings`, `viewModel.updateSettings { it.copy(...) }`, `themeNameRes(id)`, `noteFace(hex)`.
- Produces: `@Composable fun AspectSection(settings: AppSettings, onChange: ((AppSettings) -> AppSettings) -> Unit)`.

- [ ] **Step 1: Textos en los cinco idiomas**

Añadir a `android/strings.tsv` (después de `appearance_midnight`; columnas en/es/de/fr/pt-BR, copiadas de `Strings.cs`; **comprobar cada una en `src/Aldune/Resources/Strings.cs` antes de copiarla**: `AppearanceXpLight`, `AppearanceXpDark`, `AppearanceTelecomLight`, `AppearanceTelecomDark`, `AppearanceBash`, `AppearancePhosphor`):

| clave | contenido |
|---|---|
| `appearance_xp_light` … `appearance_phosphor` | los seis nombres de arriba |
| `aspect_colors` | título del bloque: «Colores del aspecto» (buscar el texto del PC en `Strings.cs`: sección de colores de Ajustes → Aspecto; si no existe uno igual, «Aspect colors» / «Colores del aspecto» / «Aspektfarben» / «Couleurs de l’apparence» / «Cores do aspecto») |
| `aspect_color_change` | «Cambiar» (reutilizar la clave del PC) |
| `aspect_colors_reset` | «Restablecer colores» (texto del PC) |
| `aspect_presets` | «Paletas» (texto del PC para las paletas de Bash) |
| `aspect_slot_<id>` | un nombre por hueco del PC: `titleBar`, `accent`, `ink`, `panel`, `trace`, `background`, `user`, `path`, `phosphor`, `details` (buscarlos en `Strings.cs`; clave `aspect_slot_title_bar`, `aspect_slot_accent`, …) |
| `aspect_suggest_title`, `aspect_suggest_body` (con `%1$s`), `aspect_suggest_apply`, `aspect_suggest_keep` | los de `Strings.AspectSuggestTheme` del PC; el cuerpo recibe el nombre del tema |

Luego: `python android/scripts/gen_strings.py` y `python android/scripts/gen_strings.py --check`.

- [ ] **Step 2: La lista de aspectos**

En `SettingsScreen.kt`, la lista `appearances` pasa a incluir los seis (mismo orden que el PC: Sistema, Oscuro, Claro, Pastel, Medianoche, XP claro, XP oscuro, Telecom claro, Telecom oscuro, Bash, Fósforo):

```kotlin
                val appearances = listOf(
                    AppPalette.SYSTEM to R.string.appearance_system, AppPalette.DARK to R.string.appearance_dark,
                    AppPalette.LIGHT to R.string.appearance_light, AppPalette.PASTEL to R.string.appearance_pastel,
                    AppPalette.MIDNIGHT to R.string.appearance_midnight,
                    AppPalette.XP_LIGHT to R.string.appearance_xp_light, AppPalette.XP_DARK to R.string.appearance_xp_dark,
                    AppPalette.TELECOM_LIGHT to R.string.appearance_telecom_light, AppPalette.TELECOM_DARK to R.string.appearance_telecom_dark,
                    AppPalette.BASH to R.string.appearance_bash, AppPalette.PHOSPHOR to R.string.appearance_phosphor,
                )
```

Al elegir uno con `suggestedThemeId` distinto del tema activo (`settings.noteThemeId`), se guarda `pendingSuggestion` en un `remember` y sale un `AlertDialog` con «Aplicar» (`updateSettings { it.copy(noteThemeId = suggested) }`) y «Mantener». El nombre del tema: `themeNameRes(id)?.let { stringResource(it) } ?: id`. Debajo de la lista: `AspectSection(settings, onChange = viewModel::updateSettings)`.

- [ ] **Step 3: El bloque de colores** (`AspectSection.kt`)

Comportamiento (sin código de relleno: son estos elementos, con el estilo del resto de `SettingsScreen`):
- Si `AspectCatalog.forMode(settings.appearance) == null` no pinta nada.
- Cabecera `R.string.aspect_colors`. Por cada `slot` de `aspect.slots`: renglón con nombre (`aspect_slot_<id>` vía un `when (slot.id)` a `R.string…`), un círculo con el color efectivo (`AspectCatalog.resolve(mode, settings.aspectColors[aspect.id])[slot.id]`, con `noteFace`) y el valor `#RRGGBB`, y un botón «Cambiar» que abre `CustomColorDialog(initial = efectivo, …, onApply = { hex -> onChange { s -> s.copy(aspectColors = s.aspectColors + (aspect.id to ((s.aspectColors[aspect.id] ?: emptyMap()) + (slot.id to hex)))) } })`.
- Debajo de cada renglón, las muestras (`slot.suggestions`) como círculos de toque (`Modifier.clickable`, con `contentDescription` = hex) que aplican igual que «Cambiar».
- Si `aspect.presets` no está vacío: fila «Paletas» con un botón por paleta (`preset.name`) que sustituye **todos** los huecos del aspecto por `preset.colors`.
- Botón «Restablecer colores»: quita la entrada del aspecto (`s.copy(aspectColors = s.aspectColors - aspect.id)`).
- Un solo helper `fun AppSettings.withAspectColor(aspectId: String, slotId: String, hex: String): AppSettings` en este fichero para no repetir la expresión de arriba.

- [ ] **Step 4: Compilar**

Run: `./gradlew test :app:assembleDebug :app:lintDebug -q` — Expected: sin errores ni avisos nuevos. Si la lista de Ajustes se vuelve demasiado larga, **no rediseñar aquí**: se decide en la sonda de la Task 8.

- [ ] **Step 5: Commit**

```
git add android/app/src/main/kotlin/app/aldune/ui/AspectSection.kt android/app/src/main/kotlin/app/aldune/ui/SettingsScreen.kt android/strings.tsv android/app/src/main/res/values/strings.xml android/app/src/main/res/values-de/strings.xml android/app/src/main/res/values-es/strings.xml android/app/src/main/res/values-fr/strings.xml android/app/src/main/res/values-pt-rBR/strings.xml
git commit -m "Android: Ajustes ofrece los aspectos retro con sus colores y el tema de notas sugerido"
```

---

### Task 8: Sonda en el Pixel y cierre

**Files:**
- Modify: `docs/ROADMAP.md` (§13: nota de que está hecho), `docs/historia/…` o `docs/ANDROID_PROBES.md` (resultado de la sonda), la memoria del proyecto `aldune_continuar_f7.md`.

**Interfaces:** ninguna (verificación y documentación).

- [ ] **Step 1: Pedir al usuario que conecte el Pixel** (con depuración USB y «No molestar» puesto) y comprobar `adb devices`. `ADB="$LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe"`, serie `61301JEA317871`, `export MSYS_NO_PATHCONV=1`. Instalar `app-debug.apk` (paquete `io.github.nachopola16.aldune.debug`): solo notas inventadas.

- [ ] **Step 2: Recorrido con capturas** (`screencap` a la carpeta de scratchpad de la sesión; coordenadas = las de la imagen mostrada × 1.21 en un Pixel 10a de 1080×2424):
  1. Ajustes → Aspecto: están los 11 y se ven con los nombres traducidos.
  2. Elegir **Bash**: la lista, el editor y los diálogos se ven con la paleta de terminal; el texto es monoespaciado; sale el diálogo del tema sugerido (Sereno) y «Aplicar» lo cambia.
  3. Cambiar «Acento» con «Cambiar» y con una muestra; probar una paleta (Ubuntu); «Restablecer colores» vuelve a Gruvbox.
  4. Repetir con **XP claro**, **Telecom claro**, **Telecom oscuro**, **XP oscuro** y **Fósforo** con un color extremo (blanco y negro) en algún hueco: nada ilegible.
  5. Con un aspecto retro elegido, el widget (si el usuario lo tiene puesto) y la barra de estado siguen la claridad del aspecto.
  6. Volver a «Según el sistema»: la app vuelve a la de siempre.
  Registrar cualquier texto ilegible, desbordamiento o lista de Ajustes incómoda. **Si algo falla, arreglarlo con test primero cuando sea lógica, y repetir.**

- [ ] **Step 3: Documentar el cierre**

Marcar el punto en `docs/ROADMAP.md` §13 (aspectos retro en Android: hecho, con la fecha), anotar el resultado de la sonda donde corresponda, actualizar `docs/ANDROID_PROBES.md` si procede, y la memoria `aldune_continuar_f7.md`. Comprobar `git log --format='%an <%ae> | %cn <%ce>' origin/main..HEAD` (todo NachoPola16) y `git status` (descartar `tests/vectors/kotlin-output.json` si aparece modificado).

- [ ] **Step 4: Commit y subida**

```
git add docs/ROADMAP.md docs/ANDROID_PROBES.md
git commit -m "Docs: aspectos retro de Android probados en el Pixel"
```
El push lo hace Claude solo si el usuario lo ha pedido en esta sesión; la publicación de una rc nueva siempre se confirma con el usuario.

---

## Autorrevisión

- **Cobertura de la spec:** núcleo (Tasks 1-3), vectores (Task 4), ajustes (Task 5), tema/widget/fuente (Task 6), interfaz con huecos, paletas, restablecer y tema sugerido (Task 7), pruebas de contraste portadas (Task 3), sonda (Task 8). Sin tarea para la «piel» porque está fuera de alcance.
- **Consistencia de tipos:** `AspectCatalog.resolve(mode: Int, chosen: Map<String,String>?)`, `AppPalette.forMode(mode, systemUsesLight, aspectColors: Map<String,String>?)`, `AppSettings.aspectColors: Map<String, Map<String,String>>` (id de aspecto → id de hueco → hex); en las Tasks 6 y 7 se convierte con `AspectCatalog.forMode(mode)?.id` antes de llamar a `forMode`.
- **Pendiente de comprobar al ejecutar (no son huecos del plan, son dependencias del código existente):** firma exacta de los ayudantes `num/str/strOrNull/obj/strings/a.d` de `Operations.kt` y de `Array(...)` en `TextVectorsTests.cs` (Task 4); el estilo de texto del editor en `NoteEditorPane.kt` (Task 6); los textos exactos del PC para los huecos y el diálogo del tema sugerido (Task 7). Cada paso dice dónde mirar.
