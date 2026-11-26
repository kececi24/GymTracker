# GymTracker API Reference

This reference describes the backend endpoints for health and schema discovery, identity and profiles, catalogue and templates, programs and versions, workout records, and AI previews and planning. Paths are relative to the API host, for example `http://localhost:8080` with Docker Compose. The final section lists planned APIs.

## Health and API discovery

| Method and path | Purpose | Authentication | Success response |
| --- | --- | --- | --- |
| `GET /health` | Check whether the API process is running. This does not check database or AI-provider readiness. | Public | `200`: `{ "status": "ok" }`. |
| `GET /openapi/v1.json` | Retrieve the generated OpenAPI document with registered paths and request/response schemas. | Public | `200`: OpenAPI JSON. |

## Authentication and conventions

Every `/api/...` endpoint requires a Firebase ID token. The two public endpoints above do not require authentication. For JSON write requests, send:

```http
Authorization: Bearer <firebase-id-token>
Content-Type: application/json
```

The backend derives ownership from the token. Clients do not supply an owner UID in write requests. Owner-specific lists return only the caller's records; the catalogue and templates are shared. Program and workout resource operations return `403` when the resource exists but belongs to someone else, and `404` when it does not exist. Profile routes require the path UID to match the token UID. Missing, invalid, and expired tokens return `401`.

Program, version, workout, and scheduled session IDs are UUIDs. Exercise and template IDs are catalogue strings such as `barbell-back-squat` and `full-body-foundation`. Version numbers are integers starting at 1. JSON property names use camel case.

## Identity, profiles, and onboarding

| Method and path | Purpose | Success response |
| --- | --- | --- |
| `POST /api/auth/session` | Verify the supplied Firebase token and initialize or load the caller's account. No request body is required. | `200`: user profile. |
| `GET /api/users/{uid}/profile` | Load the caller's profile; initialize a default account if it does not exist. | `200`: user profile. |
| `PUT /api/users/{uid}/profile` | Replace editable profile fields. | `200`: updated user profile. |
| `PUT /api/users/{uid}/onboarding` | Save profile fields and set `onboardingCompleted` to `true`. | `200`: updated user profile. |

Sign-in happens through Firebase on the client. `POST /api/auth/session` does not accept an email/password, issue a new token, or create a server session cookie. Continue sending the Firebase ID token with subsequent requests. Re-establishing a session refreshes the stored email/provider metadata without clearing profile fields.

Both profile write routes accept this shape:

```json
{
  "displayName": "Can",
  "age": 21,
  "heightCm": 180,
  "weightKg": 78,
  "experienceLevel": "Intermediate",
  "primaryGoal": "Strength",
  "availableEquipment": "Barbell, dumbbells",
  "displayTimezone": "Europe/Istanbul"
}
```

| Field | Current validation |
| --- | --- |
| `displayName` | Required; trimmed value must be at most 120 characters. |
| `age` | `null` or an integer from 16 to 120. |
| `heightCm` | `null` or a number greater than 0 and at most 300. |
| `weightKg` | `null` or a number greater than 0 and at most 500. |
| `experienceLevel` | Optional for profile updates; required for onboarding. Allowed values: `Beginner`, `Intermediate`, `Advanced`, compared without case and normalized in responses. |
| `primaryGoal` | Optional for profile updates; non-blank for onboarding. Stored as trimmed text. |
| `availableEquipment` | Optional trimmed text. |
| `displayTimezone` | Blank or omitted values default to `UTC`; otherwise trimmed and limited to 64 characters. Timezone identifiers are not currently validated against a timezone database. |

Profile updates replace the optional fields too; send their desired values rather than treating the request as a partial patch. A normal profile update preserves the onboarding flag. Profile responses contain `firebaseUid`, `email`, `authProvider`, all eight editable fields above, `onboardingCompleted`, `createdAtUtc`, and `updatedAtUtc`. New default accounts have an empty display name, `UTC` timezone, and `onboardingCompleted: false`.

Invalid profile/onboarding fields return `400`. A path UID different from the authenticated UID returns `403`, even if no account exists for that path UID.

## Catalogue and templates

| Method and path | Purpose | Success response |
| --- | --- | --- |
| `GET /api/exercises` | Browse reviewed exercises, sorted by primary muscle group and then name. | `200`: exercise array. |
| `GET /api/exercises?muscleGroup=Legs` | Filter by primary muscle group; comparison ignores case and trims surrounding whitespace. | `200`: matching exercise array, or `[]`. |
| `GET /api/program-templates` | Retrieve predefined programs, sorted by template ID. | `200`: template array with ordered exercise prescriptions. |

