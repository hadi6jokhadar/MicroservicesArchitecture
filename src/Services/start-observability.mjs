import { spawn } from "child_process";
import { fileURLToPath } from "url";
import { dirname, join } from "path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const repoRoot = join(__dirname, "..", "..");

console.log("Starting observability stack (Jaeger + Prometheus + Grafana)...");
console.log(`Working directory: ${repoRoot}`);
console.log("");

spawn(
  "wt.exe",
  [
    "--tabColor",
    "#FF6B35",
    "--title",
    "Observability Stack",
    "cmd.exe",
    "/k",
    `cd /d "${repoRoot}" && docker compose -f docker-compose.observability.yml up`,
  ],
  { detached: true, stdio: "ignore" },
);

console.log("Observability stack is starting in a new terminal tab.");
console.log("");
console.log("  Jaeger UI  → http://localhost:16686");
console.log("  Prometheus → http://localhost:9090");
console.log("  Grafana    → http://localhost:3100  (admin / admin)");
