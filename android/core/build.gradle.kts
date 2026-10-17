plugins {
    kotlin("jvm") version "2.1.10"
    kotlin("plugin.serialization") version "2.1.10"
}

group = "app.aldune"
version = "0.1.0"

dependencies {
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.8.0")

    testImplementation(platform("org.junit:junit-bom:5.11.4"))
    testImplementation("org.junit.jupiter:junit-jupiter")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
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
    systemProperty("aldune.vectors.dir", rootDir.resolve("../../tests/vectors").canonicalPath)
    testLogging { events("passed", "failed", "skipped"); showStandardStreams = false }
}
