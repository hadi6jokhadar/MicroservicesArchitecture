#!/usr/bin/env node
// The one-command version of the routine deploy flow: detects changed services once, builds +
// pushes them to Docker Hub, then immediately deploys that exact same list to PC2 (pull + up -d).
// Detecting once and reusing the list (rather than build-changed then deploy-changed as two
// separate invocations) avoids any risk of the detected set drifting if files change on disk
// between the two steps.
//
// Invoke via Nx: `nx run docker:build-deploy-changed` (from MicroservicesArchitecture-Web),
// or directly: `node docker/build-and-deploy-changed.mjs` (from this repo's root).

import { execSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { detectChangedServices } from './detect-changed-services.mjs';
import { deployToPc2 } from './pc2-deploy-lib.mjs';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const backendRoot = path.resolve(scriptDir, '..');

const services = detectChangedServices(backendRoot);

if (services.length === 0) {
  console.log(
    'No changed files detected in either repo (checked uncommitted changes, then the last commit). Nothing to build or deploy.'
  );
  process.exit(0);
}

const envPath = path.join(backendRoot, '.env');
if (!existsSync(envPath)) {
  console.error(`Missing ${envPath}`);
  console.error('Copy .env.example to .env and set DOCKERHUB_USERNAME, PC2_SSH_HOST, and PC2_REPO_PATH before running this.');
  process.exit(1);
}

console.log(`Changed services detected: ${services.join(', ')}`);

try {
  execSync(`docker compose build --push ${services.join(' ')}`, { cwd: backendRoot, stdio: 'inherit' });
} catch (error) {
  console.error('\nBuild/push failed:', error.message);
  process.exit(1);
}

deployToPc2(backendRoot, services);
