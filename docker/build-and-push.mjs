#!/usr/bin/env node
// One-click build + push of every Docker image (8 .NET services + Gateway + AI + 3 frontend
// apps) to Docker Hub, driven entirely by docker-compose.yml (single source of truth for
// build context/Dockerfile/image name per service — nothing is duplicated here).
//
// Invoke via Nx: `nx run admin:docker-build-push` (from MicroservicesArchitecture-Web),
// or directly: `node docker/build-and-push.mjs` (from this repo's root).
//
// Requires: `docker login` already done once on this machine, and a `.env` file at this
// repo's root (MicroservicesArchitecture/.env) with DOCKERHUB_USERNAME set — see .env.example.

import { execSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, '..');

const envPath = path.join(repoRoot, '.env');
if (!existsSync(envPath)) {
  console.error(`Missing ${envPath}`);
  console.error('Copy .env.example to .env and set DOCKERHUB_USERNAME before running this.');
  process.exit(1);
}

function run(command) {
  console.log(`\n> ${command}\n`);
  execSync(command, { cwd: repoRoot, stdio: 'inherit' });
}

try {
  console.log('Building all images (backend services + frontend apps)...');
  run('docker compose build');

  console.log('\nPushing all images to Docker Hub...');
  run('docker compose push');

  console.log('\nDone. On PC2, run: docker compose pull && docker compose up -d');
} catch (error) {
  console.error('\nBuild/push failed:', error.message);
  process.exit(1);
}
