package app.aldune.syncclient

import app.aldune.domain.InMemoryNoteStore

/** La batería del contrato con el almacén en memoria. La de Room la ejecuta `app` en el dispositivo. */
class InMemoryLocalSyncStoreTest : LocalSyncStoreContract() {
    override fun newHarness(): StoreHarness {
        val notes = InMemoryNoteStore()
        return object : StoreHarness {
            override val sync: LocalSyncStore = InMemoryLocalSyncStore(notes)
            override val notes = notes
        }
    }
}
