#!/usr/bin/env node
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

type CliConfig = {
  baseUrl: string;
  token?: string;
  parentId?: string;
};

const CONFIG_PATH = path.join(os.homedir(), ".briarforge", "config.json");

function printJson(value: unknown) {
  process.stdout.write(`${JSON.stringify(value, null, 2)}\n`);
}

async function readConfig(): Promise<CliConfig> {
  try {
    const raw = await fs.readFile(CONFIG_PATH, "utf8");
    return JSON.parse(raw) as CliConfig;
  } catch {
    return {
      baseUrl: process.env.OPENCLAW_BASE_URL || "http://localhost:7071/api",
      token: process.env.OPENCLAW_TOKEN,
      parentId: process.env.OPENCLAW_PARENT_ID,
    };
  }
}

async function writeConfig(config: CliConfig) {
  await fs.mkdir(path.dirname(CONFIG_PATH), { recursive: true });
  await fs.writeFile(CONFIG_PATH, JSON.stringify(config, null, 2));
}

function parseArgs(argv: string[]) {
  const positionals: string[] = [];
  const flags = new Map<string, string | boolean>();
  for (let i = 0; i < argv.length; i += 1) {
    const value = argv[i];
    if (!value.startsWith("--")) {
      positionals.push(value);
      continue;
    }
    const key = value.slice(2);
    const next = argv[i + 1];
    if (!next || next.startsWith("--")) {
      flags.set(key, true);
      continue;
    }
    flags.set(key, next);
    i += 1;
  }
  return { positionals, flags };
}

// ─── Session context ─────────────────────────────────────────────────────────

type Session = {
  baseUrl: string;
  token: string;
  parentId: string;
};

async function resolveSession(config: CliConfig): Promise<Session> {
  if (!config.token) {
    throw new Error("No token configured. Run: openclaw auth login --token <token>");
  }

  // Build base (ensure trailing slash)
  const base = config.baseUrl.endsWith("/")
    ? config.baseUrl
    : config.baseUrl + "/";
  const baseUrl = base.slice(0, -1); // remove trailing slash for use as URL origin

  // Use stored parentId if available
  if (config.parentId) {
    return { baseUrl, token: config.token, parentId: config.parentId };
  }

  // Otherwise resolve from /parents/me
  const url = new URL(`${base}parents/me`);
  const headers = new Headers({ "x-deeds-token": config.token, Accept: "application/json" });
  const res = await fetch(url, { headers });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error((body as { error?: string }).error || `Auth failed: ${res.status}`);
  }
  const data = (await res.json()) as { Id: string };
  return { baseUrl, token: config.token, parentId: data.Id };
}

async function apiRequest<T>(session: Session, pathname: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  headers.set("x-deeds-token", session.token);
  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  // Strip leading slash to avoid URL path concatenation issues
  const normalizedPath = pathname.startsWith("/") ? pathname.slice(1) : pathname;
  const url = new URL(`${session.baseUrl}/${normalizedPath}`);
  const response = await fetch(url, { ...init, headers });
  const data = (await response.json().catch(() => ({}))) as T & { error?: string; message?: string };
  if (!response.ok) {
    throw new Error(data.error || data.message || `Request failed with status ${response.status}`);
  }
  return data;
}

function getRequired(flags: Map<string, string | boolean>, name: string): string {
  const value = flags.get(name);
  if (!value || typeof value !== "string") {
    throw new Error(`Missing required flag --${name}`);
  }
  return value;
}

function getOptional(flags: Map<string, string | boolean>, name: string): string | undefined {
  const value = flags.get(name);
  return typeof value === "string" ? value : undefined;
}

// ─── Auth ─────────────────────────────────────────────────────────────────────

async function authLogin(flags: Map<string, string | boolean>, config: CliConfig) {
  const token = getRequired(flags, "token");
  const baseUrl = getOptional(flags, "base-url") || config.baseUrl;
  const parentId = getOptional(flags, "parent-id");
  await writeConfig({ baseUrl, token, parentId: parentId || config.parentId });
  printJson({ success: true, baseUrl });
}

// ─── Token management ─────────────────────────────────────────────────────────

