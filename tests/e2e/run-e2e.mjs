import { mkdirSync, mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

// A new database and media directory per invocation; never touch a developer's demo database.
const state = mkdtempSync(join(tmpdir(), 'medical-browser-'));
mkdirSync(join(state, 'photos'));
const result = spawnSync(process.execPath, [fileURLToPath(new URL('node_modules/playwright/cli.js', import.meta.url)), 'test', ...process.argv.slice(2)],
  { cwd: fileURLToPath(new URL('.', import.meta.url)), stdio: 'inherit', env: { ...process.env, MEDICAL_E2E_STATE: state } });
process.exitCode = result.status ?? 1;
