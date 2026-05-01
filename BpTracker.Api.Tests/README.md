# BpTracker.Api.Tests

Integration test suite for the BpTracker backend API.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- **Docker Desktop** (running) — tests spin up a `postgres:16` container via Testcontainers

## Run

From this directory:

```bash
dotnet test
```

Or from the solution root (`bptracker-backend/`), specifying the solution explicitly:

```bash
dotnet test backend.sln
```

## Notes

- No external services needed — email and Gemini are replaced with in-memory fakes
- No environment variables needed — the test database runs in a temporary container
- Each test class gets its own isolated Postgres container