async function tokensCreate(flags: Map<string, string | boolean>, session: Session) {
  const label = getRequired(flags, "label");
  const daysValid = flags.get("days-valid");
  const body: Record<string, unknown> = { label };
  if (typeof daysValid === "string") {
    body.daysValid = parseInt(daysValid, 10);
  }
  printJson(await apiRequest(session, "cli-tokens", {
    method: "POST",
    body: JSON.stringify(body),
  }));
}

async function tokensList(session: Session) {
  printJson(await apiRequest(session, "cli-tokens"));
}

async function tokensRevoke(flags: Map<string, string | boolean>, session: Session) {
  const id = getRequired(flags, "id");
  printJson(await apiRequest(session, `cli-tokens/${id}`, { method: "DELETE" }));
}

// ─── Children ─────────────────────────────────────────────────────────────────

async function childrenList(session: Session) {
  printJson(await apiRequest(session, `parents/${session.parentId}/children`));
}

async function childrenCreate(flags: Map<string, string | boolean>, session: Session) {
  const name = getRequired(flags, "name");
  const dpp = getOptional(flags, "dollar-per-point");
  const body: Record<string, unknown> = { name, parentId: session.parentId };
  if (dpp) {
    body.dollarPerPoint = parseFloat(dpp);
  }
  printJson(await apiRequest(session, "children", {
    method: "POST",
    body: JSON.stringify(body),
  }));
}

async function childrenUpdate(flags: Map<string, string | boolean>, session: Session, id: string) {
  const name = getOptional(flags, "name");
  const dpp = getOptional(flags, "dollar-per-point");
  const body: Record<string, unknown> = { parentId: session.parentId };
  if (name) body.name = name;
  if (dpp) body.dollarPerPoint = parseFloat(dpp);
  printJson(await apiRequest(session, `children/${id}`, {
    method: "PATCH",
    body: JSON.stringify(body),
  }));
}

async function childrenDelete(flags: Map<string, string | boolean>, session: Session) {
  const id = getRequired(flags, "id");
  printJson(await apiRequest(session, `parents/${session.parentId}/children/${id}`, { method: "DELETE" }));
}

// ─── Deed Types ────────────────────────────────────────────────────────────────

async function deedTypesList(session: Session) {
  printJson(await apiRequest(session, `parents/${session.parentId}/deed-types`));
}

async function deedTypesCreate(flags: Map<string, string | boolean>, session: Session) {
  const name = getRequired(flags, "name");
  const points = getRequired(flags, "points");
  printJson(await apiRequest(session, "deed-types", {
    method: "POST",
    body: JSON.stringify({ name, points: parseInt(points, 10), parentId: session.parentId }),
  }));
}

async function deedTypesUpdate(flags: Map<string, string | boolean>, session: Session, id: string) {
  const name = getOptional(flags, "name");
  const points = getOptional(flags, "points");
  const active = flags.get("active");
  const body: Record<string, unknown> = { parentId: session.parentId };
  if (name) body.name = name;
  if (points) body.points = parseInt(points, 10);
  if (typeof active === "boolean") body.active = active;
  printJson(await apiRequest(session, `deed-types/${id}`, {
    method: "PATCH",
    body: JSON.stringify(body),
  }));
}

async function deedTypesDelete(flags: Map<string, string | boolean>, session: Session) {
  const id = getRequired(flags, "id");
  printJson(await apiRequest(session, `parents/${session.parentId}/deed-types/${id}`, { method: "DELETE" }));
}

// ─── Deeds ────────────────────────────────────────────────────────────────────

async function deedsList(flags: Map<string, string | boolean>, session: Session) {
  const childId = getRequired(flags, "child-id");
  printJson(await apiRequest(session, `children/${childId}/deeds?parentId=${session.parentId}`));
}

async function deedsCreate(flags: Map<string, string | boolean>, session: Session) {
  const childId = getRequired(flags, "child-id");
  const deedTypeId = getRequired(flags, "deed-type-id");
  const points = getOptional(flags, "points");
  const note = getOptional(flags, "note");
  const occurredAt = getOptional(flags, "occurred-at");
  const createdBy = getOptional(flags, "created-by");
  const body: Record<string, unknown> = { childId, deedTypeId, parentId: session.parentId };
  if (points) body.points = parseInt(points, 10);
  if (note) body.note = note;
  if (occurredAt) body.occurredAt = occurredAt;
  if (createdBy) body.createdBy = createdBy;
  printJson(await apiRequest(session, "deeds", {
    method: "POST",
    body: JSON.stringify(body),
  }));
}

