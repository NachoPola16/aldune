plugins {
    alias(libs.plugins.kotlin.jvm)
    alias(libs.plugins.kotlin.serialization)
}

group = "app.aldune"
version = "0.1.0"

dependencies {
    // Para leer y escribir `aspectColors` (AspectColorsJson) y, en los tests, los vectores; no depende de Android.
    implementation(libs.kotlinx.serialization.json)

    testImplementation(platform(libs.junit.bom))
    testImplementation(libs.junit.jupiter)
    testRuntimeOnly(libs.junit.platform.launcher)
}

// Igual que android/core y android/sync: se compila para Java 11 aunque corra en un JDK más nuevo, porque en
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
    // Los vectores son el contrato y viven fuera del módulo, compartidos con C#.
    systemProperty("aldune.vectors.dir", projectDir.resolve("../../tests/vectors").canonicalPath)
    testLogging { events("failed", "skipped"); showStandardStreams = false }
}
