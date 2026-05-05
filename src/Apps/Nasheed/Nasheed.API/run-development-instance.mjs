import { spawn } from "child_process";
import { fileURLToPath } from "url";
import { dirname } from "path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

console.log("========================================");
console.log("Nasheed API - Development Instance");
console.log("========================================");
console.log("Environment: Development");
console.log("Ports: 5009 (HTTP)");
console.log("MultiTenancy: Enabled");
console.log("========================================");
console.log("");

const env = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: "Development",
  ASPNETCORE_URLS: "http://localhost:5009",
};

const proc = spawn("dotnet", ["run", "--no-launch-profile"], {
  cwd: __dirname,
  env,
  stdio: "inherit",
  shell: true,
});

proc.on("exit", (code) => {
  console.log(`\nProcess exited with code ${code}`);
  process.exit(code ?? 0);
});
