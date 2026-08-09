#!/usr/bin/env node
/**
 * Interactive release helper.
 *
 * Bumps NoMoreEquals.csproj <Version>, updates Changelog in NoMoreEquals.json,
 * then: commit → push → tag → push tag (CI builds + updates repo.json).
 *
 * Usage:
 *   node tools/release.mjs
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
const VERSION_RE = /^v?\d+(\.\d+){2,3}$/;

const rl = createInterface({ input: process.stdin, output: process.stdout });

function ask(prompt) {
  return new Promise((resolveAsk) => rl.question(prompt, resolveAsk));
}

function fail(msg) {
  console.error(`\nError: ${msg}`);
  rl.close();
  process.exit(1);
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

function git(args) {
  const result = spawnSync('git', args, {
    cwd: ROOT,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  if (result.status !== 0) {
    fail(`git ${args.join(' ')}\n${result.stderr || result.stdout || `exit ${result.status}`}`);
  }
  return result;
}

async function main() {
  console.log('NoMoreEquals release helper\n');

  const currentVersion = readCsprojVersion();
  const versionInput = (await ask(`New version [${currentVersion}]: `)).trim();
  const newVersion = versionInput || currentVersion;
  if (!VERSION_RE.test(newVersion)) {
    fail(`Invalid version "${newVersion}": need 3 or 4 numeric segments, e.g. 0.1.16 or 0.1.16.1`);
  }
  const version = newVersion.replace(/^v/i, '');

  const manifest = readManifest();
  const currentChangelog = typeof manifest.Changelog === 'string' ? manifest.Changelog : '';
  console.log(`\nCurrent changelog:\n${currentChangelog || '(empty)'}\n`);
  const changelogInput = await ask('New changelog (Enter to keep current): ');
  const newChangelog = changelogInput === '' ? currentChangelog : changelogInput;

  const tag = `v${version}`;
  const commitMsg = `chore: release ${tag}`;

  writeCsprojVersion(version);
  writeManifestChangelog(newChangelog);
  console.log('\nUpdated csproj + manifest.');

  git(['add', 'NoMoreEquals/NoMoreEquals.csproj', 'NoMoreEquals/NoMoreEquals.json']);
  git(['commit', '-m', commitMsg]);
  git(['push', 'origin', `HEAD:${DEFAULT_BRANCH}`]);
  git(['tag', tag]);
  git(['push', 'origin', tag]);

  console.log(`\nDone. Watch the Release workflow for ${tag}.`);
  rl.close();
}

main().catch((err) => {
  console.error(err);
  rl.close();
  process.exit(1);
});
