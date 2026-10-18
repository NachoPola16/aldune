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

// Los módulos puros (core, sync, text, domain, syncclient) se prueban sin Android; app solo pega piezas y pinta (spec de Android, §3).
include(":core", ":sync", ":text", ":domain", ":syncclient")

// :app y los plugins de Android (AGP, KSP, Room, que solo existen en Google Maven) se configuran únicamente si hay SDK. Sin él (una sesión
// de nube, un CI de solo JVM) ni siquiera se podría resolver el plugin, y los módulos puros dejarían de poder probarse. Con SDK nada cambia.
val hasAndroidSdk: Boolean = run {
    val fromEnv = System.getenv("ANDROID_HOME") ?: System.getenv("ANDROID_SDK_ROOT")
    val localProperties = file("local.properties")
    val fromFile = localProperties.takeIf { it.isFile }?.readLines()?.any { it.trim().startsWith("sdk.dir=") } == true
    !fromEnv.isNullOrBlank() || fromFile
}
if (hasAndroidSdk) include(":app") else rootProject.buildFileName = "build.jvm.gradle.kts"
