rootProject.name = "aldune-sync"

pluginManagement {
    repositories {
        gradlePluginPortal()
        mavenCentral()
    }
}

dependencyResolutionManagement {
    repositories {
        mavenCentral()
    }
}

// Hasta que exista la app, este módulo es su propia compilación y trae android/core como build
// incluido: Gradle sustituye "app.aldune:aldune-core" por ese proyecto sin tocarlo.
includeBuild("../core")
