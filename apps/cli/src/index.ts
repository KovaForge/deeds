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

async function request<T>(config: CliConfig, pathname: string, init: RequestInit = {}) {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  if (config.token) {
    headers.set("Authorization", `Bearer ${config.token}`);
  }
  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  const url = new URL(pathname, config.baseUrl);
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

// ─── Auth ────────────────────────────────────────────────────────────────────────────

async function authLogin(flags: Map<string, string | boolean>, config: CliConfig) {
  const token = getRequired(flags, "token");
  const baseUrl = getOptional(flags, "base-url") || config.baseUrl;
  const parentId = getOptional(flags, "parent-id");
  await writeConfig({ baseUrl, token, parentId: parentId || config.parentId });
  printJson({ success: true, baseUrl });
}

// ─── Token management ──────────────────────────────────────────────────────────

async function tokensCreate(flags: Map<string, string | boolean>, config: CliConfig) {
  const label = getRequired(flags, "label");
  const daysValid = flags.get("days-valid");
  const body: Record<string, unknown> = { label };
  if (typeof daysValid === "string") {
    body.daysValid = parseInt(daysValid, 10);
  }
  printJson(await request(config, "/cli-tokens", {
    method: "POST",
    body: JSON.stringify(body),
  }));
}

async function tokensList(flags: Map<string, string | boolean>, config: CliConfig) {
  printJson(await request(config, "/cli-tokens"));
}

async function tokensRevoke(flags: Map<string, string | boolean>, config: CliConfig) {
  const id = getRequired(flags, "id");
  printJson(await request(config, `/cli-tokens/${id}`, { method: "DELETE" }));
}

// ─── Children ─────────────────────────────────────────────────────────────────

async function childrenList(config: CliConfig) {
  printJson(await request(config, "/children"));
}

async function childrenCreate(flags: Map<string, string | boolean>, config: CliConfig) {
  const name = getRequired(flags, "name");
  const dpp = getOptional(flags, "dollar-per-point");
  const body: Record<string, unknown> = { name };
  if (dpp) {
    body.dollarPerPoint = parseFloat(dpp);
  }
  printJson(await request(config, "/children", {
    method: "POST",
    body: JSON.stringify(body),
  }));
}

async function childrenUpdate(flags: Map<string, string | boolean>, config: CliConfig, id: string) {
  const name = getOptional(flags, "name");
  const dpp = getOptional(flags, "dollar-per-point");
  const body: Record<string, unknown> = {};
  if (name) body.name = name;
  if (dpp) body.dollarPerPoint = parseFloat(dpp);
  printJson(await request(config, `/children/${id}`, {
    method: "PATCH",
    body: JSON.stringify(body),
  }));
}

async function childrenDelete(flags: Map<string, string | boolean>, config: CliConfig) {
  const id = getRequired(flags, "id");
  printJson(await request(config, `/children/${id}`, { method: "DELETE" }));
}

// ─── Deed Types ────────────────────────────────────────────────────────────────

async function deedTypesList(config: CliConfig) {
  printJson(await request(config, "/deed-types"));
}

async function deedTypesCreate(flags: Map<string, string | boolean>, config: CliConfig) {
  const name = getRequired(flags, "name");
  const points = getRequired(flags, "points");
  printJson(await request(config, "/deed-types", {
    method: "POST",
    body: JSON.stringify({ name, points: parseInt(points, 10) }),
  }));
}

async function deedTypesUpdate(flags: Map<string, string | boolean>, config: CliConfig, id: string) {
  const name = getOptional(flags, "name");
  const points = getOptional(flags, "points");
  const active = flags.get("active");
  const body: Record<string, unknown> = {};
  if (name) body.name = name;
  if (points) body.points = parseInt(points, 10);
  if (typeof active === "boolean") body.active = active;
  printJson(await request(config, `/deed-types/${id}`, {
    method: "PATCH",
    body: JSON.stringify(body),
  }));
}

async function deedTypesDelete(flags: Map<string, string | boolean>, config: CliConfig) {
  const id = getRequired(flags, "id");
  printJson(await request(config, `/deed-types/${id}`, { method: "DELETE" }));
}

// ─── Deeds ────────────────────────────────────────────────────────────────────

async function deedsList(flags: Map<string, string | boolean>, config: CliConfig) {
  const childId = getRequired(flags, "child-id");
  printJson(await request(config, `/children/${childId}/deeds`));
}

async function deedsCreate(flags: Map<string, string | boolean>, config: CliConfig) {
  const childId = getRequired(flags, "child-id");
  const deedTypeId = getRequired(flags, "deed-type-id");
  const points = getOptional(flags, "points");
  const note = getOptional(flags, "note");
  const occurredAt = getOptional(flags, "occurred-at");
  const createdBy = getOptional(flags, "created-by");
  const body: Record<string, unknown> = { childId, deedTypeId };
  if (points) body.points = parseInt(points, 10);
  if (note) body.note = note;
  if (occurredAt) body.occurredAt = occurredAt;
  if (createdBy) body.createdBy = createdBy;
  printJson(await request(config, "/deeds", {
    method: "POST",
    body: JSON.stringify(body),
  }));
}

async function deedsDelete(flags: Map<string, string | boolean>, config: CliConfig) {
  const id = getRequired(flags, "id");
  printJson(await request(config, `/deeds/${id}`, { method: "DELETE" }));
}

// ─── Redeem Types ────────────────────────────────────────────────────────────

async function redeemTypesList(config: CliConfig) {
  printJson(await request(config, "/redeem-types"));
}

async function redeemTypesCreate(flags: Map<string, string | boolean>, config: CliConfig) {
  const name = getRequired(flags, "name");
  const points = getRequired(flags, "points");
  printJson(await request(config, "/redeem-types", {
    method: "POST",
    body: JSON.stringify({ name, points: parseInt(points, 10) }),
  }));
}

// ─── Redemptions ─────────────────────────────────────────────────────────────

async function redemptionsList(flags: Map<string, string | boolean>, config: CliConfig) {
  const childId = getRequired(flags, "child-id");
  printJson(await request(config, `/children/${childId}/redemptions`));
}

async function redemptionsCreate(flags: Map<string, string | boolean>, config: CliConfig) {
  const childId = getRequired(flags, "child-id");
  const points = getRequired(flags, "points");
  const redeemTypeId = getOptional(flags, "redeem-type-id");
  const description = getOptional(flags, "description");
  const createdBy = getOptional(flags, "created-by");
  const body: Record<string, unknown> = { childId, points: parseInt(points, 10) };
  if (redeemTypeId) body.redeemTypeId = redeemTypeId;
  if (description) body.description = description;
  if (createdBy) body.createdBy = createdBy;
  printJson(await request(config, "/redemptions", {
    method: "POST",
    body: JSON.stringify(body),
  }));
}

// ─── Balances ────────────────────────────────────────────────────────────────

async function balancesList(config: CliConfig) {
  printJson(await request(config, "/children"));
}

async function balanceGet(flags: Map<string, string | boolean>, config: CliConfig) {
  const childId = getRequired(flags, "child-id");
  printJson(await request(config, `/balances/${childId}`));
}

async function history(flags: Map<string, string | boolean>, config: CliConfig) {
  const childId = getRequired(flags, "child-id");
  printJson(await request(config, `/children/${childId}/history`));
}

// ─── Parent ─────────────────────────────────────────────────────────────────

async function parentMe(config: CliConfig) {
  printJson(await request(config, "/parents/me"));
}

// ─── Main ──────────────────────────────────────────────────────────────────

async function main() {
  const { positionals, flags } = parseArgs(process.argv.slice(2));
  const [command1, command2] = positionals;
  const config = await readConfig();

  // Auth
  if (command1 === "auth" && command2 === "login") {
    await authLogin(flags, config);
    return;
  }

  // Tokens
  if (command1 === "tokens") {
    if (command2 === "create") { await tokensCreate(flags, config); return; }
    if (command2 === "list") { await tokensList(flags, config); return; }
    if (command2 === "revoke") { await tokensRevoke(flags, config); return; }
  }

  // Children
  if (command1 === "children") {
    if (command2 === "list") { await childrenList(config); return; }
    if (command2 === "create") { await childrenCreate(flags, config); return; }
    if (command2 === "update") { await childrenUpdate(flags, config, getRequired(flags, "id")); return; }
    if (command2 === "delete") { await childrenDelete(flags, config); return; }
  }

  // Deed Types
  if (command1 === "deed-types") {
    if (command2 === "list") { await deedTypesList(config); return; }
    if (command2 === "create") { await deedTypesCreate(flags, config); return; }
    if (command2 === "update") { await deedTypesUpdate(flags, config, getRequired(flags, "id")); return; }
    if (command2 === "delete") { await deedTypesDelete(flags, config); return; }
  }

  // Deeds
  if (command1 === "deeds") {
    if (command2 === "list") { await deedsList(flags, config); return; }
    if (command2 === "create") { await deedsCreate(flags, config); return; }
    if (command2 === "delete") { await deedsDelete(flags, config); return; }
  }

  // Redeem Types
  if (command1 === "redeem-types") {
    if (command2 === "list") { await redeemTypesList(config); return; }
    if (command2 === "create") { await redeemTypesCreate(flags, config); return; }
  }

  // Redemptions
  if (command1 === "redemptions") {
    if (command2 === "list") { await redemptionsList(flags, config); return; }
    if (command2 === "create") { await redemptionsCreate(flags, config); return; }
  }

  // Balances
  if (command1 === "balances") {
    if (command2 === "list") { await balancesList(config); return; }
    if (command2 === "get") { await balanceGet(flags, config); return; }
  }

  // History
  if (command1 === "history") {
    await history(flags, config);
    return;
  }

  // Parent
  if (command1 === "parent" && command2 === "me") {
    await parentMe(config);
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
      "openclaw deeds delete --id <deed-id>",
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