An exercise contains `id`, `name`, `primaryMuscleGroup`, `category`, `defaultEquipment`, and `description`. A template contains `id`, `name`, `description`, `recommendedExperienceLevel`, and `exercises`. Template exercises use the snapshot exercise fields described below.

## Programs and immutable versions

| Method and path | Purpose | Success response |
| --- | --- | --- |
| `GET /api/programs` | List the caller's active programs, most recently updated first. | `200`: program summary array, or `[]`. |
| `POST /api/programs` | Create a program from explicit exercises or a template; creates version 1. | `201`: program detail; `Location: /api/programs/{id}`. |
| `GET /api/programs/{programId}` | Read an active program with its current snapshot and version headers. | `200`: program detail. |
| `PUT /api/programs/{programId}` | Save the supplied name, description, and complete exercise list as a new version. | `200`: updated program detail. |
| `GET /api/programs/{programId}/versions/{versionNumber}` | Read an owned snapshot by version number. | `200`: version snapshot. |
| `GET /api/program-versions/{versionId}` | Read an owned snapshot directly by UUID. | `200`: version snapshot. |
| `DELETE /api/programs/{programId}` | Archive the program while retaining its snapshots. | `204`: no body. |

### Create and update requests

Create an explicit program with `POST /api/programs`:

```json
{
  "name": "Strength routine",
  "description": "Squat and push-up session",
  "templateId": null,
  "exercises": [
    {
      "exerciseId": "barbell-back-squat",
      "targetSets": 3,
      "targetReps": 10,
      "targetLoadKg": 40,
      "restSeconds": 90
    },
    {
      "exerciseId": "push-up",
      "targetSets": 3,
      "targetReps": 15,
      "targetLoadKg": null,
      "restSeconds": 60
    }
  ]
}
```

For a template-based create request, use:

```json
{
  "name": "My foundation program",
  "description": null,
  "templateId": "full-body-foundation",
  "exercises": null
}
```

On creation, a supplied template must exist. Missing or blank names/descriptions fall back to template values; a missing or empty exercise list falls back to the template's exercises. A non-empty exercise list overrides the template prescriptions.

For `PUT`, send the explicit-program request shape with the complete desired exercise list. Updates do not resolve template defaults; `templateId` is ignored and the original `sourceTemplateId` remains unchanged. A successful update always creates a new immutable version, even if the supplied content matches the current version.

### Program validation

| Field | Rule |
| --- | --- |
| `name` | Required after template resolution and trimming; at most 120 characters. |
| `exercises` | Between 1 and 30 entries after template resolution. |
| `exerciseId` | Must identify a reviewed catalogue exercise; surrounding whitespace is trimmed. |
| `targetSets` | Integer from 1 to 50. |
| `targetReps` | Integer from 1 to 500. |
| `targetLoadKg` | `null` or a numeric value from 0 to 1000 kilograms. |
| `restSeconds` | `null` or an integer from 0 to 3600 seconds. |

Invalid program inputs return `400`. Rejected creates do not save a program; rejected edits do not advance its version.

### Program response fields

A program summary contains `id`, `ownerUid`, `name`, `description`, `sourceTemplateId`, `currentVersionNumber`, `currentVersionId`, `createdAtUtc`, and `updatedAtUtc`.

A program detail adds:

- `currentVersion`: the current immutable snapshot.
- `versions`: headers ordered by version number, each containing `versionId`, `versionNumber`, `name`, and `createdAtUtc`.

A version snapshot contains `versionId`, `programId`, `versionNumber`, `name`, `description`, `createdAtUtc`, and `exercises`. Each snapshot exercise contains `exerciseId`, `exerciseName`, `primaryMuscleGroup`, `category`, `displayOrder`, `targetSets`, `targetReps`, `targetLoadKg`, and `restSeconds`. Exercise order follows the input list, starting at `displayOrder: 1`.

After archival, the program disappears from the active list. Its regular detail, update, and delete endpoints return `404`, but both version lookup endpoints remain available to its owner. Existing workout references continue to resolve to their original snapshots.

## Workout records

