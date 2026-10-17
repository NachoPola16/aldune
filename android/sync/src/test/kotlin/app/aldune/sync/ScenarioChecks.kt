package app.aldune.sync

import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncNote
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.doubleOrNull
import java.time.OffsetDateTime
import java.util.UUID
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue

private fun JsonElement?.isNull() = this == null || this is JsonNull
private fun JsonObject.text(name: String): String? = (this[name] as? JsonPrimitive)?.takeIf { it !is JsonNull }?.content
private fun JsonObject.flag(name: String): Boolean = ((this[name] as? JsonPrimitive)?.booleanOrNull) ?: false

/** Lo que dice el plan por sí solo: contadores, error, conflictos y bases. Sin ejecutar nada. */
fun assertPlanMatches(s: Scenario, plan: SyncPlan, now: OffsetDateTime) {
    val e = s.expected
    assertEquals(e.text("error"), plan.error, "${s.name}: error")
    if (plan.error != null) {
        assertTrue(plan.actions.isEmpty(), "${s.name}: con error no hay acciones")
        assertTrue(plan.newBases.isEmpty(), "${s.name}: con error no hay bases")
    }
    assertEquals(counter(e, "uploaded"), plan.uploaded, "${s.name}: uploaded")
    assertEquals(counter(e, "downloaded"), plan.downloaded, "${s.name}: downloaded")
    assertEquals(counter(e, "conflictsResolved"), plan.conflictsResolved, "${s.name}: conflictsResolved")
    assertEquals(counter(e, "tombstonesApplied"), plan.tombstonesApplied, "${s.name}: tombstonesApplied")
    assertConflicts(s, plan.actions.filterIsInstance<SyncAction.SaveConflict>().map { it.conflict })
    assertBases(s, s.bases + plan.newBases, now)
}

/** Lo que queda tras ejecutar el plan con los dobles en memoria: local, almacén remoto, bases y conflictos. */
fun assertExecutionMatches(
    s: Scenario, result: SyncResult, local: InMemoryLocalStore, transport: InMemorySyncTransport, now: OffsetDateTime,
) {
    val e = s.expected
    assertEquals(e.text("error"), result.error, "${s.name}: error")
    assertEquals(counter(e, "uploaded"), result.uploaded, "${s.name}: uploaded")
    assertEquals(counter(e, "downloaded"), result.downloaded, "${s.name}: downloaded")
    assertEquals(counter(e, "conflictsResolved"), result.conflictsResolved, "${s.name}: conflictsResolved")
    assertEquals(counter(e, "tombstonesApplied"), result.tombstonesApplied, "${s.name}: tombstonesApplied")
    assertConflicts(s, local.conflicts)
    assertBases(s, local.bases, now)
    assertLocal(s, local, now)
    assertRemote(s, transport, now)
    assertTrue(local.setBasesCalls <= 1, "${s.name}: las bases se guardan una sola vez")
}

private fun counter(e: JsonObject, name: String): Int = (e.getValue(name) as JsonPrimitive).content.toInt()

private fun assertConflicts(s: Scenario, actual: List<SyncConflict>) {
    val expected = s.expected.arr("conflicts").map { it as JsonObject }
    assertEquals(expected.size, actual.size, "${s.name}: número de conflictos")
    expected.zip(actual).forEach { (x, a) ->
        assertEquals(uuid(x.str("noteId")), a.noteId, "${s.name}: conflicto, nota")
        // De la ganadora solo cuentan fecha y dispositivo: la nota viva es ella y Core no guarda más.
        val w = x.obj("winner")
        assertEquals(date(w.str("updatedAt")).toInstant(), a.winner.updatedAt.toInstant(), "${s.name}: ganadora, fecha")
        assertEquals(w.str("deviceId"), a.winner.deviceId, "${s.name}: ganadora, dispositivo")
        val l = x.obj("losing")
        assertEquals(date(l.str("updatedAt")).toInstant(), a.losing.updatedAt.toInstant(), "${s.name}: perdedora, fecha")
        assertEquals(l.str("deviceId"), a.losing.deviceId, "${s.name}: perdedora, dispositivo")
        assertEquals(l.flag("tombstone"), a.losing.tombstone, "${s.name}: perdedora, borrado")
        assertEquals(l.text("text"), a.losing.note?.text, "${s.name}: perdedora, texto")
    }
}

private fun assertBases(s: Scenario, actual: Map<UUID, SyncBase>, now: OffsetDateTime) {
    val expected = s.expected.arr("basesAfter").map { it as JsonObject }
    assertEquals(expected.map { uuid(it.str("noteId")) }.toSet(), actual.keys, "${s.name}: ids de las bases")
    for (x in expected) {
        val base = actual.getValue(uuid(x.str("noteId")))
        assertTrue(sameDate(x.str("updatedAt"), base.updatedAt, now), "${s.name}: base, fecha ${x.str("updatedAt")} vs ${base.updatedAt}")
        assertEquals(x.str("deviceId"), base.deviceId, "${s.name}: base, dispositivo")
    }
}

