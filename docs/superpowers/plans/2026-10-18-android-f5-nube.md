# Prompt para la sesión de nube — F5, parte JVM (`domain` + `syncclient`)

> Para pegar en Claude Code en la web (claude.ai/code), repo `NachoPola16/aldune`, rama nueva desde `main`. Modelo: **Sonnet, esfuerzo medio**.
> Tope de gasto ≈10 $ (créditos que caducan el 2026-11-05). Antes de lanzarlo, la spec tiene que estar subida a `main`.

---

Trabajas en el repositorio de Aldune (app de notas; cliente Android en Kotlin bajo `android/`). Tu tarea es la **parte de la fase F5 que es Kotlin/JVM puro**,
con TDD, sin nada que dependa de Android ni dibuje nada.

## Lee primero (en este orden)

1. `AGENTS.md` (reglas del proyecto; los comentarios van **en español** y explican el porqué).
2. `docs/superpowers/specs/2026-10-18-aldune-android-f5-design.md`: **es tu especificación**. Haz §3 y §4 enteros, nada de §5.
3. `docs/superpowers/specs/2026-10-17-aldune-android-design.md` §1 y §3.1 (reglas del motor).
4. El código: `android/sync/src/main/kotlin/app/aldune/sync/` (`SyncTypes.kt`, `SyncPlanner.kt`, `SyncExecutor.kt`, `InMemoryDoubles.kt`, los transportes),
   `android/core/.../SyncModels.kt` y `ProfileCodes.kt`, y `android/domain/` entero (sobre todo `Note.kt`, `NoteStore.kt`, `NoteService.kt` y sus tests, para
   copiar el estilo).

## Identidad de git (antes del primer commit, obligatorio)

```bash
git config user.name "NachoPola16"
git config user.email "nachopolac@gmail.com"
```

Ningún commit puede llevar `Co-Authored-By`, «Generated with Claude» ni nada parecido, y el autor y el committer tienen que ser esa identidad. Antes de cada push:
`git log --format='%an <%ae> | %cn <%ce>' origin/main..HEAD` solo puede mostrar `NachoPola16 <nachopolac@gmail.com>`.

## Entorno

- JDK 21. Desde `android/`: `./gradlew` (el wrapper baja Gradle 9.6.0, fijado por hash).
- **No hay SDK de Android.** Ejecuta solo los módulos JVM:
  `./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test`.
  Si la configuración de `:app` falla por falta del SDK, haz que `android/settings.gradle.kts` incluya `:app` solo cuando exista el SDK (`ANDROID_HOME`, o
  `sdk.dir` en `local.properties`), con un comentario que lo explique. **No toques nada más de `:app`.**
- Línea base **antes de cambiar nada**: apunta el número de tests de cada módulo (deben ser core 82, sync 119, text 100, domain 113). Al acabar tienen que seguir
  todos, más los nuevos.
- La suite regenera `tests/vectors/kotlin-output.json`: **descártalo** (`git checkout -- tests/vectors/kotlin-output.json`) antes de cada commit. No toques nada más
  de `tests/vectors/`.

## Qué hacer (en este orden; un commit por paso, con mensaje en español)

1. **`domain`** (spec §3): `isProtected` y `protectedContentJson` en `Note`, y las reglas de `NoteService` para las protegidas. Test primero.
2. **Módulo `android/syncclient`** (paquete `app.aldune.syncclient`), con el mismo `build.gradle.kts` que `domain` (Java 11, JUnit 5), dependencias
   `:domain`, `:sync`, `:core` y el plugin `java-test-fixtures`. Inclúyelo en `settings.gradle.kts`.
3. Spec §4.1: `buildSyncInput` y `toDomainNote`, con generación y validación del `deviceId`.
4. Spec §4.2: `LocalSyncStore`, `LocalSnapshot`, `LocalCommit`, `CommitReport`, `InMemoryLocalSyncStore` y la batería abstracta `LocalSyncStoreContract` en
   `src/testFixtures/kotlin`, más su subclase concreta en `src/test` que la ejecuta con `InMemoryLocalSyncStore`. La concurrencia optimista es la parte que importa:
   pruébala con una edición hecha entre la foto y el commit.
5. Spec §4.3: `runSync` en dos fases, con los casos de fallo de la fase 1.
6. Spec §4.4: `SyncTriggerPolicy`, con reloj inyectado y una tabla de pruebas que cubra cada fila.
7. Spec §4.5: `classifyFailure` (con pruebas que usen `HttpSyncTransport` y `WebDavSyncTransport` reales contra MockWebServer, como ya hace `android/sync`) y
   `formatStatus` / `RelativeTime`. **Solo claves y números, nada de texto de interfaz.**
8. Spec §4.7 y §4.8: `parseProfileInput`, `validateCredentials` y `firstConnectDecision`. Para los códigos de prueba, constrúyelos con `ProfileCodes.encode`.
9. **Extremo a extremo** (spec §6, punto 2): todos los casos de esa lista en un mismo fichero de pruebas, con `NoteService` + `InMemoryLocalSyncStore` + el
   transporte en memoria de `android/sync`.
10. `docs/INFORME-NUBE-F5.md`: qué se hizo, cuántos tests hay por módulo (antes → después), decisiones que tomaste donde la spec no decía nada y qué quedó sin hacer.

## Límites (no los cruces)

- **No modifiques `android/core`, `android/text` ni `tests/vectors/`.** De `android/sync` solo se permite la excepción tipada de la spec §4.6, si y solo si
  clasificar por mensaje no basta; los mismos mensajes, y los 119 tests verdes.
- No cambies el formato de sync ni regeneres vectores. Si un caso parece contradecir lo que hace el motor, **el motor manda**: anótalo en el informe y no lo
  «arregles».
- Nada de Android, Compose, Room ni WorkManager. Nada de dependencias nuevas salvo las que ya estén en `android/gradle/libs.versions.toml`; si necesitas otra, anótalo
  en el informe y no la añadas.
- No escribas texto de interfaz (las cadenas van en `android/strings.tsv` en otra fase).
- Comentarios en español con el porqué, con la densidad de `domain`. Sin BOM en los `.kt` (los demás `.kt` del repo no lo llevan; compruébalo con
  `head -c3` en uno existente y haz lo mismo).

## Criterio de parada

Para y haz push de la rama en cuanto se cumpla **una** de estas condiciones:

- **Hecho:** los pasos 1-10 completos y `./gradlew :core:test :sync:test :text:test :domain:test :syncclient:test` verde, con los 414 de antes intactos.
- **Presupuesto:** llevas unos 10 $ o te quedan pasos que no vas a terminar con calidad. Deja comiteado lo que esté verde y escribe en el informe hasta dónde
  llegaste y qué falta. Mejor 7 pasos bien que 10 a medias.
- **Bloqueo:** algo de la spec es contradictorio o imposible sin cruzar los límites. No improvises un rodeo: anótalo en el informe con lo que has visto y para.

No abras un PR ni fusiones nada: solo push de la rama. La revisión y la fusión las hace el usuario en local.