| Method and path | Purpose | Success response |
| --- | --- | --- |
| `GET /api/workouts` | List the caller's workouts by performed time descending, then ID ascending. | `200`: workout array, or `[]`. |
| `POST /api/workouts` | Create a workout and its ordered set list together. | `201`: workout; `Location: /api/workouts/{id}`. |
| `GET /api/workouts/{workoutId}` | Read a workout and all of its sets. | `200`: workout. |
| `PUT /api/workouts/{workoutId}` | Replace performed time, program-version reference, and the entire ordered set list. | `200`: updated workout. |
| `DELETE /api/workouts/{workoutId}` | Permanently delete the workout and its sets. | `204`: no body. |

The list endpoint currently returns all matching records without pagination or date filters. Sets are managed through the workout request; there are no separate set routes.

### Create and update requests

Both `POST` and `PUT` accept:

```json
{
  "performedAtUtc": "2026-11-09T18:00:00Z",
  "programVersionId": null,
  "sets": [
    { "exerciseId": "barbell-back-squat", "repetitions": 10, "loadKg": 40 },
    { "exerciseId": "barbell-back-squat", "repetitions": 8, "loadKg": 45 },
    { "exerciseId": "push-up", "repetitions": 15, "loadKg": null }
  ]
}
```

Each array entry represents one performed set. Repeated exercise IDs are valid. The response preserves array order. `loadKg: null` represents a bodyweight set; zero is also a valid numeric load.

To associate a workout with a program, replace `programVersionId: null` with the program detail's `currentVersionId` or another owned version UUID. The version must exist and belong to the caller. An archived program's retained versions are also accepted. The workout may contain exercises or loads different from its program prescription.

`PUT` is a full replacement: include every set that should remain. Omitted sets are deleted, additional entries are added, and `programVersionId: null` removes the program reference. At least one set must remain. Editing or archiving a program does not automatically change a workout's reference; an explicit workout update can select a different owned version.

### Workout validation and persistence

| Field | Rule |
| --- | --- |
| `performedAtUtc` | Required, non-default timestamp. Send ISO 8601 with `Z` or an explicit offset. The backend normalizes it to UTC. |
| `programVersionId` | Optional UUID. Unknown version: `400`; another owner's version: `403`. |
| `sets` | Required array with 1–1500 entries; entries must not be null. |
| `exerciseId` | Required catalogue ID; surrounding whitespace is trimmed. |
| `repetitions` | Integer from 1 to 500. |
| `loadKg` | `null` or a numeric value from 0 to 1000 with at most two decimal places. |

Loads are already in kilograms; there is no unit-conversion parameter. A string such as `"40 lb"` is rejected with `400`.

The backend validates the full request before changing the workout. Workout and set writes commit in one database transaction; failed writes roll back together. A failed update preserves the previous performed time, program reference, and sets. Creating a workout also creates a local account record if one does not exist for the authenticated UID. These endpoints do not implement operation-ID replay or offline synchronization.

### Workout response example

```json
{
  "id": "616aef50-680d-468b-8052-715ed3f4954a",
  "performedAtUtc": "2026-11-09T18:00:00+00:00",
  "programVersionId": null,
  "sets": [
    { "exerciseId": "barbell-back-squat", "repetitions": 10, "loadKg": 40 },
    { "exerciseId": "barbell-back-squat", "repetitions": 8, "loadKg": 45 },
    { "exerciseId": "push-up", "repetitions": 15, "loadKg": null }
  ]
}
```

Workout responses expose `id`, `performedAtUtc`, `programVersionId`, and `sets`. Internal set IDs, owner UID, and audit timestamps are not included.

## AI provider, workout previews, and planning

## Scheduled sessions

| Method and path | Purpose | Success response |
| --- | --- | --- |
| `POST /api/scheduled-sessions` | Schedule a session in the caller's display timezone. | `201` with the session and its resource path in `Location`. |
| `PUT /api/scheduled-sessions/{sessionId}` | Move an owned active session using the caller's current display timezone. | `200` with the updated session. |
| `DELETE /api/scheduled-sessions/{sessionId}` | Mark an owned session cancelled. | `200` with the saved session and its original cancellation timestamp on repeated requests. |

Create and move requests take a local date and time such as `{"localStart":"2028-03-27T18:30:00"}`. Supply hours and minutes, with optional seconds and up to six fractional digits. The API obtains the timezone from the caller's profile and stores it with the converted UTC instant. A caller creating their first session without a local account starts with the default `UTC` timezone.

The response contains `id`, `localStart` as a datetime without an offset, `startAtUtc`, `displayTimezone`, and `cancelledAtUtc` (initially `null`). Cancellation preserves the scheduled time and timezone. Invalid time formats, offsets in the input, unrecognized profile timezones, and local times skipped or repeated during a clock change return `400`. Moving a cancelled session returns `409`. For move and cancel, a session owned by someone else returns `403` and a missing session returns `404`.

