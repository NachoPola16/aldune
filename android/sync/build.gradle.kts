plugins {
    alias(libs.plugins.kotlin.jvm)
    alias(libs.plugins.kotlin.serialization)
}

group = "app.aldune"
version = "0.1.0"

dependencies {
    implementation(project(":core"))
    implementation(libs.kotlinx.serialization.json)
    implementation(libs.okhttp)

    testImplementation(libs.okhttp.mockwebserver)

    testImplementation(platform(libs.junit.bom))
    testImplementation(libs.junit.jupiter)
    testRuntimeOnly(libs.junit.platform.launcher)
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
    systemProperty("aldune.vectors.dir", projectDir.resolve("../../tests/vectors").canonicalPath)
    // La prueba contra Aldune.SyncServer real (ServerIntegrationTest) solo corre si se le dice dónde está:
    //   gradle test -Daldune.server.url=http://127.0.0.1:5099/ -Daldune.server.token=...
    // El JVM de los tests no hereda los -D de Gradle, así que se reenvían aquí.
    for (name in listOf("aldune.server.url", "aldune.server.token")) System.getProperty(name)?.let { systemProperty(name, it) }
    testLogging { events("passed", "failed", "skipped"); showStandardStreams = false }
}
