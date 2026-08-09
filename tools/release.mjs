#!/usr/bin/env node
/**
 * Interactive release helper.
 *
 * Bumps NoMoreEquals.csproj <Version>, updates Changelog in NoMoreEquals.json,
 * then: commit → push → tag → push tag (CI builds + updates repo.json).
 *
 * Usage:
 *   node tools/release.mjs
 *   node tools/release.mjs --dry-run
 */

import { readFileSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createInterface } from 'node:readline';
import { spawnSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(__dirname, '..');
const CSPROJ = resolve(ROOT, 'NoMoreEquals/NoMoreEquals.csproj');
const MANIFEST = resolve(ROOT, 'NoMoreEquals/NoMoreEquals.json');
const DEFAULT_BRANCH = 'master';
const DRY_RUN = process.argv.includes('--dry-run');

const rl = createInterface({ input: process.stdin, output: process.stdout });

function ask(prompt) {
  return new Promise((resolveAsk) => rl.question(prompt, resolveAsk));
}

function log(...args) {
  console.log(...args);
}

function fail(msg) {
  console.error(`\nError: ${msg}`);
  rl.close();
  process.exit(1);
}

/** Accept 3 or 4 numeric segments; strip optional leading v. */
function parseVersion(raw) {
  const input = String(raw ?? '').trim();
  if (!input) {
    console.error('Version is empty. Need 3 or 4 segments, e.g. 0.1.16 or 0.1.16.1');
    return null;
  }

  const stripped = input.replace(/^v/i, '');
  const parts = stripped.split('.');

  if (parts.length < 3 || parts.length > 4) {
    console.error(
      `Invalid version "${input}": need 3 or 4 segments, got ${parts.length}. ` +
        'Examples: 0.1.16  |  0.1.16.1',
    );
    return null;
  }

  for (let i = 0; i < parts.length; i++) {
    const part = parts[i];
    if (part === '' || !/^\d+$/.test(part)) {
      console.error(
        `Invalid version "${input}": segment #${i + 1} ("${part}") must be digits only.`,
      );
      return null;
    }
  }

  return stripped;
}

function readCsprojVersion() {
  const xml = readFileSync(CSPROJ, 'utf8');
  const m = xml.match(/<Version>([^<]+)<\/Version>/);
  if (!m) fail(`Could not find <Version> in ${CSPROJ}`);
  return m[1].trim();
}

function writeCsprojVersion(version) {
  const xml = readFileSync(CSPROJ, 'utf8');
  const next = xml.replace(/<Version>[^<]+<\/Version>/, `<Version>${version}</Version>`);
  if (next === xml) fail('Failed to replace <Version> in csproj (pattern not found).');
  writeFileSync(CSPROJ, next, 'utf8');
}

function readManifest() {
  return JSON.parse(readFileSync(MANIFEST, 'utf8'));
}

function writeManifestChangelog(changelog) {
  const data = readManifest();
  data.Changelog = changelog;
  writeFileSync(MANIFEST, `${JSON.stringify(data, null, 2)}\n`, 'utf8');
}

async function askVersion(label, current) {
  for (;;) {
    const raw = await ask(`${label} [${current}]: `);
    const trimmed = raw.trim();
    if (!trimmed) return current;
    const parsed = parseVersion(trimmed);
    if (parsed) return parsed;
  }
}

async function askChangelog(current) {
  log('\n--- Changelog ---');
  log('Current:');
  log(current ? current : '(empty)');
  log('');
  log('Enter new changelog. Multi-line OK.');
  log('Finish with a line that is only: .');
  log('Press Enter immediately (then .) to keep current.');
  log('Type CLEAR then . to wipe changelog.');
  log('');

  const lines = [];
  for (;;) {
    const line = await ask('> ');
    if (line === '.') break;
    lines.push(line);
  }

  if (lines.length === 0) return current;
  if (lines.length === 1 && lines[0] === 'CLEAR') return '';
  return lines.join('\n');
}

async function confirm(prompt) {
  const a = (await ask(`${prompt} [y/N]: `)).trim().toLowerCase();
  return a === 'y' || a === 'yes';
}

function git(args, { allowFail = false } = {}) {
  const display = `git ${args.join(' ')}`;
  if (DRY_RUN) {
    log(`[dry-run] ${display}`);
    return { status: 0, stdout: '', stderr: '' };
  }
  const result = spawnSync('git', args, {
    cwd: ROOT,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  if (result.status !== 0 && !allowFail) {
    fail(`${display}\n${result.stderr || result.stdout || `exit ${result.status}`}`);
  }
  return result;
}

async function main() {
  log('NoMoreEquals release helper');
  if (DRY_RUN) log('(dry-run: no file writes / no git mutations beyond reads)\n');
  else log('');

  const status = git(['status', '--porcelain']);
  if (!DRY_RUN && status.stdout.trim()) {
    log('Working tree is not clean:');
    log(status.stdout);
    if (!(await confirm('Continue anyway? Unrelated changes will NOT be auto-staged'))) {
      rl.close();
      process.exit(0);
    }
    log('');
  }

  // --- 1) Version (csproj) ---
  const currentVersion = readCsprojVersion();
  log('File 1/2: NoMoreEquals/NoMoreEquals.csproj <Version>');
  const newVersion = await askVersion('New version', currentVersion);

  // --- 2) Changelog (manifest) ---
  const manifest = readManifest();
  const currentChangelog = typeof manifest.Changelog === 'string' ? manifest.Changelog : '';
  log('\nFile 2/2: NoMoreEquals/NoMoreEquals.json Changelog');
  const newChangelog = await askChangelog(currentChangelog);

  const tag = `v${newVersion}`;
  const commitMsg = `chore: release ${tag}`;

  log('\n--- Plan ---');
  log(`  Version:   ${currentVersion} → ${newVersion}`);
  log(`  Tag:       ${tag}`);
  log(`  Commit:    ${commitMsg}`);
  log('  Changelog:');
  log(
    newChangelog
      ? newChangelog
          .split('\n')
          .map((l) => `    ${l}`)
          .join('\n')
      : '    (empty)',
  );
  log('');
  log('Then: git push → git tag → git push origin <tag>');
  log(`CI will publish the release and update repo.json on ${DEFAULT_BRANCH}.`);
  log('');

  if (!(await confirm('Apply and release?'))) {
    log('Aborted.');
    rl.close();
    process.exit(0);
  }

  if (!DRY_RUN) {
    writeCsprojVersion(newVersion);
    writeManifestChangelog(newChangelog);
    log('\nUpdated csproj + manifest.');
  } else {
    log('\n[dry-run] Would update csproj + manifest.');
  }

  git(['add', 'NoMoreEquals/NoMoreEquals.csproj', 'NoMoreEquals/NoMoreEquals.json']);
  git(['commit', '-m', commitMsg]);
  git(['push', 'origin', `HEAD:${DEFAULT_BRANCH}`]);
  git(['tag', tag]);
  git(['push', 'origin', tag]);

  log(`\nDone. Watch the Release workflow for ${tag}.`);
  rl.close();
}

main().catch((err) => {
  console.error(err);
  rl.close();
  process.exit(1);
});
