#!/usr/bin/env node
// Rebuilds + pushes only the Docker images for services whose source actually changed,
// instead of rebuilding all 12 every time (build-push-all) or picking targets one by one.
//
// Change detection lives in detect-changed-services.mjs (shared with
// build-and-deploy-changed.mjs so the two scripts can't drift on file->service mapping).
// There's no persistent "last successfully pushed" marker, so if you commit without building
// and then commit again, only the latest commit's diff is considered — rebuild everything once
// (build-push-all) if you suspect something got missed.
//
// Invoke via Nx: `nx run docker:build-changed` (from MicroservicesArchitecture-Web),
// or directly: `node docker/build-changed.mjs` (from this repo's root).
//
// Note: this only builds and pushes to Docker Hub — it does NOT deploy to PC2. Run
// `nx run docker:deploy-changed` afterward (or use `nx run docker:build-deploy-changed` to do
// both in one step).

import { execSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { detectChangedServices } from './detect-changed-services.mjs';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const backendRoot = path.resolve(scriptDir, '..');

const services = detectChangedServices(backendRoot);

if (services.length === 0) {
  console.log(
    'No changed files detected in either repo (checked uncommitted changes, then the last commit). Nothing to build.'
  );
  process.exit(0);
}

const envPath = path.join(backendRoot, '.env');
if (!existsSync(envPath)) {
  console.error(`Missing ${envPath}`);
  console.error('Copy .env.example to .env and set DOCKERHUB_USERNAME before running this.');
  process.exit(1);
}

console.log(`Changed services detected: ${services.join(', ')}`);

try {
  execSync(`docker compose build --push ${services.join(' ')}`, { cwd: backendRoot, stdio: 'inherit' });
  console.log('\nDone. Run `nx run docker:deploy-changed` to deploy these to PC2.');
} catch (error) {
  console.error('\nBuild/push failed:', error.message);
  process.exit(1);
}
