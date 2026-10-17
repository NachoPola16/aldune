plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
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
    }

    buildTypes {
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

dependencies {
    implementation(project(":text"))

    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.material3)
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.core.ktx)
}
