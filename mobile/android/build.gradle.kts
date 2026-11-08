allprojects {
    repositories {
        google()
        mavenCentral()
    }
}

val flutterBuildDir = rootProject.layout.projectDirectory.dir("../build")
rootProject.layout.buildDirectory.set(flutterBuildDir)
subprojects {
    layout.buildDirectory.set(flutterBuildDir.dir(name))
}
subprojects {
    evaluationDependsOn(":app")
}

tasks.register<Delete>("clean") {
    delete(rootProject.layout.buildDirectory)
}
