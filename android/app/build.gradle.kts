plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.ksp)
    alias(libs.plugins.androidx.room)
}

android {
    // El paquete del código (app.aldune) y el applicationId son independientes: el id es permanente y lo eligió el usuario
    // (spec de Android §8.1); el namespace puede seguir siendo el del código.
    namespace = "app.aldune"
    compileSdk = 37

    defaultConfig {
        applicationId = "io.github.nachopola16.aldune"
        minSdk = 26
        // 36 a propósito aunque el móvil tenga la 37: subirlo activa cambios de comportamiento que no hace falta absorber ahora.
        targetSdk = 36
        versionCode = 1
        versionName = "0.1.0"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildTypes {
        debug {
            // Las pruebas instrumentadas desinstalan la app al acabar. Con un id propio para depuración, nunca tocan una instalación
            // «de verdad» con notas reales en el mismo móvil.
            applicationIdSuffix = ".debug"
        }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }

    buildFeatures { compose = true }
}

// El esquema de Room se exporta y se versiona: cada versión lleva su migración probada (spec de Android §4.1).
room {
    schemaDirectory("$projectDir/schemas")
}

dependencies {
    implementation(project(":text"))
    implementation(project(":domain"))
    implementation(libs.androidx.room.runtime)
    ksp(libs.androidx.room.compiler)

    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.core.ktx)
}

// Las pruebas instrumentadas comparten el esquema exportado para probar migraciones.
android.sourceSets.getByName("androidTest").assets.directories.add("$projectDir/schemas")

dependencies {
    androidTestImplementation(libs.androidx.test.ext.junit)
    androidTestImplementation(libs.androidx.test.runner)
    androidTestImplementation(libs.androidx.room.testing)
    // MigrationTestHelper lee el esquema exportado con kotlinx-serialization 1.8+, pero AGP alinea las pruebas con el classpath de la app,
    // donde lifecycle fija el 1.7.3 y da AbstractMethodError. Solo en debug: la versión de producción no lo lleva.
    debugImplementation(libs.kotlinx.serialization.json)
    androidTestImplementation(libs.junit4)
}
