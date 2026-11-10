#!/usr/bin/env node
import { execFileSync } from 'node:child_process';
import { appendFileSync } from 'node:fs';
import { CoverageError, selectBaselineRun } from './coverage-lib.mjs';

const ARTIFACT = 'coverage-summary';
const CONFIG_AT_BASE = '.github/coverage/coverage.config.json';

function output(values) {
  const lines = Object.entries(values).map(([key, value]) => `${key}=${value}`).join('\n');
  if (process.env.GITHUB_OUTPUT) {
    appendFileSync(process.env.GITHUB_OUTPUT, `${lines}\n`);
  }
  process.stdout.write(`${lines}\n`);
}

async function api(path) {
  const response = await fetch(`${process.env.GITHUB_API_URL ?? 'https://api.github.com'}/repos/${process.env.GITHUB_REPOSITORY}${path}`, {
    headers: {
      Authorization: `Bearer ${process.env.GITHUB_TOKEN}`,
      Accept: 'application/vnd.github+json',
      'X-GitHub-Api-Version': '2022-11-28',
    },
  });
  if (!response.ok) {
    throw new CoverageError(`GitHub API ${path} failed: ${response.status} ${response.statusText}`);
  }
  return response.json();
}

function baseHasCoverageGate(baseSha) {
  try {
    execFileSync('git', ['cat-file', '-e', `${baseSha}^{commit}`], { stdio: 'ignore' });
  } catch {
    throw new CoverageError(`Target commit ${baseSha} is unavailable; refusing to bootstrap. Fetch the target history and retry.`);
  }
  try {
    execFileSync('git', ['cat-file', '-e', `${baseSha}:${CONFIG_AT_BASE}`], { stdio: 'ignore' });
    return true;
  } catch {
    return false;
  }
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

try {
  const baseSha = process.env.BASE_SHA;
  const branch = process.env.BASE_REF ?? 'main';
  const workflow = process.env.BASELINE_WORKFLOW ?? 'build-test.yml';
  const deadline = Date.now() + Number(process.env.BASELINE_WAIT_MINUTES ?? 30) * 60_000;
  if (!baseSha || !process.env.GITHUB_TOKEN || !process.env.GITHUB_REPOSITORY) {
    throw new CoverageError('BASE_SHA, GITHUB_TOKEN and GITHUB_REPOSITORY are required to resolve the baseline.');
  }

  if (!baseHasCoverageGate(baseSha)) {
    process.stdout.write(`::warning::Target commit ${baseSha} predates the coverage gate; no baseline exists. This is only expected for the PR that introduces the gate.\n`);
    output({ bootstrap: 'true', 'run-id': '' });
  } else {
    for (;;) {
      const { workflow_runs: runs } = await api(
        `/actions/workflows/${workflow}/runs?head_sha=${baseSha}&per_page=100`,
      );
      const selection = selectBaselineRun(runs, baseSha, branch);
      if (selection.status === 'ready') {
        const { artifacts } = await api(`/actions/runs/${selection.run.id}/artifacts?per_page=100`);
        const artifact = artifacts.find((candidate) => candidate.name === ARTIFACT);
        if (!artifact || artifact.expired) {
          throw new CoverageError(`The baseline run ${selection.run.id} for ${baseSha} has no usable '${ARTIFACT}' artifact (${artifact ? 'expired' : 'not found'}). Re-run the ${workflow} workflow on ${branch} for that commit, or update the PR branch.`);
        }
        output({ bootstrap: 'false', 'run-id': selection.run.id });
        break;
      }
      if (selection.status === 'pending' && Date.now() < deadline) {
        process.stdout.write(`Baseline run ${selection.run.id} for ${baseSha} is still ${selection.run.status}; waiting...\n`);
        await sleep(30_000);
        continue;
      }
      const reason = {
        missing: `No ${workflow} run on ${branch} was found for the target commit ${baseSha}. Re-run it (workflow_dispatch on ${branch}) or update the PR branch onto a commit that has been built.`,
        pending: `The baseline run for ${baseSha} did not finish within the wait window.`,
        failed: `The ${workflow} run on ${branch} for ${baseSha} did not succeed, so it has no trustworthy baseline.`,
      }[selection.status];
      throw new CoverageError(reason);
    }
  }
} catch (error) {
  process.stderr.write(`::error::${error instanceof CoverageError ? error.message : (error?.stack ?? String(error))}\n`);
  process.exitCode = 1;
}
