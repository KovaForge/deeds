# Good Deeds Tracker

Good Deeds Tracker lets parents record good and bad deeds, award or deduct points, and translate those points into dollars. The solution is designed to stay within the free tiers of Azure Static Web Apps and Neon Postgres while running entirely in C#.

## Solution Overview

| Layer | Technology | Purpose |
|-------|------------|---------|
| Front-end | Blazor WebAssembly | Rich SPA written in C# that runs in the browser |
| Back-end | Azure Functions (.NET isolated) | Serverless API for parents, children, deeds, redemptions, and exports |
| Database | Neon Postgres | Durable storage for all domain entities |
| Hosting | Azure Static Web Apps | Serves the Blazor client and Functions API together |
| Dev Environment | GitHub Codespaces | Cloud-based development workspace |

### Key Features

- Parent onboarding via email with local storage of the linked parent id
- CRUD for children with custom dollar-per-point rates and live balances
- Deed type library supporting positive and negative scoring
- Child detail view to log deeds, review history, and delete mistakes
- Optional ChatGPT integration to score deeds using a stored API key
- Redemption tracking with automatic balance recalculation
- CSV export of each child history for audits or reports
- Responsive UI themed for family use

## Repository Layout

```
good-deeds/
├── api/           # Azure Functions (.NET isolated)
└── app/           # Blazor WebAssembly client
```

### API Project (`api/`)

- `Program.cs` boots the Functions host, binds configuration, and ensures the database schema.
- `Data.cs` centralizes Dapper helpers for CRUD operations and CSV export logic.
- Function classes grouped by domain (`ParentsFunctions`, `ChildrenFunctions`, `DeedsFunctions`, etc.) expose HTTP endpoints.

### Client Project (`app/`)

- `Pages/` contains the dashboard, children manager, deed types, settings, and child profile views.
- `Services/ApiClient.cs` wraps all calls to the Functions API.
- `Services/UserSettingsService.cs` stores the parent id and ChatGPT key in browser local storage.
- `Services/ChatGptService.cs` calls OpenAI when configured.

## Running Locally

### Prerequisites

- .NET 8 SDK
- Azure Functions Core Tools v4
- Postgres connection string (Neon free tier recommended)

### Codespaces quick start

1. Verify tooling: `dotnet --info` and `func --version` in the Codespaces terminal.
2. From repo root run `./dev.sh` (or `./dev.ps1` in PowerShell) to start both the Functions host and Blazor dev server.
3. Codespaces forwards the key ports automatically:
	- API: 7071 (`http://localhost:7071/api/health`)
	- Blazor dev server: 7032 (https) and 5269 (http)
4. Stop both with `Ctrl+C` in the terminal.

### Configure Environment

1. Copy `api/local.settings.json` and set the `DB` value under `Values` to your Postgres connection string.
2. Ensure the database user has permission to create tables (schema is generated automatically).

### Quick Start (watch both projects)

- Bash: `./dev.sh`
- PowerShell: `./dev.ps1`

The scripts start the Functions host (`func start`) and the Blazor dev server (`dotnet watch run`) together. Stop with `Ctrl+C` in the foreground terminal.

### Start the Functions API

```bash
cd api
func start
```

You should see the health endpoint at `http://localhost:7071/api/health`.

### Start the Blazor Client

In a second terminal:

```bash
cd app
dotnet watch run
```

The dev server defaults to `https://localhost:7032` (and `http://localhost:5269`). The client expects the API at `http://localhost:7071/api/`; update `app/wwwroot/appsettings.json` if you change ports. CORS is enabled in the Functions host for the local Blazor ports.

## First-Time Use

1. Visit the **Settings** page and enter a parent email. The API will create or re-link the parent and cache the id locally.
2. Optional: paste an OpenAI API key to enable the **Ask ChatGPT for points** button on the child detail page. Keys stay in browser local storage only.
3. Head to **Children** to add child profiles, then **Deed Types** to define reusable positive or negative deeds.
4. Open a child profile to log deeds, delete mistakes, trigger redemptions, and view balance history.

## REST API Summary

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/health` | Ping endpoint for uptime checks |
| POST | `/api/parents` | Create parent by email |
| GET | `/api/parents?email=` | Find parent by email |
| GET | `/api/parents/{id}` | Retrieve parent by id |
| POST | `/api/parents/{parentId}/children` | Create child |
| GET | `/api/parents/{parentId}/children` | List children for parent |
| GET | `/api/children/{childId}` | Get child detail |
| PUT | `/api/children/{childId}` | Update child name or rate |
| DELETE | `/api/parents/{parentId}/children/{childId}` | Remove child (cascades deeds and redemptions) |
| GET | `/api/children/{childId}/balance` | Current points and dollar balance |
| POST | `/api/parents/{parentId}/deed-types` | Create deed type |
| GET | `/api/parents/{parentId}/deed-types` | List deed types |
| DELETE | `/api/parents/{parentId}/deed-types/{deedTypeId}` | Remove deed type |
| POST | `/api/deeds` | Log a deed for a child |
| GET | `/api/children/{childId}/deeds` | List deeds for a child |
| DELETE | `/api/children/{childId}/deeds/{deedId}` | Delete a deed |
| POST | `/api/redemptions` | Redeem points |
| GET | `/api/children/{childId}/redemptions` | List redemptions for a child |
| GET | `/api/children/{childId}/export/csv` | Download deed and redemption history as CSV |

Responses use JSON unless noted. Errors follow `{ "error": "message" }`.

## Deployment Notes

1. Provision a Neon Postgres database and copy the connection string into the Azure Functions configuration (`DB`).
2. Deploy the Azure Functions project (zip deploy or GitHub Actions) and run once to ensure schema creation.
3. Configure Azure Static Web Apps to build the Blazor client and Functions API (`app_location: app`, `api_location: api`).
4. Store secrets in Azure (OpenAI key usage is optional and client-side only; avoid hard-coding).

## Future Enhancements

- Authentication integration (Azure Static Web Apps or custom identity)
- Role-based admin dashboard and audit logging UI
- PDF export and richer reporting
- Automated tests covering API endpoints and Razor components

## License

MIT License. See `LICENSE` for details.
