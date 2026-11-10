import assert from 'node:assert/strict';
import { execFileSync, spawn } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const script = join(dirname(fileURLToPath(import.meta.url)), 'resolve-baseline.mjs');

function git(cwd, ...args) {
  return execFileSync('git', ['-c', 'user.email=t@example.test', '-c', 'user.name=t', ...args], { cwd, encoding: 'utf8' }).trim();
}

// Builds a repository whose first commit has no coverage config and whose second commit has it.
function repository() {
  const cwd = mkdtempSync(join(tmpdir(), 'baseline-repo-'));
  git(cwd, 'init', '-q');
  writeFileSync(join(cwd, 'a.txt'), 'a');
  git(cwd, 'add', '.');
  git(cwd, 'commit', '-q', '-m', 'before the gate');
  const before = git(cwd, 'rev-parse', 'HEAD');
  mkdirSync(join(cwd, '.github', 'coverage'), { recursive: true });
  writeFileSync(join(cwd, '.github', 'coverage', 'coverage.config.json'), '{"excludePathPatterns":[]}');
  git(cwd, 'add', '-f', '.');
  git(cwd, 'commit', '-q', '-m', 'with the gate');
  return { cwd, before, after: git(cwd, 'rev-parse', 'HEAD') };
}

async function run({ cwd, baseSha, runs, artifacts, extraEnv = {} }) {
  const server = createServer((request, response) => {
    response.setHeader('content-type', 'application/json');
    if (request.url.includes('/actions/workflows/')) {
      response.end(JSON.stringify({ workflow_runs: runs }));
    } else if (request.url.includes('/artifacts')) {
      response.end(JSON.stringify({ artifacts }));
    } else {
      response.statusCode = 404;
      response.end('{}');
    }
  });
  await new Promise((resolve) => server.listen(0, resolve));
  const outputFile = join(mkdtempSync(join(tmpdir(), 'baseline-out-')), 'output.txt');
  writeFileSync(outputFile, '');
  const child = spawn(process.execPath, [script], {
    cwd,
    env: {
      ...process.env,
      BASE_SHA: baseSha,
      BASE_REF: 'main',
      GITHUB_TOKEN: 'token',
      GITHUB_REPOSITORY: 'owner/repo',
      GITHUB_API_URL: `http://127.0.0.1:${server.address().port}`,
      GITHUB_OUTPUT: outputFile,
      BASELINE_WAIT_MINUTES: '0',
      ...extraEnv,
    },
  });
  let stdout = '';
  let stderr = '';
  child.stdout.on('data', (chunk) => { stdout += chunk; });
  child.stderr.on('data', (chunk) => { stderr += chunk; });
  const status = await new Promise((resolve) => child.on('close', resolve));
  server.close();
  return { status, stdout, stderr, output: readFileSync(outputFile, 'utf8') };
}

const successfulRun = (sha) => ({ id: 42, head_sha: sha, head_branch: 'main', event: 'push', status: 'completed', conclusion: 'success', created_at: '2026-01-01T00:00:00Z' });

test('fails closed when the target commit cannot be read instead of bootstrapping', async () => {
  const { cwd } = repository();
  const result = await run({ cwd, baseSha: '0000000000000000000000000000000000000000', runs: [], artifacts: [] });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /unavailable; refusing to bootstrap/);
  assert.doesNotMatch(result.output, /bootstrap=true/);
});

test('bootstraps (warns and passes) only when the target commit predates the coverage gate', async () => {
  const { cwd, before } = repository();
  const result = await run({ cwd, baseSha: before, runs: [], artifacts: [] });
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.output, /bootstrap=true/);
  assert.match(result.stdout, /::warning::.*predates the coverage gate/);
});

test('does not bootstrap once the target commit contains the gate: a missing baseline run fails', async () => {
  const { cwd, after } = repository();
  const result = await run({ cwd, baseSha: after, runs: [], artifacts: [] });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /No build-test.yml run on main was found for the target commit/);
});

test('resolves the exact run and artifact for the target commit', async () => {
  const { cwd, after } = repository();
  const result = await run({
    cwd,
    baseSha: after,
    runs: [successfulRun('someone-elses-commit'), successfulRun(after)],
    artifacts: [{ name: 'coverage-summary', expired: false }],
  });
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.output, /bootstrap=false/);
  assert.match(result.output, /run-id=42/);
});

test('fails when the baseline run belongs to a different commit', async () => {
  const { cwd, after } = repository();
  const result = await run({ cwd, baseSha: after, runs: [successfulRun('another-commit')], artifacts: [{ name: 'coverage-summary', expired: false }] });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /No build-test.yml run on main was found/);
});

test('fails clearly when the baseline artifact is expired or absent', async () => {
  const { cwd, after } = repository();
  const expired = await run({ cwd, baseSha: after, runs: [successfulRun(after)], artifacts: [{ name: 'coverage-summary', expired: true }] });
  assert.equal(expired.status, 1);
  assert.match(expired.stderr, /expired/);
  const absent = await run({ cwd, baseSha: after, runs: [successfulRun(after)], artifacts: [] });
  assert.equal(absent.status, 1);
  assert.match(absent.stderr, /not found/);
});

test('fails when the baseline run failed or has not finished', async () => {
  const { cwd, after } = repository();
  const failed = await run({ cwd, baseSha: after, runs: [{ ...successfulRun(after), conclusion: 'failure' }], artifacts: [] });
  assert.equal(failed.status, 1);
  assert.match(failed.stderr, /did not succeed/);
  const pending = await run({ cwd, baseSha: after, runs: [{ ...successfulRun(after), status: 'in_progress', conclusion: null }], artifacts: [] });
  assert.equal(pending.status, 1);
  assert.match(pending.stderr, /did not finish within the wait window/);
});

test('fails when the GitHub API cannot be queried', async () => {
  const { cwd, after } = repository();
  const result = await run({ cwd, baseSha: after, runs: [], artifacts: [], extraEnv: { GITHUB_API_URL: 'http://127.0.0.1:1' } });
  assert.equal(result.status, 1);
});
