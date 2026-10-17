rootProject.name = "aldune-android"

pluginManagement {
    repositories {
        // Android y AndroidX solo existen en Google Maven; el filtro evita que el resto se busque ahí (y F-Droid lo agradece).
        google {
            content {
                includeGroupByRegex("com.android.*")
                includeGroupByRegex("com.google.*")
                includeGroupByRegex("androidx.*")
            }
        }
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google {
            content {
                includeGroupByRegex("com.android.*")
                includeGroupByRegex("com.google.*")
                includeGroupByRegex("androidx.*")
            }
        }
        mavenCentral()
    }
}

// Los módulos puros (core, sync, text) se prueban sin Android; app solo pega piezas y pinta (spec de Android, §3).
include(":core", ":sync", ":text", ":app")
