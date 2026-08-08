#!/usr/bin/env node
// Deploys to PC2 whatever build-changed.mjs would currently detect as changed — use this after
// `nx run docker:build-changed` has finished pushing. Re-runs the same git-diff-based detection,
// so make sure nothing changed on disk between the build and this deploy (if it did, prefer
// `nx run docker:build-deploy-changed` instead, which detects once and reuses that same list).
//
// Invoke via Nx: `nx run docker:deploy-changed` (from MicroservicesArchitecture-Web),
// or directly: `node docker/deploy-changed.mjs` (from this repo's root).

import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { detectChangedServices } from './detect-changed-services.mjs';
import { deployToPc2 } from './pc2-deploy-lib.mjs';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const backendRoot = path.resolve(scriptDir, '..');

const services = detectChangedServices(backendRoot);

if (services.length === 0) {
  console.log(
    'No changed files detected in either repo (checked uncommitted changes, then the last commit). Nothing to deploy.'
  );
  process.exit(0);
}

console.log(`Changed services detected: ${services.join(', ')}`);
deployToPc2(backendRoot, services);