async function deedsDelete(flags: Map<string, string | boolean>, session: Session) {
  const id = getRequired(flags, "id");
  const childId = getRequired(flags, "child-id");
  printJson(await apiRequest(session, `children/${childId}/deeds/${id}?parentId=${session.parentId}`, { method: "DELETE" }));
}

// ─── Redeem Types ────────────────────────────────────────────────────────────

async function redeemTypesList(session: Session) {
  printJson(await apiRequest(session, `parents/${session.parentId}/redeem-types`));
}

async function redeemTypesCreate(flags: Map<string, string | boolean>, session: Session) {
  const name = getRequired(flags, "name");
  const points = getRequired(flags, "points");
  printJson(await apiRequest(session, "redeem-types", {
    method: "POST",
    body: JSON.stringify({ name, points: parseInt(points, 10), parentId: session.parentId }),
  }));
}

// ─── Redemptions ─────────────────────────────────────────────────────────────

async function redemptionsList(flags: Map<string, string | boolean>, session: Session) {
  const childId = getRequired(flags, "child-id");
  printJson(await apiRequest(session, `children/${childId}/redemptions?parentId=${session.parentId}`));
}

async function redemptionsCreate(flags: Map<string, string | boolean>, session: Session) {
  const childId = getRequired(flags, "child-id");
  const points = getRequired(flags, "points");
  const redeemTypeId = getOptional(flags, "redeem-type-id");
  const description = getOptional(flags, "description");
  const createdBy = getOptional(flags, "created-by");
  const body: Record<string, unknown> = { childId, points: parseInt(points, 10), parentId: session.parentId };
  if (redeemTypeId) body.redeemTypeId = redeemTypeId;
  if (description) body.description = description;
  if (createdBy) body.createdBy = createdBy;
  printJson(await apiRequest(session, "redemptions", {
    method: "POST",
    body: JSON.stringify(body),
  }));
}

// ─── Balances ────────────────────────────────────────────────────────────────

async function balancesList(session: Session) {
  printJson(await apiRequest(session, `parents/${session.parentId}/children/with-balances`));
}

async function balanceGet(flags: Map<string, string | boolean>, session: Session) {
  const childId = getRequired(flags, "child-id");
  printJson(await apiRequest(session, `balances/${childId}?parentId=${session.parentId}`));
}

async function history(flags: Map<string, string | boolean>, session: Session) {
  const childId = getRequired(flags, "child-id");
  const res = await apiRequestRaw(session, `children/${childId}/export/csv`);
  // Parse CSV: entry_type,points,dollar_value,note,occurred_at,recorded_by
  const lines = res.split("\n").filter((l) => l.trim());
  if (lines.length < 2) {
    printJson([]);
    return;
  }
  const headers = lines[0].split(",");
  const rows = lines.slice(1).map((line) => {
    const values = line.split(",");
    const row: Record<string, string> = {};
    headers.forEach((h, i) => { row[h.trim()] = values[i]?.trim() || ""; });
    return row;
  });
  printJson(rows);
}

async function apiRequestRaw(session: Session, pathname: string, init: RequestInit = {}): Promise<string> {
  const headers = new Headers(init.headers);
  headers.set("x-deeds-token", session.token);
  const normalizedPath = pathname.startsWith("/") ? pathname.slice(1) : pathname;
  const url = new URL(`${session.baseUrl}/${normalizedPath}`);
  const response = await fetch(url, { ...init, headers });
  if (!response.ok) {
    throw new Error(`Request failed with status ${response.status}`);
  }
  return response.text();
}

// ─── Parent ─────────────────────────────────────────────────────────────────

async function parentMe(session: Session) {
  printJson(await apiRequest(session, "parents/me"));
}

// ─── Main ─────────────────────────────────────────────────────────────────────

