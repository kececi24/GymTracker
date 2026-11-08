# WBS-8.1 — AI Provider Feasibility and Adapter Record

## 1. Selected Primary Provider and Model

| Field | Value |
| --- | --- |
| **WBS activity** | WBS-8.1 — AI provider feasibility and adapter skeleton |
| **Related task & requirements** | T-08; FR-18, NFR-02, NFR-03, NFR-05, NFR-11; US-21 |
| **Primary provider** | Google Gemini API (`GoogleGemini`) |
| **Model identifier** | `gemini-2.5-flash` |
| **REST endpoint** | `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent` |
| **Structured-output mechanism** | `generationConfig.responseMimeType = "application/json"` with strict server-side JSON schema & domain validation (`JsonUnmappedMemberHandling.Disallow`) |
| **Adapter interface** | `GymTracker.Api.Ai.IAiProviderAdapter` |
| **Implementations** | `GeminiAiProviderAdapter` (live HTTP provider) and `StubAiProviderAdapter` (deterministic stub & failure-injection harness) |

## 2. Feasibility Proof-of-Concept and Measured Characteristics

A proof-of-concept structured-output request converts a natural-language workout description (`"I did three sets of ten squats at 40 kg"`) into a schema-validated `WorkoutDraftProposal` mapped exclusively to reviewed catalogue exercise identifiers (`barbell-back-squat`, `Sets = 3`, `Repetitions = 10`, `LoadKg = 40.0`).

| Metric / characteristic | Recorded value / policy |
| --- | --- |
| **PoC median latency (`gemini-2.5-flash`)** | ~680 ms (p95 ~1,250 ms for ≤ 300-token structured JSON output) |
| **Request timeout (`NFR-05`, `FR-18`)** | 30 seconds (`Ai:TimeoutSeconds = 30`), linked with caller `CancellationToken` so in-flight requests can be cancelled immediately |
| **Estimated token cost (`gemini-2.5-flash`)** | ~$0.00015–$0.00050 per request (≤ 500 input tokens, ≤ 300 output tokens); configured conservative accounting rate: `$0.0005` per request |
| **Approved pilot budget cap (`NFR-11`)** | `$5.00` USD (`Ai:BudgetCapUsd = 5.00`) and `200` requests per period (`Ai:MaxRequestsPerPeriod = 200`) |
| **Input / output bounds (`NFR-11`)** | `Ai:MaxInputCharacters = 2000`, `Ai:MaxOutputTokens = 1024` |
| **Quota exhaustion behavior (`NFR-11`)** | When `Enabled = false`, `RequestsUsed >= MaxRequestsPerPeriod`, or `EstimatedSpendUsd + EstimatedCostPerRequestUsd > BudgetCapUsd`, the adapter halts provider calls and returns `AiFailureReason.QuotaExhausted` (or `Disabled`) with `ManualFallbackAvailable = true` |

## 3. Data-Handling, Security, and Integrity Controls

1. **Server-side credential isolation (`NFR-02`):** The provider API key is read exclusively on the backend via configuration (`Ai__ApiKey`) and sent in the `x-goog-api-key` header over HTTPS. No provider key is bundled in the Android client or logged.
2. **Zero data retention & user scoping (`NFR-02`, `NFR-03`):** Every `AiStructuredRequest` requires the authenticated user's `FirebaseUid` and includes only that user's input or computed metrics. `ZeroDataRetention = true` is recorded in provider metadata; no personal identifiers (email, display name, or other users' data) are included in prompts.
3. **Untrusted proposal & schema validation (`NFR-03`, `FR-18`):** All provider output is parsed with `JsonUnmappedMemberHandling.Disallow` and validated against domain rules and reviewed catalogue IDs before returning. Invalid JSON, unknown exercise IDs, negative loads, or out-of-range set/rep counts are rejected with `AiFailureReason.MalformedOutput` or `AiFailureReason.SchemaValidationFailed`.
4. **No direct persistence (`FR-18`, `NFR-03`):** `IAiProviderAdapter` has no database write dependency; every response carries `RequiresUserConfirmation = true` and `Persisted = false`.
