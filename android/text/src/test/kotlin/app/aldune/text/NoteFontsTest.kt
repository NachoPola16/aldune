package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/** The same cases as `NoteFontTests` on Windows: the two implementations of the check must agree. */
class NoteFontsTest {
    private fun u16(out: MutableList<Byte>, v: Int) { out.add((v shr 8).toByte()); out.add(v.toByte()) }
    private fun u32(out: MutableList<Byte>, v: Long) { u16(out, (v shr 16).toInt() and 0xFFFF); u16(out, v.toInt() and 0xFFFF) }

    /** A minimal sfnt: header, one table record ('name') and a name table with the given records (platform, nameId, text). */
    private fun font(tag: String = "ttf", vararg names: Triple<Int, Int, String>): ByteArray {
        val strings = mutableListOf<Byte>()
        val records = mutableListOf<Byte>()
        for ((platform, nameId, text) in names) {
            val bytes = if (platform == 1) text.toByteArray(Charsets.ISO_8859_1) else text.toByteArray(Charsets.UTF_16BE)
            u16(records, platform); u16(records, if (platform == 1) 0 else 1); u16(records, 0)
            u16(records, nameId); u16(records, bytes.size); u16(records, strings.size)
            strings.addAll(bytes.toList())
        }
        val table = mutableListOf<Byte>()
        u16(table, 0); u16(table, names.size); u16(table, 6 + records.size)
        table.addAll(records); table.addAll(strings)
        val file = mutableListOf<Byte>()
        if (tag == "OTTO") file.addAll("OTTO".toByteArray().toList()) else file.addAll(listOf(0, 1, 0, 0))
        u16(file, 1); u16(file, 16); u16(file, 0); u16(file, 0)
        file.addAll("name".toByteArray().toList())
        file.addAll(List(4) { 0 })
        u32(file, 28); u32(file, table.size.toLong())
        file.addAll(table)
        return file.toByteArray()
    }

    @Test fun readsTheFamilyOfATrueTypeFont() {
        val info = FontFileInspector.inspect(font("ttf", Triple(3, 1, "Lora")), "lora.ttf")
        assertTrue(info.isValid); assertEquals("Lora", info.family)
    }

    @Test fun readsTheFamilyOfAnOpenTypeFont() {
        val info = FontFileInspector.inspect(font("OTTO", Triple(3, 1, "Fira Sans")), "x.OTF")
        assertTrue(info.isValid); assertEquals("Fira Sans", info.family)
    }

    @Test fun prefersTheTypographicFamilyOverTheLegacyOne() =
        assertEquals("Roboto", FontFileInspector.inspect(font("ttf", Triple(3, 1, "Roboto Light"), Triple(3, 16, "Roboto")), "r.ttf").family)

    @Test fun readsAMacintoshRomanNameWhenThereIsNoWindowsOne() =
        assertEquals("Old Mac", FontFileInspector.inspect(font("ttf", Triple(1, 1, "Old Mac")), "m.ttf").family)

    @Test fun refusesAWrongExtension() {
        for (name in listOf("font.exe", "font", "font.ttf.txt"))
            assertEquals(FontFileProblem.EXTENSION, FontFileInspector.inspect(font("ttf", Triple(3, 1, "X")), name).problem, name)
    }

    @Test fun refusesBytesThatAreNotAFont() =
        assertEquals(FontFileProblem.NOT_A_FONT, FontFileInspector.inspect("MZ this is a program, not a font........".toByteArray(), "evil.ttf").problem)

    @Test fun refusesAFileLargerThanTenMegabytes() =
        assertEquals(FontFileProblem.TOO_LARGE, FontFileInspector.inspect(ByteArray(FontFileInspector.MAX_BYTES + 1), "big.ttf").problem)

    @Test fun refusesAFontWithoutAReadableName() =
        assertEquals(FontFileProblem.NO_NAME, FontFileInspector.inspect(font("ttf"), "n.ttf").problem)

    @Test fun refusesTruncatedAndLyingTables() {
        val f = font("ttf", Triple(3, 1, "Lora"))
        assertEquals(FontFileProblem.NOT_A_FONT, FontFileInspector.inspect(f.copyOfRange(0, 20), "t.ttf").problem)
        val lying = f.copyOf(); lying[20] = 0x7F // the table offset points far past the end
        assertFalse(FontFileInspector.inspect(lying, "l.ttf").isValid)
    }

    @Test fun aCollectionIsNotAccepted() =
        assertEquals(FontFileProblem.NOT_A_FONT, FontFileInspector.inspect("ttcf".toByteArray() + ByteArray(64), "c.ttf").problem)

    @Test fun anUnknownOrWindowsOnlyChoiceLoadsAsSystem() {
        assertEquals(NoteFonts.SYSTEM, NoteFonts.normalize(99))
        assertEquals(NoteFonts.SYSTEM, NoteFonts.normalize(4)) // "installed font": Windows only
        assertEquals(NoteFonts.IMPORTED, NoteFonts.normalize(NoteFonts.IMPORTED))
    }

    @Test fun lookWithItsOwnTextStyleKeepsIt() {
        assertFalse(NoteFonts.applies(monospaceLook = true, serifLook = false))
        assertFalse(NoteFonts.applies(monospaceLook = false, serifLook = true))
        assertTrue(NoteFonts.applies(monospaceLook = false, serifLook = false))
    }
}
