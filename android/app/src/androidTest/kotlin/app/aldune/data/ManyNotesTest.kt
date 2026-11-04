package app.aldune.data

import android.content.Context
import android.util.Log
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import app.aldune.domain.AesGcmNoteCipher
import app.aldune.domain.LineEnding
import app.aldune.domain.Note
import app.aldune.domain.Clock
import app.aldune.domain.IdFactory
import app.aldune.domain.NoteListing
import app.aldune.domain.NoteListOrder
import app.aldune.domain.NoteService
import app.aldune.domain.NoteState
import app.aldune.text.MarkdownLive
import app.aldune.text.NoteSearch
import java.time.Instant
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

/**
 * A big notebook on a real device: how long the store takes to load and write thousands of notes, and what the list does with them.
 * In-memory database, never the app's own. The timings go to logcat (tag ManyNotes); the bounds are generous and only catch a collapse.
 */
@RunWith(AndroidJUnit4::class)
class ManyNotesTest {
    private lateinit var db: AldunaDatabase
    private val cipher = AesGcmNoteCipher(ByteArray(32) { it.toByte() })
    private lateinit var store: RoomNoteStore
    private val count = 3000

    @Before fun setUp() {
        val context = ApplicationProvider.getApplicationContext<Context>()
        db = Room.inMemoryDatabaseBuilder(context, AldunaDatabase::class.java).build()
        store = RoomNoteStore(db, cipher)
    }

    @After fun tearDown() = db.close()

    private fun body(i: Int) = buildString {
        append("Note $i title\n")
        repeat(8) { append("line $it of note $i with **bold**, *italic* and a [link](https://example.com/$i) plus some filler words\n") }
        append("☐ task one\n☐ task two\n")
    }

    private fun note(i: Int) = Note(
        id = "id-$i", text = body(i), lineEnding = LineEnding.LF, color = "#EBD38B",
        createdAt = Instant.parse("2026-10-17T10:00:00Z").plusSeconds(i.toLong()), updatedAt = Instant.parse("2026-10-17T10:30:00Z").plusSeconds(i.toLong()),
        state = NoteState.ACTIVE, tags = listOf("tag${i % 20}", "all"),
    )

    private inline fun <T> timed(label: String, block: () -> T): Pair<T, Long> {
        val start = System.nanoTime()
        val result = block()
        val ms = (System.nanoTime() - start) / 1_000_000
        Log.i("ManyNotes", "$label: $ms ms")
        return result to ms
    }

    @Test fun aBigNotebookStaysUsable() {
        val (_, writeOneByOne) = timed("put $count notes one by one") { for (i in 0 until count) store.put(note(i)) }
        val (loaded, readAll) = timed("all() with $count notes (decrypts every one)") { store.all() }
        assertEquals(count, loaded.size)
        val (_, readOne) = timed("get one") { store.get("id-1500") }
        val (hits, search) = timed("search 'note 2999' over $count notes") { loaded.filter { NoteSearch.matchesNote(it.text, it.tags, "note 2999") } }
        val (_, parseAll) = timed("MarkdownLive.parse of every note") { loaded.sumOf { MarkdownLive.parse(it.text).size } }
        val (_, rewrite) = timed("put 100 notes again (an edit each)") { for (i in 0 until 100) store.put(note(i).copy(text = body(i) + "edited")) }
        assertTrue(hits.isNotEmpty())
        // What the view model does after every change to the notes (refreshLocked): read and decrypt all, split by state, filter, sort.
        val service = NoteService(store, Clock { Instant.parse("2026-11-04T10:00:00Z") }, "device-a", IdFactory { "new-${System.nanoTime()}" })
        val (_, refresh) = timed("one refresh (listByState + matching + sort) with $count notes") {
            val byState = service.listByState()
            NoteListing.sort(service.matching(byState.getValue(NoteState.ACTIVE), "", null), NoteListOrder.TITLE).size
        }
        val (_, refreshQuery) = timed("one refresh with a query") {
            NoteListing.sort(service.matching(service.listByState().getValue(NoteState.ACTIVE), "note 2999", null), NoteListOrder.TITLE).size
        }
        Log.i("ManyNotes", "refresh=$refresh ms, refreshWithQuery=$refreshQuery ms")
        assertTrue("a refresh took $refresh ms", refresh < 4_000)
        assertTrue("a refresh with a query took $refreshQuery ms", refreshQuery < 5_000)
        Log.i("ManyNotes", "summary: write=$writeOneByOne ms, readAll=$readAll ms, readOne=$readOne ms, search=$search ms, parse=$parseAll ms, rewrite100=$rewrite ms")
        assertTrue("reading everything took $readAll ms", readAll < 8_000)
        assertTrue("one edit takes too long: ${rewrite / 100.0} ms", rewrite < 5_000)
        assertTrue("searching took $search ms", search < 3_000)
    }
}
