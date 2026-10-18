plugins {
    alias(libs.plugins.kotlin.jvm)
    `java-test-fixtures`
}

group = "app.aldune"
version = "0.1.0"

dependencies {
    // Las firmas públicas exponen tipos de los tres (Note, SyncInput, SyncNote…), de ahí `api`.
    api(project(":domain"))
    api(project(":sync"))
    api(project(":core"))

    // La batería del contrato la hereda `app` en sus pruebas; necesita JUnit y los tipos de domain en la API de los fixtures.
    testFixturesApi(project(":domain"))
    testFixturesApi(kotlin("test"))
    testFixturesApi(platform(libs.junit.bom))
    testFixturesApi(libs.junit.jupiter)

    // Solo para leer y escribir el JSON del contenido protegido con el mismo formato que core (ver SyncMapping.kt).
    implementation(libs.kotlinx.serialization.json)

    testImplementation(kotlin("test"))
    testImplementation(libs.okhttp.mockwebserver)
    testImplementation(platform(libs.junit.bom))
    testImplementation(libs.junit.jupiter)
    testRuntimeOnly(libs.junit.platform.launcher)
}

// Igual que android/core, sync, text y domain: se compila para Java 11 aunque corra en un JDK más nuevo, porque en
// Android solo existe lo que ofrece minSdk 26.
java {
    sourceCompatibility = JavaVersion.VERSION_11
    targetCompatibility = JavaVersion.VERSION_11
}

kotlin {
    compilerOptions {
        jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_11)
    }
}

tasks.withType<JavaCompile>().configureEach { options.release.set(11) }

tasks.test {
    useJUnitPlatform()
    testLogging { events("failed", "skipped"); showStandardStreams = false }
}
