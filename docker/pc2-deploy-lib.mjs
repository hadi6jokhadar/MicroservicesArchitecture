// Shared PC2-deploy logic used by deploy-pc2.mjs, deploy-changed.mjs, and
// build-and-deploy-changed.mjs. Reads PC2_SSH_HOST + PC2_REPO_PATH from .env and SSHes in to run
// `git pull` + `docker compose pull` + `docker compose up -d` for the given services (or every
// service if the list is empty).
//
// The `git pull` step exists because docker-compose.yml (and anything else tracked, unlike the
// gitignored appsettings.Docker.json files) only reaches PC2's checkout through git — pulling new
// images was never enough on its own for a compose-level change (new deploy.resources.limits, a
// new profile, a new service block, etc.), since PC2's on-disk docker-compose.yml stayed whatever
// it was at the last manual pull. See Doc/DOCKER_DEPLOYMENT_GUIDE.md's stale-config pitfalls for
// the appsettings.Docker.json version of this same class of bug.
//
// Requires PC2_SSH_HOST to already work as a plain `ssh <PC2_SSH_HOST>` (i.e. already set up in
// this machine's ~/.ssh/config with a User + IdentityFile, exactly like the "SSH connection" used
// throughout Doc/DOCKER_DEPLOYMENT_GUIDE.md). If PC2's IP ever changes, update .env's
// PC2_SSH_HOST (and your ~/.ssh/config entry) — nothing in this script hardcodes it.

import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

export function readPc2Config(repoRoot) {
  const envPath = path.join(repoRoot, '.env');
  if (!existsSync(envPath)) {
    console.error(`Missing ${envPath}`);
    console.error('Copy .env.example to .env and set PC2_SSH_HOST + PC2_REPO_PATH before running this.');
    process.exit(1);
  }

  const envVars = Object.fromEntries(
    readFileSync(envPath, 'utf8')
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line && !line.startsWith('#') && line.includes('='))
      .map((line) => {
        const idx = line.indexOf('=');
        return [line.slice(0, idx).trim(), line.slice(idx + 1).trim()];
      })
  );

  const { PC2_SSH_HOST, PC2_REPO_PATH } = envVars;
  if (!PC2_SSH_HOST || !PC2_REPO_PATH) {
    console.error('Set PC2_SSH_HOST and PC2_REPO_PATH in .env before running this — see .env.example.');
    process.exit(1);
  }

  return { PC2_SSH_HOST, PC2_REPO_PATH };
}

export function deployToPc2(repoRoot, services) {
  const { PC2_SSH_HOST, PC2_REPO_PATH } = readPc2Config(repoRoot);
  const target = services.length > 0 ? services.join(' ') : '';

  // macOS non-interactive SSH can't reach Docker Desktop's Keychain-backed credential helper —
  // point Docker at a temporary, credential-free config instead (every image here is public, so
  // no real credentials are needed for `pull`). See Doc/DOCKER_DEPLOYMENT_GUIDE.md, "SSH + Docker
  // Hub credential workaround".
  const remoteCommand = [
    'mkdir -p /tmp/docker-nocreds/cli-plugins',
    'ln -sf /Applications/Docker.app/Contents/Resources/cli-plugins/docker-compose /tmp/docker-nocreds/cli-plugins/docker-compose',
    `echo '{"credsStore":""}' > /tmp/docker-nocreds/config.json`,
    'export DOCKER_CONFIG=/tmp/docker-nocreds',
    `cd '${PC2_REPO_PATH}'`,
    'git pull',
    `/usr/local/bin/docker compose pull ${target}`,
    `/usr/local/bin/docker compose up -d ${target}`,
  ].join(' && ');

  console.log(`\nDeploying to PC2 (${PC2_SSH_HOST}): ${target || '(all services)'}\n`);

  try {
    // execFileSync passes each argv element directly to the ssh binary with no local shell
    // involved — critical on Windows (PC1), where execSync's default cmd.exe would otherwise
    // mangle the JSON literal's double quotes embedded inside remoteCommand (mismatched with
    // cmd.exe's own quoting rules, which differ from POSIX shells). ssh itself forwards
    // remoteCommand to PC2's bash unchanged, where the single/double quotes are exactly what
    // bash expects.
    execFileSync('ssh', [PC2_SSH_HOST, remoteCommand], { stdio: 'inherit' });
    console.log('\nDeploy complete.');
  } catch (error) {
    console.error('\nDeploy failed:', error.message);
    process.exit(1);
  }
}
