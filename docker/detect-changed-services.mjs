// Shared change-detection logic used by build-changed.mjs and build-and-deploy-changed.mjs —
// kept in one place so the two scripts can never drift on which files map to which service.
//
// Change detection: uncommitted changes (unstaged + staged + untracked) in both repos first;
// if nothing is uncommitted, falls back to the last commit's diff.

import { execSync } from 'node:child_process';
import path from 'node:path';

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

export function detectChangedServices(backendRoot) {
  const frontendRoot = path.resolve(backendRoot, '..', 'MicroservicesArchitecture-Web');
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
    else if (file.startsWith('src/Services/Backup/')) services.add('backup');
    else if (file.startsWith('src/Gateway/')) services.add('gateway');
    else if (file.startsWith('src/Shared/ihsandev_shared/')) services.add('ai');
    else if (file.startsWith('src/Shared/')) {
      // A .NET shared library changed — every .NET service + Gateway consumes these.
      ['identity', 'tenant', 'notification', 'filemanager', 'translation', 'category', 'nasheed', 'backup', 'gateway'].forEach(
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

  return [...services];
}
