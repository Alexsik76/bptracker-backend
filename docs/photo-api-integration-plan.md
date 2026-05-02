# Plan: Photo API Integration (Revised)

This plan describes how to integrate the `photo-api` service into the BP Tracker application to collect a labelled dataset for machine learning.

## Goals
- Forward photos of blood pressure monitors to `photo-api` along with metadata.
- Track user corrections (Gemini suggestion vs. final saved values).
- Ensure "best-effort" delivery: BP Tracker must work even if `photo-api` is unavailable.
- Maintain security: `photo-api` credentials stay on the backend.
- **Zero regressions:** Existing endpoints and tests must remain unchanged.

## Architecture

### Data Flow
1. **Analyze:** User takes/uploads a photo. Frontend calls `POST /api/v1/measurements/analyze`. Backend returns Gemini's suggestion.
2. **State:** Frontend stores the original `File` (blob) and Gemini's suggestions (`sys`, `dia`, `pulse`) in local memory.
3. **Save:** User submits the form. 
   - **Case A (With Photo):** Frontend calls `POST /api/v1/measurements/with-photo` using `multipart/form-data`.
   - **Case B (No Photo):** Frontend calls the existing `POST /api/v1/measurements` with JSON (unchanged).
4. **Forward:** If `with-photo` is called, the backend:
   - Saves the measurement to the DB normally.
   - Asynchronously (fire-and-forget via `_ = _photoApi.UploadAsync(...)`) forwards the photo + metadata to `photo-api`.

## Backend Changes

### 1. New Service: `PhotoApiService`
Handles communication with the external `photo-api`.
- **Interface:** `IPhotoApiService` with `Task UploadAsync(...)`. Returns `Task` to allow fire-and-forget at the call site without `async void` risks.
- **Implementation:** Uses `IHttpClientFactory` to get a client.
- **Safety:** The background task MUST NOT capture scoped services (like `AppDbContext`). It should only use `IHttpClientFactory` and `ILogger`.
- **Metadata:**
  - `timestamp`: Taken from `Measurement.RecordedAt` (ISO 8601 with TZ).
  - `corrected_by_user`: Comparison between `CreateMeasurementDto` values and `GeminiResult` values.
  - `device_model`: From `PHOTO_API_DEVICE_MODEL`.
  - `notes` and `quality_flags`: Intentionally left `null` in this iteration.

### 2. New Endpoint
- `POST /api/v1/measurements/with-photo`
- **Rate Limiting:** Apply `analyze` policy (10 req/min per user).
- **Security:** Requires authorization.
- Accepts `multipart/form-data`:
  - `image`: The file.
  - `sys`, `dia`, `pulse`: User-confirmed values.
  - `geminiSys`, `geminiDia`, `geminiPulse`: Original values from AI.

### 3. Configuration & Validation
New settings class `PhotoApiSettings`.
- **Startup Check:** If `PHOTO_API_ENABLED=true`, validate that `PHOTO_API_URL` and `PHOTO_API_TOKEN` are present. Throw `OptionsValidationException` if missing.
- **Environment Variables:**
  | Variable | Default | Required if Enabled |
  |----------|---------|---------------------|
  | `PHOTO_API_ENABLED` | `false` | No |
  | `PHOTO_API_URL` | - | **Yes** |
  | `PHOTO_API_TOKEN` | - | **Yes** |
  | `PHOTO_API_DEVICE_MODEL` | `Paramed Expert-X` | No |

## Frontend Changes

### 1. Image Preprocessing
Before sending a photo to `/analyze` or `/with-photo`, the frontend will:
- Resize the image to **max 1024px** on the longer side (preserving aspect ratio).
- Re-encode as **JPEG at 0.85 quality**.
- This is implemented using a `<canvas>` element.
- The same compressed `Blob` is used for both the initial analysis and the final save.

### 2. `App` Class (`js/app.js`)
- Add `_preprocessImage(file)` method to handle resizing and compression.
- Update `_analyzeAndFill(file)`:
  - Call `_preprocessImage(file)` first.
  - Store the resulting `Blob` in `this._lastAnalysis.file`.
  - Send the same `Blob` to `api.analyzeImage`.
- In `handleFormSubmit(e)`:
  - If `this._lastAnalysis` exists, construct `FormData` and call `api.addMeasurementWithPhoto(formData)`.
  - Else, call existing `api.addMeasurement(data)`.

## Testing Strategy

### 1. Unit Tests (`BpTracker.Api.Tests/Services/PhotoApiServiceTests.cs`)
- Mock `HttpMessageHandler` to verify:
  - `corrected_by_user` logic (true vs false).
  - No HTTP call when `PHOTO_API_ENABLED=false`.
  - Errors from `photo-api` are caught and logged, not re-thrown.
  - Correct headers (Bearer token) and JSON structure.

### 2. Integration Tests (`BpTracker.Api.Tests/Measurements/WithPhotoTests.cs`)
- Verify `POST /api/v1/measurements/with-photo`:
  - **201 Created:** Saves to DB and returns data when valid.
  - **401 Unauthorized:** Fails without a valid session.
  - **400/422 Unprocessable Content:** Fails on invalid `sys`/`dia`/`pulse` values.
  - **429 Too Many Requests:** Verifies rate limiting is applied.

## Failure Handling
- Log errors on backend.
- Do not fail the user's request if `photo-api` is down.
- Ensure the background task doesn't cause memory leaks or app crashes.
