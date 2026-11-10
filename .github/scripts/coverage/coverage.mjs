#!/usr/bin/env node
import { execFileSync } from 'node:child_process';
import { appendFileSync, existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import {
  CoverageError,
  changedLineCoverage,
  compareSummaries,
  mergeReports,
  parseCobertura,
  parseUnifiedDiff,
  renderCobertura,
  renderComparisonMarkdown,
  renderSummaryMarkdown,
  sha256,
  summarise,
} from './coverage-lib.mjs';

function options(args) {
  const parsed = {};
  for (let index = 0; index < args.length; index += 1) {
    const argument = args[index];
    if (!argument.startsWith('--')) {
      throw new CoverageError(`Unexpected argument: ${argument}`);
    }
    const name = argument.slice(2);
    const value = args[index + 1];
    if (value === undefined || value.startsWith('--')) {
      throw new CoverageError(`Missing value for --${name}`);
    }
    (parsed[name] ??= []).push(value);
    index += 1;
  }
  return parsed;
}

function single(parsed, name, fallback) {
  const value = parsed[name]?.[0] ?? fallback;
  if (value === undefined) {
    throw new CoverageError(`--${name} is required.`);
  }
  return value;
}

function findReports(directory) {
  if (!existsSync(directory)) {
    throw new CoverageError(`Coverage input directory not found: ${directory}`);
  }
  const found = [];
  const walk = (path) => {
    for (const name of readdirSync(path)) {
      const full = join(path, name);
      if (statSync(full).isDirectory()) {
        walk(full);
      } else if (name.endsWith('.cobertura.xml')) {
        found.push(full);
      }
    }
  };
  walk(directory);
  return found;
}

function writeStepSummary(markdown) {
  if (process.env.GITHUB_STEP_SUMMARY) {
    appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${markdown}\n`);
  }
}

function merge(parsed) {
  const config = JSON.parse(readFileSync(single(parsed, 'config', '.github/coverage/coverage.config.json'), 'utf8'));
  const runsettings = readFileSync(single(parsed, 'runsettings', 'coverage.runsettings'), 'utf8');
  const reportPaths = (parsed.input ?? []).flatMap(findReports);
  const minimum = Number(single(parsed, 'min-reports', '1'));
  if (reportPaths.length < minimum) {
    throw new CoverageError(`Expected at least ${minimum} Cobertura report(s) but found ${reportPaths.length}.`);
  }
  const reports = reportPaths.map((path) => parseCobertura(readFileSync(path, 'utf8'), path, config));
  const merged = mergeReports(reports);

  let changed = null;
  const diffBase = parsed['diff-base']?.[0];
  if (diffBase) {
    const diff = execFileSync('git', ['diff', '--unified=0', '--no-color', diffBase, 'HEAD', '--', 'src'], {
      encoding: 'utf8',
      maxBuffer: 256 * 1024 * 1024,
    });
    changed = changedLineCoverage(merged, parseUnifiedDiff(diff, config.excludePathPatterns));
  }

  const summary = summarise(merged, {
    commit: single(parsed, 'sha', 'unknown'),
    generatedAt: new Date().toISOString(),
    reportCount: reportPaths.length,
    config: {
      configSha: sha256(JSON.stringify(config)),
      runsettingsSha: sha256(runsettings.replaceAll('\r\n', '\n')),
      excludePathPatterns: [...config.excludePathPatterns].sort(),
    },
    ...(changed ? { changed } : {}),
  });
  if (summary.totals.lines.total === 0 || summary.totals.branches.total === 0) {
    throw new CoverageError('The merged coverage report has no line or branch data.');
  }

  const output = single(parsed, 'output');
  mkdirSync(output, { recursive: true });
  writeFileSync(join(output, 'coverage-summary.json'), `${JSON.stringify(summary, null, 2)}\n`);
  writeFileSync(join(output, 'coverage.cobertura.xml'), renderCobertura(summary, merged));
  const markdown = renderSummaryMarkdown(summary);
  writeFileSync(join(output, 'coverage-summary.md'), markdown);
  writeStepSummary(markdown);
  process.stdout.write(markdown);
}

function compare(parsed) {
  const baseline = JSON.parse(readFileSync(single(parsed, 'baseline'), 'utf8'));
  const current = JSON.parse(readFileSync(single(parsed, 'current'), 'utf8'));
  const srcDirectory = single(parsed, 'src-dir', 'src');
  const existingProjects = readdirSync(srcDirectory)
    .filter((name) => statSync(join(srcDirectory, name)).isDirectory());
  const result = compareSummaries(baseline, current, { existingProjects });
  const markdown = renderComparisonMarkdown(result, {
    baselineSha: baseline.commit,
    currentSha: current.commit,
  });
  const output = parsed.output?.[0];
  if (output) {
    mkdirSync(dirname(output), { recursive: true });
    writeFileSync(output, markdown);
  }
  writeStepSummary(markdown);
  process.stdout.write(markdown);
  if (!result.passed) {
    process.exitCode = 1;
  }
}

try {
  const [command, ...rest] = process.argv.slice(2);
  const parsed = options(rest);
  if (command === 'merge') {
    merge(parsed);
  } else if (command === 'compare') {
    compare(parsed);
  } else {
    throw new CoverageError('Usage: coverage.mjs <merge|compare> [options]');
  }
} catch (error) {
  const message = error instanceof CoverageError ? error.message : (error?.stack ?? String(error));
  process.stderr.write(`::error::Coverage check failed: ${message}\n`);
  writeStepSummary(`## Coverage regression gate\n\n❌ **Failed:** ${message}\n`);
  process.exitCode = 1;
}
