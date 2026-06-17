import { spawn } from "child_process";
import { fileURLToPath } from "url";
import { dirname, join } from "path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const testProjects = [
  {
    color: "#FFFF00",
    title: "Tenant Tests",
    path: join(__dirname, "Tenant", "Tenant.API.Tests"),
  },
  {
    color: "#00FF00",
    title: "Identity Tests",
    path: join(__dirname, "Identity", "Identity.API.Tests"),
  },
  {
    color: "#0077ffff",
    title: "Notification Tests",
    path: join(__dirname, "Notification", "Notification.API.Tests"),
  },
  {
    color: "#6200ffff",
    title: "FileManager Tests",
    path: join(__dirname, "FileManager", "FileManager.API.Tests"),
  },
  {
    color: "#FF8800",
    title: "Translation Tests",
    path: join(__dirname, "Translation", "Translation.API.Tests"),
  },
  {
    color: "#00ffddff",
    title: "Category Tests",
    path: join(__dirname, "Category", "Category.API.Tests"),
  },
];

console.log("Starting Redis for tests...\n");

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// Start Redis in its own terminal tab (same as start-all-services.mjs)
spawn(
  "wt.exe",
  [
    "--tabColor",
    "#646464ff",
    "--title",
    "Redis (Tests)",
    "cmd.exe",
    "/k",
    `cd /d "${join(__dirname, "..", "..")}" && redis-server.lnk`,
  ],
  {
    detached: true,
    stdio: "ignore",
  },
);

// Wait for Redis to be ready before launching tests
await delay(5000);

console.log("Running all service tests...\n");

async function runTests() {
  for (const project of testProjects) {
    console.log(`Starting ${project.title}...`);

    spawn(
      "wt.exe",
      [
        "--tabColor",
        project.color,
        "--title",
        project.title,
        "cmd.exe",
        "/k",
        `cd /d "${project.path}" && dotnet test -v normal`,
      ],
      {
        detached: true,
        stdio: "ignore",
      },
    );

    await delay(2000);
  }

  console.log("\nAll test runners are starting...");
  console.log("You can now close this window.");
}

runTests().catch(console.error);
