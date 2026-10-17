plugins {
    alias(libs.plugins.kotlin.jvm)
    alias(libs.plugins.kotlin.serialization)
}

group = "app.aldune"
version = "0.1.0"

dependencies {
    implementation(libs.kotlinx.serialization.json)

    testImplementation(platform(libs.junit.bom))
    testImplementation(libs.junit.jupiter)
    testRuntimeOnly(libs.junit.platform.launcher)
}

// Se compila para Java 11 aunque corra en un JDK más nuevo: en Android solo existe lo que
// ofrece minSdk 26 (java.time sí, java.util.Base64 sí, javax.crypto con PBKDF2WithHmacSHA256 sí).
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
    // Los vectores son el contrato del formato y viven fuera de este módulo, compartidos con C#.
    systemProperty("aldune.vectors.dir", projectDir.resolve("../../tests/vectors").canonicalPath)
    testLogging { events("passed", "failed", "skipped"); showStandardStreams = false }
}