## AI provider and workout draft previews

| Method and path | Purpose | Success response |
| --- | --- | --- |
| `GET /api/ai/provider` | Read configured provider metadata and current cost-control counters. | `200`: provider metadata. |
| `POST /api/ai/workout-draft-preview` | Convert a natural-language description into a validated, unpersisted workout proposal. | `200`: draft preview. |
| `POST /api/ai/goal-suggestion` | Suggest a goal using the signed-in user's saved goal and equipment preferences. | `200`: goal proposal. |
| `POST /api/ai/program-proposal` | Suggest a program using the signed-in user's goal, experience, and available equipment. | `200`: program proposal. |
| `POST /api/ai/progress-insight` | Explain the signed-in user's recent training metrics. | `200`: progress explanation. |

The metadata response contains `provider`, `modelId`, `supportsStructuredOutput`, `timeoutSeconds`, `maxInputCharacters`, `maxOutputTokens`, `maxRequestsPerPeriod`, `budgetCapUsd`, `estimatedCostPerRequestUsd`, `zeroDataRetention`, `enabled`, `requestsUsed`, and `estimatedSpendUsd`. These describe configuration and local accounting, rather than provider billing or a successful live-provider health check.

The backend returns fixed sample proposals through the stub adapter by default (`Ai:UseStub` is `true`) or when the API key is missing. Live generation requires `Ai:UseStub` set to `false` and an API key configured on the server.

### Preview request and response

Send `POST /api/ai/workout-draft-preview`:

```json
{
  "descriptionText": "I did three sets of ten squats at 40 kg"
}
```

The success response contains `originalText`, `proposal`, `requiresUserConfirmation: true`, `persisted: false`, `provider`, `modelId`, and `latencyMs`. For example, the proposal portion has this structure:

```json
{
  "summary": "Three sets of squats",
  "sets": [
    { "exerciseId": "barbell-back-squat", "sets": 3, "repetitions": 10, "loadKg": 40 }
  ]
}
```

The adapter validates the summary, catalogue exercise IDs, grouped set counts (1–50), repetitions (1–500), and external loads (0–1000 kg). Write external loads with units such as `40 kg` or `100 lb`. The API checks the proposed loads against the quantities in the description and accepts pounds converted to kilograms to two decimal places. The metadata endpoint reports the configured input limit and timeout.

The preview never saves a workout. Let the user review and edit it first. To save an approved draft, expand each grouped entry into individual sets for `POST /api/workouts`, supply the performed timestamp and optional program-version reference, and satisfy the workout validation rules above. For example, an AI entry with `sets: 3` becomes three workout set entries.

### Planning requests and responses

The three planning endpoints require authentication and accept an empty body. Save a primary goal in the profile before requesting a goal suggestion. Program proposals also require an experience level. Progress insights compare sessions, resistance volume, and bodyweight repetitions across the latest 30 days and the preceding 30 days. They require at least one workout in those 60 days.

Each response has `proposal`, `requiresUserConfirmation`, `persisted` (always `false`), `provider`, `modelId`, and `latencyMs`. The proposal shape depends on the endpoint.

| Endpoint | Proposal fields | Review and save |
| --- | --- | --- |
| Goal suggestion | `primaryGoal` | Review and edit the suggestion before updating the profile with `PUT /api/users/{uid}/profile`. |
| Program proposal | `name`, `description`, `exercises` (the same fields as a program write) | Review and edit the proposal before submitting `POST /api/programs`. |
| Progress insight | `explanation` | Display for review. `requiresUserConfirmation` is `false`. |

### AI failures

| Status | `failureReason` | Meaning |
| --- | --- | --- |
| `400` | `InputTooLarge` | Description exceeds the configured character limit. |
| `422` | `MalformedOutput` or `SchemaValidationFailed` | Provider output failed validation, workout input was incomplete, or the planning request lacked required data. |
| `429` | `QuotaExhausted` or `Disabled` | A local or provider limit was reached, or AI is disabled. |
| `504` | `Timeout` | Adapter timeout. |
| `503` | Other adapter failure reasons, such as `ProviderUnavailable` or `Cancelled` | The adapter could not complete the request. Cancellation may also end the client connection before a response can be received. |

Workout preview failures return this shape, preserving the user's input for manual correction. Planning failures return the same fields except `originalText`.

