import { spawn } from "child_process";
import { fileURLToPath } from "url";
import { dirname } from "path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const child = spawn("dotnet", ["run", "--no-launch-profile"], {
  cwd: __dirname,
  stdio: "inherit",
  shell: false,
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: "Development",
    ASPNETCORE_URLS: "http://localhost:5006;https://localhost:5106",
  },
});

child.on("error", (error) => {
  console.error("Failed to start Translation.API development instance:", error);
  process.exit(1);
});

child.on("close", (code) => {
  process.exit(code ?? 0);
});
