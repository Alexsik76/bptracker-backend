# BP Tracker — Backend

REST API для системи відстеження артеріального тиску. Виступає також проксі до Gemini AI для розпізнавання знімків тонометра.

## Стек

- **.NET 10** Minimal APIs
- **PostgreSQL 16** + Entity Framework Core (Npgsql)
- **Scalar UI** — інтерактивна документація
- **Docker + Docker Compose** — розгортання
- **Gemini AI** — OCR знімків тонометра

## Структура проекту

```
├── Data/               AppDbContext, міграції EF Core
├── Models/             Measurement, TreatmentSchema, GeminiSettings, GoogleSheetsSettings
├── DTOs/               MeasurementDto, CreateMeasurementDto, ImageAnalysisResultDto
├── Services/           IMeasurementService, ISchemaService, IGeminiService та реалізації
├── Endpoints/          MeasurementEndpoints, SchemaEndpoints, SyncEndpoint, AnalyzeEndpoints
├── Program.cs          Startup, DI, CORS, Rate Limiting, автоміграції
└── Dockerfile          Multi-stage build (sdk → publish → runtime)
```

## API Endpoints

### Measurements
| Метод | URL | Опис |
|---|---|---|
| `GET` | `/api/measurements` | Останні 30 вимірювань |
| `POST` | `/api/measurements` | Додати вимірювання `{sys, dia, pulse}` |
| `DELETE` | `/api/measurements/{id}` | Видалити вимірювання |
| `POST` | `/api/measurements/analyze` | OCR фото тонометра → `{sys, dia, pulse}` |

### Schemas
| Метод | URL | Опис |
|---|---|---|
| `GET` | `/api/schemas/active` | Активна схема лікування |

### Sync
| Метод | URL | Опис |
|---|---|---|
| `POST` | `/api/sync/google-sheets` | Синхронізація з Google Sheets |

Інтерактивна документація: `https://api-bptracker.home.vn.ua/scalar/v1`

## Змінні оточення

| Змінна | Обов'язкова | За замовчуванням | Опис |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | Так | — | PostgreSQL connection string |
| `GEMINI_API_KEY` | Так | — | API ключ Google Gemini |
| `GEMINI_MODEL` | Ні | `gemini-flash-latest` | Назва моделі Gemini |
| `GOOGLE_SCRIPT_URL` | Ні | — | URL Google Apps Script для синхронізації |
| `CORS_ORIGINS` | Ні | `https://bptracker.home.vn.ua` | Дозволені origins через кому |

## Безпека

- **Cloudflare WAF** — доступ обмежено по IP (лише домашня мережа)
- **CORS** — дозволено лише `bptracker.home.vn.ua` (конфігурується через `CORS_ORIGINS`)
- **Rate limiting** — `/api/measurements/analyze` обмежено до 10 запитів/хвилину per IP

## Валідація даних

PostgreSQL CHECK constraints + валідація в сервісному шарі:
- Систолічний: 40–300
- Діастолічний: 20–200
- Пульс: 30–250

## Локальна розробка

**Потрібно:** .NET 10 SDK, Docker, `dotnet-ef` (`dotnet tool install -g dotnet-ef`)

```bash
# Запустити БД
docker-compose up db -d

# Запустити API (міграції застосуються автоматично)
GEMINI_API_KEY=your_key dotnet run
```

```bash
# Нова міграція після зміни моделей
dotnet ef migrations add <Name>
```

## Docker розгортання

```bash
docker-compose up --build
```

API доступне на порту `5000` (внутрішньо `8080`). PostgreSQL на `5436`.
