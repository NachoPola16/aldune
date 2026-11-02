package app.aldune.ui

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * `SETTINGS_INDEX` is written by hand (Compose has no list of what a screen holds), so these tests keep it in step with `SettingsScreen`:
 * they read the sources, so a switch added to a tab without its line in the index fails here instead of being silently unsearchable.
 */
class SettingsIndexTest {
    private val ui = File("src/main/kotlin/app/aldune/ui")
    private val index = File(ui, "SettingsSearch.kt").readText()

    /** The `R.string` names the index lists, with their tab, read from the `SettingsEntry(tab, R.string.x...)` calls. */
    private val entries: List<Pair<Int, String>> = Regex("""SettingsEntry\((\d+),\s*R\.string\.(\w+)""").findAll(index).map { it.groupValues[1].toInt() to it.groupValues[2] }.toList()

    @Test
    fun `the index is not empty and was parsed`() {
        assertTrue("parsed ${entries.size} entries", entries.size > 50)
    }

    @Test
    fun `every entry points at a string that exists`() {
        val strings = File("src/main/res/values/strings.xml").readText()
        val missing = entries.map { it.second }.distinct().filter { """name="$it"""" !in strings }
        assertEquals("strings missing for index entries", emptyList<String>(), missing)
    }

    @Test
    fun `every entry belongs to a tab that exists`() {
        assertEquals(emptyList<Int>(), entries.map { it.first }.filter { it !in 0..5 })
    }

    @Test
    fun `an option is not listed twice in the same tab`() {
        val repeated = entries.groupingBy { it }.eachCount().filterValues { it > 1 }.keys
        assertEquals(emptyList<Pair<Int, String>>(), repeated.toList())
    }

    @Test
    fun `every switch of Settings has its line in the index`() {
        val listed = entries.map { it.second }.toSet()
        val switches = ui.listFiles { f -> f.extension == "kt" }.orEmpty().flatMap { file ->
            Regex("""SwitchRow\(\s*stringResource\(R\.string\.(\w+)\)""").findAll(file.readText()).map { it.groupValues[1] }.toList()
        }.distinct()
        assertTrue("no SwitchRow found: the pattern is out of date", switches.size > 10)
        assertEquals("switches missing from SETTINGS_INDEX", emptyList<String>(), switches.filter { it !in listed })
    }

    @Test
    fun `every section title of Settings has its line in the index`() {
        val listed = entries.map { it.second }.toSet()
        val sections = File(ui, "SettingsScreen.kt").readText().let { Regex("""Section\(R\.string\.(\w+)\)""").findAll(it).map { m -> m.groupValues[1] }.toList() }.distinct()
        assertTrue("no Section found: the pattern is out of date", sections.isNotEmpty())
        assertEquals("sections missing from SETTINGS_INDEX", emptyList<String>(), sections.filter { it !in listed })
    }
}
