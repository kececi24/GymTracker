# GymTracker

GymTracker is an Android-first fitness planning and workout tracking application.

| Path | Contents |
| --- | --- |
| `backend/GymTracker.Api/` | ASP.NET Core API, EF Core migrations, and PostgreSQL configuration |
| `backend/GymTracker.Api.Tests/` | xUnit smoke tests for the API and OpenAPI contract |
| `mobile/` | Flutter Android client with Firebase authentication, Riverpod and Dio wiring, a navigation shell, and widget tests |
| `.github/workflows/ci.yml` | Build, test, migration rollback, Compose smoke, and Trivy checks |
| `docs/` | Course project charter, plan, requirements, and AI-use record |

## Start the backend

Install Docker with Compose v2. From the repository root, copy `.env.example` to `.env`, then run:

```sh
docker compose up --build -d
curl --fail --retry 15 --retry-all-errors http://localhost:8080/health
curl http://localhost:8080/openapi/v1.json
```

Compose starts PostgreSQL 17 and applies the initial EF Core migration before the API starts listening. The first migration creates an account identity table keyed by the Firebase UID. `/health` checks API liveness. Use `docker compose logs api` for startup errors and `docker compose down` to stop the services and retain the database volume.

To build and run the API smoke tests outside Docker, install the .NET 10 SDK:

```sh
dotnet test GymTracker.slnx
```

The CI workflow exercises the migration, rollback to an empty schema, and reapplication on a disposable PostgreSQL database. For EF CLI commands, run `dotnet tool restore`, set `ConnectionStrings__GymTracker` to a connection string for the target database, and pass `--project backend/GymTracker.Api` to `dotnet ef`.

## Run the app in Chrome

The Flutter client can be run in Chrome for quick UI development and debugging. From the repository root, use:

```sh
cd mobile
flutter pub get --enforce-lockfile
flutter run -d chrome --web-port=7357 --dart-define=API_BASE_URL=http://localhost:8080
```

In VS Code, you can instead open **Run and Debug**, select **GymTracker: Chrome**, and press **F5**. The fixed port lets the local API accept browser requests without opening CORS to arbitrary websites. Start the backend first if you want the API health check; the rest of the app can still be viewed when the backend is offline.

Firebase App Check uses its web debug provider in a debug build. If App Check enforcement is enabled in Firebase, copy the debug token printed in the Chrome developer console and register it under **App Check → Apps → Manage debug tokens**.

For a release web build, pass the reCAPTCHA v3 site key with `--dart-define=APP_CHECK_WEB_RECAPTCHA_SITE_KEY=your-site-key`.

## Start the Android app

Install Flutter **3.44.0**, JDK 17, Android SDK Platform 36, Android Build Tools 36.0.0, and Android NDK 28.2.13676358. Start an Android emulator.

Firebase client configuration is generated locally and is not committed. Install the Firebase and FlutterFire CLIs once, then run the configuration from `mobile/`:

```sh
npm install -g firebase-tools
firebase login
dart pub global activate flutterfire_cli

cd mobile
export PATH="$PATH:$HOME/.pub-cache/bin"
flutterfire configure \
  --project=gymtracker-dd0d6 \
  --platforms=android,web \
  --android-package-name=com.gymtracker.app \
  --yes
```

This writes `lib/firebase_options.dart` and `android/app/google-services.json`, both gitignored. For Google Sign-In, register the SHA-1 and SHA-256 fingerprints of every signing key (debug and release) on the Android app in Firebase.

Then run:

```sh
flutter pub get --enforce-lockfile
flutter analyze
flutter test
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:8080
```

`10.0.2.2` reaches the host from the standard Android emulator. Local HTTP is enabled in debug builds. The auth and navigation widget tests use a fake auth repository, so they need neither Firebase nor an API URL. Run `flutter build apk --debug` to build the same APK variant as CI.

### CI Firebase configuration

The CI build has no generated Firebase files. It uses inert placeholders from `mobile/tool/firebase/` by default so formatting, analysis, tests, and the debug build still run. To produce a CI APK that connects to the real Firebase project, add these repository secrets under **Settings → Secrets and variables → Actions**:

```sh
base64 -w0 mobile/lib/firebase_options.dart
base64 -w0 mobile/android/app/google-services.json
```

Store the outputs as `FIREBASE_OPTIONS_DART_B64` and `GOOGLE_SERVICES_JSON_B64`. The workflow uses them when present and falls back to the placeholders otherwise.

## Feature work

The scaffold includes the initial account schema, a public health endpoint, Firebase email/password and Google authentication, and the Home, Workouts, History, and Profile navigation shell. App Check, FCM, Drift storage, and the application workflows are implementation tasks tracked in the project plan. Flutter dependencies for those integrations are declared. WBS-1.4 owns Firebase project configuration; the provisional Android application ID `com.gymtracker.app` is registered in the `gymtracker-dd0d6` project and must be confirmed before Play Store publication, because it cannot change afterwards. WBS-8.1 owns AI provider selection.