```json
{
  "error": "Prompt text cannot be empty. Please provide input or use manual entry.",
  "failureReason": "SchemaValidationFailed",
  "originalText": "",
  "manualFallbackAvailable": true
}
```

Authentication and JSON-binding errors can occur before the adapter and use the common behavior described below.

## Common error responses

| Status | Meaning |
| --- | --- |
| `400 Bad Request` | Invalid input, unknown exercise/template, or an unknown program-version reference in a workout write. |
| `401 Unauthorized` | Missing, expired, or invalid authentication token. |
| `403 Forbidden` | Access to another user's resource or reference to their program version. |
| `404 Not Found` | Requested resource/version is missing, or an active-program operation targets an archived program. |
| `409 Conflict` | A move targets a cancelled scheduled session. |
| `422 Unprocessable Entity` | AI input/output schema validation failed; see preview failures above. |
| `429 Too Many Requests` | AI disabled or local quota/spending limit reached. |
| `503 Service Unavailable` | AI adapter/provider failure. |
| `504 Gateway Timeout` | AI adapter timeout. |
| `500 Internal Server Error` | Unexpected server/database failure; workout writes are rolled back. |

Program, workout, and profile application validation/ownership errors use this shape. Authentication failures also use an `error` field:

```json
{ "error": "Workout not found." }
```

Malformed JSON and type-binding errors are handled by ASP.NET Core, so clients should not assume every error response uses that shape. Unexpected server failures likewise do not have a guaranteed JSON error body.

## Typical client flow

1. Sign in with Firebase on the client, then call `POST /api/auth/session` with the ID token.
2. Read the returned profile and complete onboarding if needed.
3. Fetch exercises or program templates, then create a program and retain its `id` and `currentVersionId`.
4. Record a workout referencing that version UUID, or use `null` for an independent workout. Optionally start with an AI preview and confirm the edited proposal before saving.
5. Retrieve history through `GET /api/workouts` and use the workout ID for edits or deletion.
6. When the program changes, use its new version for future workouts. Retrieve an older workout's snapshot through `GET /api/program-versions/{versionId}`.

## Planned APIs

The following project-plan areas do not currently have registered endpoints. Route names, request bodies, and response contracts are deferred until implementation:

- Calendar session listings, reminder preferences, and notification delivery.
- Operation-ID replay and offline synchronization endpoints.
- Progress dashboard and personal-record APIs.
- Dedicated goal management.
- Friend connections and private workout-summary sharing/revocation.
- Optional badges/quotes, group chat, joint workouts, and public program sharing.

This list describes planned scope, not working API capabilities. See the [project plan](Project_Plan.md) and [requirements specification](Requirements_Specification.md) for ownership and acceptance criteria.

## Source and verification

- [Application route registration and health endpoint](../backend/GymTracker.Api/Program.cs)
- [Identity and profile endpoint implementation](../backend/GymTracker.Api/Endpoints/IdentityAndProfileEndpoints.cs)
- [Program endpoint implementation](../backend/GymTracker.Api/Endpoints/ProgramEndpoints.cs)
- [Workout endpoint implementation](../backend/GymTracker.Api/Endpoints/WorkoutEndpoints.cs)
- [Scheduling endpoint implementation](../backend/GymTracker.Api/Endpoints/SchedulingEndpoints.cs)
- [Workout schema and constraints](../backend/GymTracker.Api/Data/WorkoutRecord.cs)
- [AI endpoint implementation](../backend/GymTracker.Api/Endpoints/AiAdapterEndpoints.cs)
- [AI adapter configuration and contracts](../backend/GymTracker.Api/Ai/AiProviderAdapter.cs)
- [Identity and profile integration tests](../backend/GymTracker.Api.Tests/IdentityAndProfileTests.cs)
- [Program integration tests](../backend/GymTracker.Api.Tests/ProgramEndpointsTests.cs)
- [Workout integration tests](../backend/GymTracker.Api.Tests/WorkoutEndpointsTests.cs)
- [Scheduling integration tests](../backend/GymTracker.Api.Tests/SchedulingEndpointsTests.cs)
- [Health and OpenAPI tests](../backend/GymTracker.Api.Tests/FoundationTests.cs)
- [AI adapter tests](../backend/GymTracker.Api.Tests/AiProviderAdapterTests.cs)

The tests cover health/schema discovery, identity and onboarding, authentication, ownership, program snapshots, workout/set replacement, validation, UTC normalization, AI adapter behavior, and rollback after a database write failure. AI test responses use stubs rather than live model calls.
