#!/usr/bin/env node
// Deploys already-pushed images to PC2: `docker compose pull` + `docker compose up -d` for the
// given services, or every service if none are given.
//
// Invoke via Nx: `nx run docker:deploy-all` (from MicroservicesArchitecture-Web),
// or directly: `node docker/deploy-pc2.mjs [service...]` (from this repo's root).

import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { deployToPc2 } from './pc2-deploy-lib.mjs';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const backendRoot = path.resolve(scriptDir, '..');

deployToPc2(backendRoot, process.argv.slice(2));
