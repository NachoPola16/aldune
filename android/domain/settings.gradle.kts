rootProject.name = "aldune-domain"

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

// Hasta que exista la app, este módulo es su propia compilación y trae android/text como build
// incluido (igual que android/sync con core): Gradle sustituye "app.aldune:aldune-text" por ese proyecto.
includeBuild("../text")