private fun assertLocal(s: Scenario, local: InMemoryLocalStore, now: OffsetDateTime) {
    val notes = s.expected.arr("localAfter").map { it as JsonObject }
    assertEquals(notes.map { uuid(it.str("id")) }.toSet(), local.notes.keys, "${s.name}: notas locales")
    for (x in notes) {
        val item = local.notes.getValue(uuid(x.str("id")))
        assertNote(s, x, item.note, now, "local")
        assertEquals(x.text("authorDeviceId"), item.authorDeviceId, "${s.name}: autor de ${x.str("id")}")
    }
    val tombstones = s.expected.arr("localTombstonesAfter").map { it as JsonObject }
    assertEquals(tombstones.map { uuid(it.str("noteId")) }.toSet(), local.tombstones.keys, "${s.name}: lápidas locales")
    for (x in tombstones) {
        val t = local.tombstones.getValue(uuid(x.str("noteId")))
        assertTrue(sameDate(x.str("deletedAt"), t.deletedAt, now), "${s.name}: lápida, fecha")
        assertEquals(x.str("deviceId"), t.deviceId, "${s.name}: lápida, dispositivo")
    }
}

private fun assertRemote(s: Scenario, transport: InMemorySyncTransport, now: OffsetDateTime) {
    val expected = s.expected.arr("remoteAfter").map { it as JsonObject }
    val unreadable = expected.filter { it.flag("unreadable") }
    assertEquals(unreadable.size, transport.unreadable.size, "${s.name}: objetos ilegibles (deben quedar intactos)")
    val readable = expected.filter { !it.flag("unreadable") }
    assertEquals(readable.map { uuid(it.str("noteId")) }.toSet(), transport.store.keys, "${s.name}: objetos del almacén")
    for (x in readable) {
        val id = uuid(x.str("noteId"))
        val envelope = transport.store.getValue(id)
        assertEquals(x.flag("tombstone"), envelope.tombstone, "${s.name}: $id, borrado")
        assertTrue(sameDate(x.str("updatedAt"), envelope.updatedAt, now), "${s.name}: $id, fecha")
        assertEquals(x.str("deviceId"), envelope.deviceId, "${s.name}: $id, dispositivo")
        assertEquals(x.flag("rewritten"), id in transport.written, "${s.name}: $id, ¿lo escribió esta pasada?")
        if (envelope.tombstone) {
            assertEquals(x.flag("authentic"), SyncEnvelopeCodec.isAuthenticTombstone(envelope, s.key), "${s.name}: $id, firma")
        } else {
            val note = try {
                SyncEnvelopeCodec.decryptNote(envelope, s.key)
            } catch (e: java.security.GeneralSecurityException) {
                null
            }
            val xn = x["note"]
            if (xn.isNull()) assertEquals(null, note, "${s.name}: $id, no debe descifrar con esta clave")
            else assertNote(s, xn as JsonObject, note!!, now, "almacén")
        }
    }
}

private fun assertNote(s: Scenario, x: JsonObject, n: SyncNote, now: OffsetDateTime, where: String) {
    val tag = "${s.name}: nota ${x.str("id")} ($where)"
    assertEquals(x.str("text"), n.text, "$tag, texto")
    assertEquals(x.str("color"), n.color, "$tag, color")
    assertTrue(sameDate(x.str("createdAt"), n.createdAt, now), "$tag, createdAt")
    assertTrue(sameDate(x.str("updatedAt"), n.updatedAt, now), "$tag, updatedAt ${x.str("updatedAt")} vs ${n.updatedAt}")
    assertEquals((x.getValue("state") as JsonPrimitive).content.toInt(), n.state, "$tag, estado")
    assertEquals(x.str("screenOrigin"), n.screenOrigin, "$tag, screenOrigin")
    assertEquals(x.arr("tags").map { (it as JsonPrimitive).content }, n.tags, "$tag, etiquetas")
    assertEquals((x.getValue("isProtected") as JsonPrimitive).booleanOrNull, n.isProtected, "$tag, isProtected")
    val pc = x["protectedContent"]
    if (pc.isNull()) assertEquals(null, n.protectedContent, "$tag, protectedContent")
    else {
        val p = pc as JsonObject
        assertEquals(p.str("salt"), n.protectedContent?.salt, "$tag, salt")
        assertEquals(p.str("cipherText"), n.protectedContent?.cipherText, "$tag, protegido")
    }
    val dock = (x["dockPosition"] as? JsonPrimitive)?.takeIf { it !is JsonNull }?.doubleOrNull
    assertEquals(dock, n.dockPosition, "$tag, dockPosition")
}
