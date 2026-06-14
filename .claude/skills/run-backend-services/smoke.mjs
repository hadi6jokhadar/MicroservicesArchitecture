/**
 * Smoke-test driver for the backend microservices.
 *
 * Usage:
 *   node smoke.mjs [service1 service2 ...]
 *
 * If no services are given, probes all known HTTP health endpoints
 * for services that are already running. Pass service names to START
 * them first (in background), then probe.
 *
 * Service names: identity, tenant, notification, filemanager,
 *                translation, category, gateway, nasheed
 *                (ai is Python — start manually via its bat/mjs)
 *
 * Examples:
 *   node smoke.mjs                          # probe all, report status
 *   node smoke.mjs identity tenant          # start those two, probe all
 *   node smoke.mjs --only identity          # start + probe only identity
 */

import { spawn } from "child_process";
import { fileURLToPath } from "url";
import { dirname, join, resolve } from "path";
import http from "http";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// Relative to MicroservicesArchitecture/src/Services (or Apps for Nasheed)
const SERVICES_ROOT = resolve(__dirname, "..", "..", "..", "src", "Services");
const APPS_ROOT = resolve(__dirname, "..", "..", "..", "src", "Apps");
const GATEWAY_ROOT = resolve(__dirname, "..", "..", "..", "src", "Gateway");

const SERVICE_MAP = {
  identity:     { cwd: join(SERVICES_ROOT, "Identity", "Identity.API"),         port: 5001, type: "dotnet" },
  tenant:       { cwd: join(SERVICES_ROOT, "Tenant", "Tenant.API"),             port: 5002, type: "dotnet" },
  notification: { cwd: join(SERVICES_ROOT, "Notification", "Notification.API"), port: 5004, type: "dotnet" },
  filemanager:  { cwd: join(SERVICES_ROOT, "FileManager", "FileManager.API"),   port: 5005, type: "dotnet" },
  translation:  { cwd: join(SERVICES_ROOT, "Translation", "Translation.API"),   port: 5006, type: "dotnet" },
  category:     { cwd: join(SERVICES_ROOT, "Category", "Category.API"),         port: 5007, type: "dotnet" },
  ai:           { cwd: join(SERVICES_ROOT, "AI", "AI.API"),                     port: 5008, type: "python" },
  nasheed:      { cwd: join(APPS_ROOT, "Nasheed", "Nasheed.API"),               port: 5009, type: "dotnet" },
  gateway:      { cwd: join(GATEWAY_ROOT, "Gateway.API"),                       port: 5000, type: "dotnet" },
};

const HEALTH_PATHS = {
  dotnet: "/health",
  python: "/health",
};

// ── argument parsing ──────────────────────────────────────────────────────────
const args = process.argv.slice(2);
const onlyFlag = args.indexOf("--only") !== -1;
const targets = args.filter(a => !a.startsWith("--")).map(a => a.toLowerCase());

// Validate
for (const t of targets) {
  if (!SERVICE_MAP[t]) {
    console.error(`Unknown service: "${t}". Valid: ${Object.keys(SERVICE_MAP).join(", ")}`);
    process.exit(1);
  }
}

// ── helpers ───────────────────────────────────────────────────────────────────
function probe(port, path = "/health", timeoutMs = 3000) {
  return new Promise((resolve) => {
    const req = http.get({ hostname: "localhost", port, path, timeout: timeoutMs }, (res) => {
      let body = "";
      res.on("data", d => body += d);
      res.on("end", () => resolve({ ok: res.statusCode < 400, status: res.statusCode, body: body.trim() }));
    });
    req.on("error", () => resolve({ ok: false, status: 0, body: "connection refused" }));
    req.on("timeout", () => { req.destroy(); resolve({ ok: false, status: 0, body: "timeout" }); });
  });
}

function wait(ms) { return new Promise(r => setTimeout(r, ms)); }

function launchDotnet(name, cfg) {
  const env = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: "Development",
    ASPNETCORE_URLS: `http://localhost:${cfg.port}`,
  };
  const child = spawn("dotnet", ["run", "--no-launch-profile"], {
    cwd: cfg.cwd,
    env,
    stdio: ["ignore", "pipe", "pipe"],
    shell: false,
  });
  child.stdout.on("data", d => process.stdout.write(`[${name}] ${d}`));
  child.stderr.on("data", d => process.stderr.write(`[${name}] ${d}`));
  child.on("error", e => console.error(`[${name}] failed to start:`, e.message));
  return child;
}

function launchPython(name, cfg) {
  const pythonExe = process.platform === "win32"
    ? join(cfg.cwd, "venv", "Scripts", "python.exe")
    : join(cfg.cwd, "venv", "bin", "python");
  const child = spawn(pythonExe, ["-m", "uvicorn", "main:app", "--reload", "--port", String(cfg.port)], {
    cwd: cfg.cwd,
    stdio: ["ignore", "pipe", "pipe"],
    shell: false,
  });
  child.stdout.on("data", d => process.stdout.write(`[${name}] ${d}`));
  child.stderr.on("data", d => process.stderr.write(`[${name}] ${d}`));
  child.on("error", e => console.error(`[${name}] failed to start:`, e.message));
  return child;
}

// ── main ──────────────────────────────────────────────────────────────────────
const started = [];

if (targets.length > 0) {
  console.log(`\nStarting: ${targets.join(", ")}...\n`);
  for (const name of targets) {
    const cfg = SERVICE_MAP[name];
    const child = cfg.type === "python" ? launchPython(name, cfg) : launchDotnet(name, cfg);
    started.push({ name, cfg, child });
  }
  console.log("Waiting 15 s for services to initialise...\n");
  await wait(15000);
}

// Determine which services to probe
const toProbe = onlyFlag && targets.length > 0
  ? targets.map(n => [n, SERVICE_MAP[n]])
  : Object.entries(SERVICE_MAP);

console.log("\n── Health check ──────────────────────────────────────────\n");
let allOk = true;
for (const [name, cfg] of toProbe) {
  const path = HEALTH_PATHS[cfg.type] ?? "/health";
  const result = await probe(cfg.port, path);
  const icon = result.ok ? "✓" : "✗";
  const label = result.ok ? "UP" : "DOWN";
  console.log(`  ${icon} ${name.padEnd(13)} :${cfg.port}  ${label}  (HTTP ${result.status}) ${result.ok ? "" : "— " + result.body}`);
  if (!result.ok) allOk = false;
}
console.log("\n──────────────────────────────────────────────────────────\n");

// Kill started children
for (const { name, child } of started) {
  process.stdout.write(`Stopping ${name}... `);
  child.kill();
  console.log("done");
}

process.exit(allOk ? 0 : 1);
