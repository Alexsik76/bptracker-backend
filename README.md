# BP Tracker API

This is the backend for the Blood Pressure & Treatment System, built with C# .NET 10 Minimal APIs and PostgreSQL.

## 🚀 Live API Documentation
The interactive API documentation (Scalar UI) is available at:
[https://api-bptracker.home.vn.ua/scalar/v1](https://api-bptracker.home.vn.ua/scalar/v1)

## 🛠 Tech Stack
- **Framework:** .NET 10 (Minimal APIs)
- **Database:** PostgreSQL 16
- **ORM:** Entity Framework Core (with Npgsql)
- **API Docs:** Scalar API Reference (via Microsoft.AspNetCore.OpenApi)
- **Containerization:** Docker & Docker Compose

## 📂 Project Structure
```text
backend/
├── Data/               # AppDbContext & Migrations
├── Models/             # Domain Entities (Measurement, TreatmentSchema)
├── DTOs/               # Data Transfer Objects
├── Services/           # Business Logic (IMeasurementService, ISchemaService)
├── Endpoints/          # API Route Definitions
├── Program.cs          # App Startup & Dependency Injection
└── Dockerfile          # Multi-stage build for production
```

## ⚙️ Local Development

### Prerequisites
- .NET 10 SDK
- Docker Desktop / Docker Engine
- `dotnet-ef` global tool (`dotnet tool install --global dotnet-ef`)

### Setup
1. **Clone the repository.**
2. **Run the database:**
   ```bash
   docker-compose up db -d
   ```
3. **Run the API:**
   ```bash
   dotnet run
   ```
   The API will automatically apply migrations to the database on startup.

### Database Migrations
To add a new migration after changing models:
```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## 🔌 API Endpoints

### Measurements
- **`GET /api/measurements`**
  - **Description:** Returns the last 30 measurements.
  - **Response:** `200 OK`
    ```json
    [
      {
        "id": "uuid",
        "recordedAt": "2024-04-17T12:00:00Z",
        "sys": 120,
        "dia": 80,
        "pulse": 70
      }
    ]
    ```

- **`POST /api/measurements`**
  - **Description:** Adds a new measurement.
  - **Request Body:**
    ```json
    {
      "sys": 120,
      "dia": 80,
      "pulse": 70
    }
    ```
  - **Response:** `201 Created` with the created object.

### Treatment Schemas
- **`GET /api/schemas/active`**
  - **Description:** Returns the currently active treatment schedule.
  - **Response:** `200 OK`
    ```json
    {
      "id": "Schema_1",
      "isActive": true,
      "scheduleDocument": {
        "08:00": "Lisinopril 10mg",
        "20:00": "Amlodipine 5mg"
      }
    }
    ```
  - **Response:** `404 Not Found` if no active schema exists.

## 🐳 Docker Deployment
To build and run the entire stack (API + DB):
```bash
docker-compose up --build
```
The API will be exposed on port `5000` (mapped to `8080` internally).

## 🔒 Data Integrity
The system enforces data integrity at the database level using PostgreSQL `CHECK` constraints for:
- **Systolic:** 40 - 300
- **Diastolic:** 20 - 200
- **Pulse:** 30 - 250
