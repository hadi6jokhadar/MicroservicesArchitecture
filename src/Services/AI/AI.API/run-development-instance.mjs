import { spawn } from "child_process";
import { fileURLToPath } from "url";
import { dirname } from "path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const command =
  process.platform === "win32"
    ? "venv\\Scripts\\python.exe"
    : "venv/bin/python";
const args = ["-m", "uvicorn", "main:app", "--reload", "--port", "5008"];

const child = spawn(command, args, {
  cwd: __dirname,
  stdio: "inherit",
  shell: false,
  env: {
    ...process.env,
  },
});

child.on("error", (error) => {
  console.error("Failed to start AI.API development instance:", error);
  process.exit(1);
});

child.on("close", (code) => {
  process.exit(code ?? 0);
});
