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

- `Program.cs` boots the Functions host, normalizes the Postgres connection string, and auto-applies SQL migrations from `api/sql` at startup.
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

1. Visit the **Quick Start** page to create a parent, add a child, add a deed type, log a deed, redeem points, and view balance from one screen.
2. Optional: add your OpenAI API key in **Settings** to enable the **Ask AI to adjust points** button on the child detail page.
3. Head to **Children** to manage children, then **Deed Types** to define reusable deeds.
4. Open a child profile to log deeds, delete mistakes, trigger redemptions, and view balance history.

## Microsoft Sign-In

The client uses Azure Static Web Apps authentication endpoints (`/.auth/login/aad` and `/.auth/me`) to sign in and link a parent account by email. Configure Microsoft identity in your Static Web Apps resource and ensure the app registration allows the audience you want (work/school and optional personal Microsoft accounts).

Required redirect URI:

- `https://<your-app>.azurestaticapps.net/.auth/login/aad/callback`

For local development, use the Static Web Apps CLI (`swa start`) if you need to test sign-in; otherwise the sign-in buttons will not return a user locally.

## AI Key Storage

Each user can save their own OpenAI API key in **Settings**. Keys are encrypted server-side using `AI_KEY_ENCRYPTION_KEY` and stored per parent account. Set `AI_KEY_ENCRYPTION_KEY` to a base64-encoded 32-byte value in the Functions app configuration.

## Linking Accounts

If you sign in with Microsoft using an email address that differs from the parent email you previously stored in the app, open **Settings** and use **Link existing account** to map your Microsoft identity to the existing parent email.

## REST API Summary

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/health` | Ping endpoint for uptime checks |
| POST | `/api/parents` | Create or re-link a parent by email |
| POST | `/api/children` | Create a child (requires `x-parent-id` or `parentId` in body) |
| POST | `/api/deed-types` | Create a deed type for the parent |
| POST | `/api/deeds` | Log a deed; resolves points from the deed type when not provided |
| POST | `/api/redemptions` | Redeem points for a child (fails if insufficient) |
| GET | `/api/balances/{childId}` | Current points and dollar balance for a child |

Additional helper routes remain available for lookups and exports (e.g., `GET /api/parents?email=`, `GET /api/parents/{parentId}/children`, `GET /api/children/{childId}/deeds`, `GET /api/children/{childId}/export/csv`).

Responses use JSON unless noted. Errors follow `{ "error": "message" }`.

### Curl examples

Set a base URL first (defaults shown assume local Functions):

```bash
BASE="http://localhost:7071/api"
```

Create or load a parent by email (returns 201 for new, 200 if existing):

```bash
curl -i -X POST "$BASE/parents" \
	-H "Content-Type: application/json" \
	-d '{"email":"parent@example.com"}'
```

Create a child:

```bash
PARENT_ID="<parent-guid>"
curl -i -X POST "$BASE/children" \
	-H "Content-Type: application/json" \
	-H "x-parent-id: $PARENT_ID" \
	-d '{"parentId":"'$PARENT_ID'","name":"Sam","dollarPerPoint":1.5}'
```

Create a deed type:

```bash
curl -i -X POST "$BASE/deed-types" \
	-H "Content-Type: application/json" \
	-H "x-parent-id: $PARENT_ID" \
	-d '{"parentId":"'$PARENT_ID'","name":"Wash dishes","points":3}'
```

Log a deed (points optional; falls back to deed type points):

```bash
CHILD_ID="<child-guid>"
DEED_TYPE_ID="<deed-type-guid>"
curl -i -X POST "$BASE/deeds" \
	-H "Content-Type: application/json" \
	-H "x-parent-id: $PARENT_ID" \
	-d '{"parentId":"'$PARENT_ID'","childId":"'$CHILD_ID'","deedTypeId":"'$DEED_TYPE_ID'","note":"Evening chores"}'
```

Redeem points (fails with 409 if balance too low):

```bash
curl -i -X POST "$BASE/redemptions" \
	-H "Content-Type: application/json" \
	-H "x-parent-id: $PARENT_ID" \
	-d '{"parentId":"'$PARENT_ID'","childId":"'$CHILD_ID'","points":5,"description":"Treat"}'
```

Get balance:

```bash
curl -i -X GET "$BASE/balances/$CHILD_ID" -H "x-parent-id: $PARENT_ID"
```

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
