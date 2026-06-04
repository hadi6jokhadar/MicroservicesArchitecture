import { spawn } from "child_process";
import { fileURLToPath } from "url";
import { dirname, join } from "path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const services = [
  {
    color: "#FF6B35",
    title: "Observability (Jaeger + Prometheus + Grafana)",
    path: join(__dirname, "..", ".."),
    command: "docker compose -f docker-compose.observability.yml up",
  },
  {
    color: "#646464ff",
    title: "Redis",
    path: join(__dirname, "..", ".."),
    command: "redis-server.lnk",
  },
  {
    color: "#FFFF00",
    title: "Tenant Service",
    path: join(__dirname, "Tenant", "Tenant.API"),
    command: "run-development-instance.bat",
  },
  {
    color: "#00FF00",
    title: "Identity - Dev Instance",
    path: join(__dirname, "Identity", "Identity.API"),
    command: "run-development-instance.bat",
  },
  {
    color: "#0077ffff",
    title: "Notification Service",
    path: join(__dirname, "Notification", "Notification.API"),
    command: "run-development-instance.bat",
  },
  {
    color: "#6200ffff",
    title: "FileManager Service",
    path: join(__dirname, "FileManager", "FileManager.API"),
    command: "run-development-instance.bat",
  },
  {
    color: "#FF8800",
    title: "Translation Service",
    path: join(__dirname, "Translation", "Translation.API"),
    command: "run-development-instance.bat",
  },
  {
    color: "#00ffddff",
    title: "AI Service",
    path: join(__dirname, "AI", "AI.API"),
    command: "run-development-instance.bat",
  },
  {
    color: "#FFFFFF",
    title: "Category Service",
    path: join(__dirname, "Category", "Category.API"),
    command: "run-development-instance.bat",
  },
  {
    color: "#FF0055",
    title: "Gateway API",
    path: join(__dirname, "..", "Gateway", "Gateway.API"),
    command: "run-development-instance.bat",
  },
];

console.log("Starting all development services...\n");

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function startServices() {
  for (const service of services) {
    console.log(`Starting ${service.title}...`);

    // Launch Windows Terminal tab
    spawn(
      "wt.exe",
      [
        "--tabColor",
        service.color,
        "--title",
        service.title,
        "cmd.exe",
        "/k",
        `cd /d "${service.path}" && ${service.command}`,
      ],
      {
        detached: true,
        stdio: "ignore",
      },
    );

    // 4 second delay
    await delay(4000);
  }

  console.log("\nAll development instances are starting...");
  console.log("You can now close this window.");
}

startServices().catch(console.error);
