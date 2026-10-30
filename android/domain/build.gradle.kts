plugins {
    alias(libs.plugins.kotlin.jvm)
}

group = "app.aldune"
version = "0.1.0"

dependencies {
    // Las reglas de texto, color, orden y búsqueda salen de :text; aquí no se reimplementan.
    // `api` porque la interfaz pública de domain expone sus tipos (NoteTheme, NoteTone…).
    api(project(":text"))
    // El manifiesto de la copia automática (AutoExport.kt) es JSON.
    implementation(libs.kotlinx.serialization.json)

    testImplementation(kotlin("test"))
    testImplementation(platform(libs.junit.bom))
    testImplementation(libs.junit.jupiter)
    testRuntimeOnly(libs.junit.platform.launcher)
}

// Igual que android/core, sync y text: se compila para Java 11 aunque corra en un JDK más nuevo, porque en
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
