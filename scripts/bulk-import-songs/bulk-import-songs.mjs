#!/usr/bin/env node
// Bulk-import existing MP3 files into the Nasheed service.
// Run on PC2 (the Mac host) after `docker compose up -d` — talks to
// FileManager (5005) and Nasheed (5009) on localhost, and Identity (5001) to log in.
//
// Usage:
//   node bulk-import-songs.mjs --source "/path/to/mp3s" --email admin@example.com --password '...' --tenant-id ihsandev [--concurrency 5] [--limit 5] [--csv ./import-results.csv]
//   node bulk-import-songs.mjs --source "C:\Users\Hady Joukhadar\Music\رفع اناشيد" --email anashid@ihsandev.com --password @Test123 --tenant-id anashid --limit 1
//
// Requires Node 18+ (built-in fetch/FormData/Blob).

import fs from "node:fs";
import path from "node:path";

const GATEWAY_URL = "http://ihsandev.gleeze.com:5000";
const FILE_GROUP_PROJECT = 4;

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (!arg.startsWith("--")) continue;
    const key = arg.slice(2);
    const next = argv[i + 1];
    if (next === undefined || next.startsWith("--")) {
      args[key] = true;
    } else {
      args[key] = next;
      i++;
    }
  }
  return args;
}

function csvEscape(value) {
  const s = String(value ?? "");
  if (s.includes(",") || s.includes('"') || s.includes("\n")) {
    return `"${s.replace(/"/g, '""')}"`;
  }
  return s;
}

function loadExistingResults(csvPath) {
  const done = new Set();
  if (!fs.existsSync(csvPath)) return done;
  const lines = fs.readFileSync(csvPath, "utf8").split("\n").filter(Boolean);
  for (const line of lines.slice(1)) {
    const match = line.match(/^"?(.*?)"?,(success|failed),/);
    if (match && match[2] === "success") {
      done.add(match[1].replace(/""/g, '"'));
    }
  }
  return done;
}

async function login(email, password, tenantId) {
  const res = await fetch(`${GATEWAY_URL}/api/v1/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "x-tenant-id": tenantId,
    },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) {
    throw new Error(`Login failed: ${res.status} ${await res.text()}`);
  }
  const data = await res.json();
  if (!data.accessToken) {
    throw new Error(
      `Login response had no accessToken: ${JSON.stringify(data)}`,
    );
  }
  return data.accessToken;
}

async function uploadFile(filePath, fileName, token, tenantId) {
  const buffer = fs.readFileSync(filePath);
  const form = new FormData();
  form.append("file", new Blob([buffer], { type: "audio/mpeg" }), fileName);
  form.append("group", String(FILE_GROUP_PROJECT));

  const res = await fetch(`${GATEWAY_URL}/api/v1/filemanager/files`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
      "x-tenant-id": tenantId,
    },
    body: form,
  });
  if (!res.ok) {
    throw new Error(`Upload failed: ${res.status} ${await res.text()}`);
  }
  const data = await res.json();
  if (!data.id && !data.Id) {
    throw new Error(`Upload response had no Id: ${JSON.stringify(data)}`);
  }
  return data.id ?? data.Id;
}

async function createSong(title, fileId, token, tenantId) {
  const res = await fetch(`${GATEWAY_URL}/api/v1/songs`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
      "x-tenant-id": tenantId,
    },
    body: JSON.stringify({ title, fileId, artistId: null }),
  });
  if (!res.ok) {
    throw new Error(`Create song failed: ${res.status} ${await res.text()}`);
  }
  const data = await res.json();
  return data.id ?? data.Id;
}

async function processFile(sourceDir, fileName, token, tenantId) {
  const filePath = path.join(sourceDir, fileName);
  const title = path.basename(fileName, path.extname(fileName)).trim();
  const fileId = await uploadFile(filePath, fileName, token, tenantId);
  const songId = await createSong(title, fileId, token, tenantId);
  return { fileId, songId };
}

async function runWithConcurrency(items, limit, worker) {
  const results = new Array(items.length);
  let nextIndex = 0;

  async function runNext() {
    while (nextIndex < items.length) {
      const currentIndex = nextIndex++;
      results[currentIndex] = await worker(items[currentIndex], currentIndex);
    }
  }

  const workers = Array.from(
    { length: Math.min(limit, items.length) },
    runNext,
  );
  await Promise.all(workers);
  return results;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));

  if (!args.source || !args.email || !args.password || !args["tenant-id"]) {
    console.error(
      "Usage: node bulk-import-songs.mjs --source <folder> --email <email> --password <password> --tenant-id <tenantId> [--concurrency 5] [--limit N] [--csv ./import-results.csv]",
    );
    process.exit(1);
  }

  const sourceDir = args.source;
  const concurrency = Number(args.concurrency ?? 5);
  const limit = args.limit ? Number(args.limit) : undefined;
  const csvPath = args.csv ?? path.join(process.cwd(), "import-results.csv");

  if (!fs.existsSync(sourceDir) || !fs.statSync(sourceDir).isDirectory()) {
    console.error(`Source folder not found: ${sourceDir}`);
    process.exit(1);
  }

  let files = fs
    .readdirSync(sourceDir, { withFileTypes: true })
    .filter((entry) => entry.isFile() && /\.mp3$/i.test(entry.name))
    .map((entry) => entry.name);

  const alreadyDone = loadExistingResults(csvPath);
  const skipped = files.filter((f) => alreadyDone.has(f)).length;
  files = files.filter((f) => !alreadyDone.has(f));

  if (limit) files = files.slice(0, limit);

  console.log(
    `Found ${files.length} file(s) to import (${skipped} already succeeded previously, skipped).`,
  );
  if (files.length === 0) {
    console.log("Nothing to do.");
    return;
  }

  console.log("Logging in...");
  const token = await login(args.email, args.password, args["tenant-id"]);
  console.log("Logged in.");

  const csvIsNew = !fs.existsSync(csvPath);
  const csvStream = fs.createWriteStream(csvPath, { flags: "a" });
  if (csvIsNew) csvStream.write("fileName,status,fileId,songId,error\n");

  let successCount = 0;
  let failureCount = 0;
  let processed = 0;

  await runWithConcurrency(files, concurrency, async (fileName) => {
    try {
      const { fileId, songId } = await processFile(
        sourceDir,
        fileName,
        token,
        args["tenant-id"],
      );
      successCount++;
      csvStream.write(`${csvEscape(fileName)},success,${fileId},${songId},\n`);
    } catch (err) {
      failureCount++;
      csvStream.write(
        `${csvEscape(fileName)},failed,,,${csvEscape(err.message)}\n`,
      );
      console.error(`FAILED: ${fileName} — ${err.message}`);
    } finally {
      processed++;
      if (processed % 25 === 0 || processed === files.length) {
        console.log(
          `Progress: ${processed}/${files.length} (success: ${successCount}, failed: ${failureCount})`,
        );
      }
    }
  });

  csvStream.end();

  console.log("\nDone.");
  console.log(`  Success: ${successCount}`);
  console.log(`  Failed:  ${failureCount}`);
  console.log(`  Results: ${csvPath}`);
  if (failureCount > 0) {
    console.log("  Re-run the same command to retry only the failed files.");
  }
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
