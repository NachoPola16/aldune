plugins {
    kotlin("jvm") version "2.1.10"
    kotlin("plugin.serialization") version "2.1.10"
}

group = "app.aldune"
version = "0.1.0"

dependencies {
    implementation("app.aldune:aldune-core")
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.8.0")

    testImplementation(platform("org.junit:junit-bom:5.11.4"))
    testImplementation("org.junit.jupiter:junit-jupiter")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
}

// Igual que android/core: se compila para Java 11 aunque corra en un JDK más nuevo, porque en
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
    systemProperty("aldune.vectors.dir", rootDir.resolve("../../tests/vectors").canonicalPath)
    testLogging { events("passed", "failed", "skipped"); showStandardStreams = false }
}
