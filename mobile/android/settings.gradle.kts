pluginManagement {
    val properties = java.util.Properties()
    file("local.properties").inputStream().use { properties.load(it) }
    val flutterSdkPath = requireNotNull(properties.getProperty("flutter.sdk"))

    includeBuild("$flutterSdkPath/packages/flutter_tools/gradle")

    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

plugins {
    id("org.jetbrains.kotlin.android") version "2.4.21" apply false
    id("dev.flutter.flutter-plugin-loader") version "1.0.0"
    id("com.android.application") version "8.13.2" apply false
    // START: FlutterFire Configuration
    id("com.google.gms.google-services") version("4.3.15") apply false
    // END: FlutterFire Configuration
}

include(":app")
