#!/usr/bin/env node
// Rebuilds + pushes only the Docker images for services whose source actually changed,
// instead of rebuilding all 12 every time (build-push-all) or picking targets one by one.
//
// Change detection: uncommitted changes (unstaged + staged + untracked) in both repos first;
// if nothing is uncommitted, falls back to the last commit's diff, so "commit then build"
// still works. There's no persistent "last successfully pushed" marker, so if you commit
// without building and then commit again, only the latest commit's diff is considered —
// rebuild everything once (build-push-all) if you suspect something got missed.
//
// Invoke via Nx: `nx run docker:build-changed` (from MicroservicesArchitecture-Web),
// or directly: `node docker/build-changed.mjs` (from this repo's root).

import { execSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const backendRoot = path.resolve(scriptDir, '..');
const frontendRoot = path.resolve(backendRoot, '..', 'MicroservicesArchitecture-Web');

function changedFiles(repoRoot) {
  const run = (cmd) => {
    try {
      return execSync(cmd, { cwd: repoRoot, encoding: 'utf8' })
        .split('\n')
        .map((line) => line.trim())
        .filter(Boolean);
    } catch {
      return [];
    }
  };
  let files = [
    ...run('git diff --name-only HEAD'),
    ...run('git diff --name-only --cached'),
    ...run('git ls-files --others --exclude-standard'),
  ];
  if (files.length === 0) {
    files = run('git diff --name-only HEAD~1 HEAD');
  }
  return [...new Set(files)];
}

const backendChanges = changedFiles(backendRoot);
const frontendChanges = changedFiles(frontendRoot);

const services = new Set();

for (const file of backendChanges) {
  if (file.startsWith('src/Services/Identity/')) services.add('identity');
  else if (file.startsWith('src/Services/Tenant/')) services.add('tenant');
  else if (file.startsWith('src/Services/Notification/')) services.add('notification');
  else if (file.startsWith('src/Services/FileManager/')) services.add('filemanager');
  else if (file.startsWith('src/Services/Translation/')) services.add('translation');
  else if (file.startsWith('src/Services/Category/')) services.add('category');
  else if (file.startsWith('src/Services/AI/')) services.add('ai');
  else if (file.startsWith('src/Apps/Nasheed/')) services.add('nasheed');
  else if (file.startsWith('src/Gateway/')) services.add('gateway');
  else if (file.startsWith('src/Shared/ihsandev_shared/')) services.add('ai');
  else if (file.startsWith('src/Shared/')) {
    // A .NET shared library changed — every .NET service + Gateway consumes these.
    ['identity', 'tenant', 'notification', 'filemanager', 'translation', 'category', 'nasheed', 'gateway'].forEach(
      (s) => services.add(s)
    );
  }
}

for (const file of frontendChanges) {
  if (file.startsWith('apps/admin/')) services.add('admin');
  else if (file.startsWith('apps/nasheed/admin/')) services.add('nasheed-admin');
  else if (file.startsWith('apps/nasheed/web/')) services.add('nasheed-web');
  else if (file.startsWith('libs/')) {
    // A shared frontend lib changed — every app consumes it.
    ['admin', 'nasheed-admin', 'nasheed-web'].forEach((s) => services.add(s));
  }
}

if (services.size === 0) {
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

const serviceList = [...services];
console.log(`Changed services detected: ${serviceList.join(', ')}`);

try {
  execSync(`docker compose build --push ${serviceList.join(' ')}`, { cwd: backendRoot, stdio: 'inherit' });
  console.log('\nDone. On PC2, run: docker compose pull && docker compose up -d');
} catch (error) {
  console.error('\nBuild/push failed:', error.message);
  process.exit(1);
}