async function main() {
  const { positionals, flags } = parseArgs(process.argv.slice(2));
  const [command1, command2] = positionals;
  const config = await readConfig();

  // Auth — no session needed
  if (command1 === "auth" && command2 === "login") {
    await authLogin(flags, config);
    return;
  }

  // Resolve session (parent ID from /parents/me if not stored)
  const session = await resolveSession(config);

  // Tokens
  if (command1 === "tokens") {
    if (command2 === "create") { await tokensCreate(flags, session); return; }
    if (command2 === "list") { await tokensList(session); return; }
    if (command2 === "revoke") { await tokensRevoke(flags, session); return; }
  }

  // Children
  if (command1 === "children") {
    if (command2 === "list") { await childrenList(session); return; }
    if (command2 === "create") { await childrenCreate(flags, session); return; }
    if (command2 === "update") { await childrenUpdate(flags, session, getRequired(flags, "id")); return; }
    if (command2 === "delete") { await childrenDelete(flags, session); return; }
  }

  // Deed Types
  if (command1 === "deed-types") {
    if (command2 === "list") { await deedTypesList(session); return; }
    if (command2 === "create") { await deedTypesCreate(flags, session); return; }
    if (command2 === "update") { await deedTypesUpdate(flags, session, getRequired(flags, "id")); return; }
    if (command2 === "delete") { await deedTypesDelete(flags, session); return; }
  }

  // Deeds
  if (command1 === "deeds") {
    if (command2 === "list") { await deedsList(flags, session); return; }
    if (command2 === "create") { await deedsCreate(flags, session); return; }
    if (command2 === "delete") { await deedsDelete(flags, session); return; }
  }

  // Redeem Types
  if (command1 === "redeem-types") {
    if (command2 === "list") { await redeemTypesList(session); return; }
    if (command2 === "create") { await redeemTypesCreate(flags, session); return; }
  }

  // Redemptions
  if (command1 === "redemptions") {
    if (command2 === "list") { await redemptionsList(flags, session); return; }
    if (command2 === "create") { await redemptionsCreate(flags, session); return; }
  }

  // Balances
  if (command1 === "balances") {
    if (command2 === "list") { await balancesList(session); return; }
    if (command2 === "get") { await balanceGet(flags, session); return; }
  }

  // History
  if (command1 === "history") {
    await history(flags, session);
    return;
  }

  // Parent
  if (command1 === "parent" && command2 === "me") {
    await parentMe(session);
    return;
  }

  // Help
  printJson({
    usage: [
      "# Auth",
      "openclaw auth login --token <token> [--base-url <url>] [--parent-id <id>]",
      "",
      "# CLI Tokens",
      "openclaw tokens create --label <label> [--days-valid <days>]",
      "openclaw tokens list",
      "openclaw tokens revoke --id <token-id>",
      "",
      "# Children",
      "openclaw children list",
      "openclaw children create --name <name> [--dollar-per-point <number>]",
      "openclaw children update --id <child-id> [--name <name>] [--dollar-per-point <number>]",
      "openclaw children delete --id <child-id>",
      "",
      "# Deed Types",
      "openclaw deed-types list",
      "openclaw deed-types create --name <name> --points <number>",
      "openclaw deed-types update --id <deed-type-id> [--name <name>] [--points <n>] [--active true|false]",
      "openclaw deed-types delete --id <deed-type-id>",
      "",
      "# Deeds",
      "openclaw deeds list --child-id <child-id>",
      "openclaw deeds create --child-id <child-id> --deed-type-id <deed-type-id> [--points <n>] [--note <text>] [--occurred-at <ISO date>] [--created-by <text>]",
      "openclaw deeds delete --child-id <child-id> --id <deed-id>",
      "",
      "# Redeem Types",
      "openclaw redeem-types list",
      "openclaw redeem-types create --name <name> --points <number>",
      "",
      "# Redemptions",
      "openclaw redemptions list --child-id <child-id>",
      "openclaw redemptions create --child-id <child-id> --points <n> [--redeem-type-id <id>] [--description <text>]",
      "",
      "# Balances",
      "openclaw balances list",
      "openclaw balances get --child-id <child-id>",
      "",
      "# History",
      "openclaw history --child-id <child-id>",
      "",
      "# Parent",
      "openclaw parent me",
    ]
  });
}

main().catch((error) => {
  printJson({
    error: true,
    message: error instanceof Error ? error.message : String(error),
  });
  process.exitCode = 1;
});
