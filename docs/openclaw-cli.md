# OpenClaw CLI

Command-line interface for managing the Good Deeds Tracker API. Operates as a parent — authenticating via Bearer token or Azure SWA auth — and managing all children, deeds, deed types, redeem types, redemptions, and balances under that single parent.

## Installation

```bash
npm install -g @briarforge/cli
# or run directly:
node apps/cli/dist/index.js
```

## Configuration

The CLI stores config in `~/.briarforge/config.json`. You can also set environment variables:

| Env variable | Description |
|-------------|-------------|
| `OPENCLAW_BASE_URL` | API base URL (default: `http://localhost:7071/api`) |
| `OPENCLAW_TOKEN` | Bearer token for authentication |
| `OPENCLAW_PARENT_ID` | Parent ID (used when token not present) |

## Setup

```bash
openclaw auth login --token <token> [--base-url <url>] [--parent-id <id>]
```

The CLI stores the token and base URL locally. Future commands pick up credentials automatically.

## Authentication

The CLI supports two auth mechanisms (tried in order):

1. **Bearer token** — `Authorization: Bearer gd_pat_xxx`. Tokens are created via `openclaw tokens create`.
2. **Azure SWA auth** — via `x-ms-client-principal` header (local dev / deployed SWA auth).

All API routes accept Bearer token as the primary auth path; the SWA path is a fallback.

---

## Commands

### Auth

#### `openclaw auth login --token <token> [--base-url <url>]`

Store credentials locally for future commands.

```bash
openclaw auth login --token gd_pat_abc123 --base-url https://myapp.azurewebsites.net/api
```

---

### CLI Tokens

Manage personal access tokens for CLI authentication.

#### `openclaw tokens create --label <label> [--days-valid <days>]`

Create a new CLI token. Returns the raw token — **save it now, it won't be shown again**.

```bash
openclaw tokens create --label " laptop"
openclaw tokens create --label "CI runner" --days-valid 90
```

#### `openclaw tokens list`

List all active tokens (last-used, expiry). Does not return the raw token.

```bash
openclaw tokens list
```

#### `openclaw tokens revoke --id <token-id>`

Revoke a token immediately.

```bash
openclaw tokens revoke --id 550e8400-e29b-41d4-a716-446655440000
```

---

### Children

#### `openclaw children list`

List all children for the authenticated parent.

```bash
openclaw children list
```

#### `openclaw children create --name <name> [--dollar-per-point <number>]`

Add a new child. Defaults `$` per point to `1.00`.

```bash
openclaw children create --name "Sam" --dollar-per-point 1.50
```

#### `openclaw children update --id <child-id> [--name <name>] [--dollar-per-point <number>]`

Update an existing child's name or rate.

```bash
openclaw children update --id <child-id> --name "Samuel" --dollar-per-point 2.00
```

#### `openclaw children delete --id <child-id>`

Remove a child and all their deeds/redemptions.

```bash
openclaw children delete --id <child-id>
```

---

### Deed Types

#### `openclaw deed-types list`

List all deed types for the authenticated parent.

```bash
openclaw deed-types list
```

#### `openclaw deed-types create --name <name> --points <number>`

Create a new deed type. Points are positive (good deeds) or negative (corrections).

```bash
openclaw deed-types create --name "Helped with dishes" --points 3
openclaw deed-types create --name "Backtalk" --points -2
```

#### `openclaw deed-types update --id <deed-type-id> [--name <name>] [--points <n>] [--active true|false]`

Update or deactivate a deed type.

```bash
openclaw deed-types update --id <deed-type-id> --points 5
openclaw deed-types update --id <deed-type-id> --active false
```

#### `openclaw deed-types delete --id <deed-type-id>`

Delete a deed type. Fails if any deeds reference it.

```bash
openclaw deed-types delete --id <deed-type-id>
```

---

### Deeds

#### `openclaw deeds list --child-id <child-id>`

List all deeds for a specific child.

```bash
openclaw deeds list --child-id <child-id>
```

#### `openclaw deeds create --child-id <child-id> --deed-type-id <deed-type-id> [--points <n>] [--note <text>] [--occurred-at <ISO date>] [--created-by <text>]`

Log a deed. If points are omitted, the deed type's default is used.

```bash
openclaw deeds create --child-id <child-id> --deed-type-id <deed-type-id> --note "Morning chore"
openclaw deeds create --child-id <child-id> --deed-type-id <deed-type-id> --occurred-at "2025-01-15T08:00:00Z"
```

#### `openclaw deeds delete --id <deed-id>`

Delete a logged deed. Balance recalculates automatically.

```bash
openclaw deeds delete --id <deed-id>
```

---

### Redeem Types

#### `openclaw redeem-types list`

```bash
openclaw redeem-types list
```

#### `openclaw redeem-types create --name <name> --points <number>`

Create a redemption reward type.

```bash
openclaw redeem-types create --name "Screen time" --points 10
openclaw redeem-types create --name "Ice cream" --points 15
```

---

### Redemptions

#### `openclaw redemptions list --child-id <child-id>`

List redemptions for a child.

```bash
openclaw redemptions list --child-id <child-id>
```

#### `openclaw redemptions create --child-id <child-id> --points <n> [--redeem-type-id <id>] [--description <text>]`

Redeem points. Fails if insufficient balance (409 Conflict).

```bash
openclaw redemptions create --child-id <child-id> --points 5 --description "Treat"
```

---

### Balances

#### `openclaw balances list`

List all children with their current point and dollar balances.

```bash
openclaw balances list
```

#### `openclaw balances get --child-id <child-id>`

Get balance for a specific child.

```bash
openclaw balances get --child-id <child-id>
```

---

### History

#### `openclaw history --child-id <child-id>`

Full transaction history (deeds and redemptions) for a child, in chronological order.

```bash
openclaw history --child-id <child-id>
```

---

### Parent

#### `openclaw parent me`

Get the currently authenticated parent account.

```bash
openclaw parent me
```

---

## Token Format

`gd_pat_` prefix identifies a Good Deeds PAT. Stored as SHA-256 hash in the database; raw token shown once at creation.

---

## Curl Examples

```bash
BASE="http://localhost:7071/api"
TOKEN="gd_pat_xxx"

# Create a deed type
curl -X POST "$BASE/deed-types" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"Washing dishes","points":3}'

# Log a deed
curl -X POST "$BASE/deeds" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"childId":"<child-id>","deedTypeId":"<deed-type-id>","note":"Evening chores"}'

# Redeem points
curl -X POST "$BASE/redemptions" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"childId":"<child-id>","points":5,"description":"Treat"}'
```

---

## OpenClaw / OpenClaw CLI

The CLI is designed to be driven by OpenClaw agent workers. Each command maps to a single API call. Commands are stateless — state is stored server-side per parent.

## Environment Variables for Azure Deployment

| Variable | Description |
|---------|-------------|
| `DB` | Neon Postgres connection string |
| `AI_KEY_ENCRYPTION_KEY` | Base64 AES-256 key for OpenAI key encryption |
| `ALLOW_ANONYMOUS_PARENT` | Set to `true` to allow unauthenticated parent creation |
