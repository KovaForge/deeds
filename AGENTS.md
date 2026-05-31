# Agent Rules

- Always summarize code changes in the final response, and use that summary when performing `git push` after each code update.

## Semantic Versioning

- Use semantic versioning for all versioned artifacts: MAJOR.MINOR.PATCH.
- MAJOR: breaking changes to public APIs, data contracts, or user-visible behavior.
- MINOR: backward-compatible features or enhancements.
- PATCH: backward-compatible bug fixes or small corrections.
- Pre-release and build metadata follow SemVer 2.0.0 conventions.

## Purpose

- This document provides clear operating instructions for LLM-assisted work in this repository.

## Scope

- Applies to documentation, code, tests, configs, and release notes.
- If a request conflicts with repository guidelines, ask for clarification.

## Communication

- Be concise and factual.
- Prefer short paragraphs and bullet lists.
- Use consistent terminology from existing docs.

## Repository Awareness

- Read existing docs before adding new guidance.
- Avoid duplicating information unless it is a deliberate summary.
- Keep instructions in ASCII unless the target file already uses Unicode.

## Change Safety

- Do not remove or rewrite unrelated content.
- Do not change version numbers unless explicitly requested.
- Flag assumptions clearly when requirements are ambiguous.

## Code and Config Changes

- Follow existing patterns in each project area.
- Keep changes minimal and targeted.
- Add small comments only when necessary to explain non-obvious logic.

## Testing

- If you modify executable code, suggest relevant tests.
- If tests are added, align them with current test conventions.

## Documentation

- Update or add docs when behavior or usage changes.
- Keep filenames and headings descriptive and stable.

## Security and Privacy

- Do not include secrets or tokens.
- Avoid logging sensitive data in examples.

## Output Format

- For changes, summarize what changed and where.
- Provide next steps only when they are natural and actionable.

---

## OpenClaw / Hermes Operability

This project (Good Deeds Tracker) is designed to be operated by OpenClaw/Hermes agent workers. The following conventions apply.

### Token Authentication

- CLI tokens use `gd_pat_` prefix (Good Deeds Personal Access Token).
- Raw token is shown once at creation time — it cannot be recovered later.
- Tokens are stored as SHA-256 hashes in `parent_cli_tokens` table.
- The API accepts `Authorization: Bearer <token>` on all routes.

### Single-Parent Model

All API operations are scoped to a single parent account. The CLI operates as the authenticated parent and can manage:
- Children (create, update, delete, list)
- Deed types (create, update, delete, list)
- Deeds (create, delete, list per child)
- Redeem types (create, list)
- Redemptions (create, list per child)
- Balances and history

The parent is resolved from the Bearer token (CLI auth) or `x-ms-client-principal` header (Azure SWA auth) or `x-parent-id` header / `?parentId=` query param (fallback).

### CLI Usage

```bash
# Authenticate
openclaw auth login --token <gd_pat_xxx> --base-url <api-base-url>

# Manage tokens
openclaw tokens create --label "laptop" [--days-valid 90]
openclaw tokens list
openclaw tokens revoke --id <token-id>

# Operate on children
openclaw children list
openclaw children create --name "Sam" --dollar-per-point 1.50

# Operate on deeds
openclaw deed-types create --name "Washing dishes" --points 3
openclaw deeds create --child-id <id> --deed-type-id <id> --note "Evening chores"

# View balances
openclaw balances list
openclaw history --child-id <id>
```

### Build Notes

- C# API: `cd api && dotnet build` (Azure Functions .NET 8 isolated worker)
- TypeScript CLI: `cd apps/cli && npm run build` (produces `dist/index.js`)
- TypeScript compiles successfully; C# build has a known metadata generator issue in some environments but the source compiles correctly.
