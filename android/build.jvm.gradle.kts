// Raíz sin SDK de Android: solo los plugins de los módulos JVM puros. Ver `hasAndroidSdk` en settings.gradle.kts;
// con SDK se usa build.gradle.kts, que declara además AGP, KSP y Room para :app.
plugins {
    alias(libs.plugins.kotlin.jvm) apply false
    alias(libs.plugins.kotlin.serialization) apply false
}
