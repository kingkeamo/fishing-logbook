import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import {
  CoverageError,
  changedLineCoverage,
  compareSummaries,
  mergeReports,
  normaliseSourceFile,
  parseCobertura,
  parseUnifiedDiff,
  renderComparisonMarkdown,
  selectBaselineRun,
  summarise,
} from './coverage-lib.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const config = { excludePathPatterns: ['/obj/', '.g.cs'] };

// A file is described as { path, lines: [[number, hits, 'c/t'?], ...] }.
function cobertura(files) {
  const classes = files.map((file, index) => {
    const lines = file.lines.map(([number, hits, branch]) => (branch
      ? `<line number="${number}" hits="${hits}" branch="True" condition-coverage="50% (${branch})" />`
      : `<line number="${number}" hits="${hits}" branch="False" />`)).join('');
    return `<class name="C${index}" filename="${file.path}"><methods><method name="M"><lines>${lines}</lines></method></methods><lines>${lines}</lines></class>`;
  }).join('');
  return `<?xml version="1.0"?><coverage line-rate="1" lines-valid="1" lines-covered="1"><packages><package name="p"><classes>${classes}</classes></package></packages></coverage>`;
}

function summaryOf(files, overrides = {}) {
  const merged = mergeReports([parseCobertura(cobertura(files), 'fixture', config)]);
  return summarise(merged, {
    commit: 'abc',
    generatedAt: '2026-01-01T00:00:00Z',
    config: { configSha: 'c', runsettingsSha: 'r', excludePathPatterns: ['/obj/', '.g.cs'] },
    ...overrides,
  });
}

// Four lines and four branches, half of each covered.
const baselineFiles = [
  { path: 'src/FishingLogBook.Api/A.cs', lines: [[1, 1], [2, 0], [3, 1, '1/2'], [4, 0, '1/2']] },
  { path: 'src/FishingLogBook.Web/B.cs', lines: [[1, 1], [2, 1]] },
];

test('normalises Windows and Linux absolute paths to the same project-relative key', () => {
  assert.deepEqual(normaliseSourceFile('src\\FishingLogBook.Api\\Sub\\A.cs'), { project: 'FishingLogBook.Api', key: 'FishingLogBook.Api/Sub/A.cs' });
  assert.deepEqual(normaliseSourceFile('/home/runner/work/r/r/src/FishingLogBook.Api/Sub/A.cs'), { project: 'FishingLogBook.Api', key: 'FishingLogBook.Api/Sub/A.cs' });
});

test('ignores generated, obj and non-production files', () => {
  assert.equal(normaliseSourceFile('src/FishingLogBook.Web/obj/Debug/X.cs', ['/obj/']), null);
  assert.equal(normaliseSourceFile('src/FishingLogBook.Web/X.g.cs', ['.g.cs']), null);
  assert.equal(normaliseSourceFile('tests/FishingLogBook.Web.Tests/T.cs'), null);
});

test('merges reports by taking the best hits and best branch coverage per line', () => {
  const first = parseCobertura(cobertura([{ path: 'src/FishingLogBook.Api/A.cs', lines: [[1, 0], [2, 1, '1/2']] }]), 'a', config);
  const second = parseCobertura(cobertura([{ path: 'src\\FishingLogBook.Api\\A.cs', lines: [[1, 5], [2, 0, '2/2']] }]), 'b', config);
  const summary = summarise(mergeReports([first, second]), {});
  assert.deepEqual(summary.totals.lines, { covered: 2, total: 2 });
  assert.deepEqual(summary.totals.branches, { covered: 2, total: 2 });
});

test('counts duplicate class entries for one line once', () => {
  const summary = summaryOf(baselineFiles);
  assert.deepEqual(summary.totals.lines, { covered: 4, total: 6 });
  assert.deepEqual(summary.totals.branches, { covered: 2, total: 4 });
  assert.deepEqual(Object.keys(summary.projects), ['FishingLogBook.Api', 'FishingLogBook.Web']);
});

test('rejects reports that are not Cobertura or contain no classes', () => {
  assert.throws(() => parseCobertura('<html/>', 'bad', config), CoverageError);
  assert.throws(() => parseCobertura('<coverage lines-valid="0"></coverage>', 'empty', config), /no classes/);
  assert.throws(
    () => parseCobertura('<coverage lines-valid="1"><class filename="src/FishingLogBook.Api/A.cs"><line number="x" hits="1"/></class></coverage>', 'bad', config),
    /malformed/,
  );
});

test('passes when coverage is unchanged', () => {
  const result = compareSummaries(summaryOf(baselineFiles), summaryOf(baselineFiles));
  assert.equal(result.passed, true);
  assert.deepEqual(result.failures, []);
});

test('passes when line and branch coverage increase', () => {
  const better = [
    { path: 'src/FishingLogBook.Api/A.cs', lines: [[1, 1], [2, 1], [3, 1, '2/2'], [4, 1, '2/2']] },
    baselineFiles[1],
  ];
  const result = compareSummaries(summaryOf(baselineFiles), summaryOf(better));
  assert.equal(result.passed, true);
  assert.ok(result.metrics.every((metric) => metric.delta > 0));
});

test('fails when only line coverage decreases', () => {
  const worse = [
    { path: 'src/FishingLogBook.Api/A.cs', lines: [[1, 1], [2, 0], [3, 0, '1/2'], [4, 0, '1/2']] },
    baselineFiles[1],
  ];
  const result = compareSummaries(summaryOf(baselineFiles), summaryOf(worse));
  assert.equal(result.passed, false);
  assert.match(result.failures.join('\n'), /Line coverage decreased/);
  assert.doesNotMatch(result.failures.join('\n'), /Branch coverage decreased/);
});

test('fails when only branch coverage decreases', () => {
  const worse = [
    { path: 'src/FishingLogBook.Api/A.cs', lines: [[1, 1], [2, 0], [3, 1, '0/2'], [4, 0, '1/2']] },
    baselineFiles[1],
  ];
  const result = compareSummaries(summaryOf(baselineFiles), summaryOf(worse));
  assert.equal(result.passed, false);
  assert.match(result.failures.join('\n'), /Branch coverage decreased/);
  assert.doesNotMatch(result.failures.join('\n'), /Line coverage decreased/);
});

test('uses unrounded values: a decrease that rounds to the same percentage still fails', () => {
  const big = (covered) => ({
    schemaVersion: 1,
    config: { configSha: 'c', runsettingsSha: 'r', excludePathPatterns: [] },
    totals: { lines: { covered, total: 100000 }, branches: { covered: 500, total: 1000 } },
    projects: {},
    files: {},
  });
  const result = compareSummaries(big(90000), big(89999));
  assert.equal(result.passed, false);
});

test('ignores floating point noise below the tolerance', () => {
  const summary = (covered, total) => ({
    schemaVersion: 1,
    config: { configSha: 'c', runsettingsSha: 'r', excludePathPatterns: [] },
    totals: { lines: { covered, total }, branches: { covered: 1, total: 2 } },
    projects: {},
    files: {},
  });
  assert.equal(compareSummaries(summary(1, 3), summary(1, 3)).passed, true);
  assert.equal(compareSummaries(summary(1, 3), summary(1, 3), { tolerance: 0 }).passed, true);
});

test('fails clearly for missing, zero or incompatible coverage data', () => {
  const good = summaryOf(baselineFiles);
  assert.throws(() => compareSummaries(good, { ...good, totals: { lines: { covered: 0, total: 0 }, branches: good.totals.branches } }), /lines coverage totals/);
  assert.throws(() => compareSummaries(good, { ...good, totals: undefined }), /totals are missing/);
  assert.throws(() => compareSummaries({ ...good, schemaVersion: 99 }, good), /unsupported coverage summary schema/);
  assert.throws(() => compareSummaries(good, null), CoverageError);
});

test('adding an untested production file lowers coverage and fails', () => {
  const withNewFile = [...baselineFiles, { path: 'src/FishingLogBook.Api/New.cs', lines: [[1, 0], [2, 0], [3, 0]] }];
  const result = compareSummaries(summaryOf(baselineFiles), summaryOf(withNewFile));
  assert.equal(result.passed, false);
  assert.deepEqual(result.addedFiles, ['FishingLogBook.Api/New.cs']);
});

test('adding a fully tested production file is allowed', () => {
  const withNewFile = [...baselineFiles, { path: 'src/FishingLogBook.Api/New.cs', lines: [[1, 3], [2, 3]] }];
  const result = compareSummaries(summaryOf(baselineFiles), summaryOf(withNewFile));
  assert.equal(result.passed, true);
  assert.deepEqual(result.addedFiles, ['FishingLogBook.Api/New.cs']);
});

test('removing a production file is compared on the remaining code', () => {
  const result = compareSummaries(summaryOf(baselineFiles), summaryOf([baselineFiles[0]]));
  assert.deepEqual(result.removedFiles, ['FishingLogBook.Web/B.cs']);
  assert.equal(result.passed, false, 'removing fully covered code lowers the rate and is still a regression');
});

test('a project that still exists but has no coverage data fails', () => {
  const current = summaryOf([baselineFiles[0]]);
  const result = compareSummaries(summaryOf(baselineFiles), current, { existingProjects: ['FishingLogBook.Api', 'FishingLogBook.Web'] });
  assert.match(result.failures.join('\n'), /FishingLogBook\.Web still exists under src\/ but has no coverage data/);
});

test('a project deleted from src is reported, not treated as missing data', () => {
  const current = summaryOf([baselineFiles[0]]);
  const result = compareSummaries(summaryOf(baselineFiles), current, { existingProjects: ['FishingLogBook.Api'] });
  assert.doesNotMatch(result.failures.join('\n'), /no coverage data/);
  assert.match(result.notes.join('\n'), /FishingLogBook\.Web was removed/);
});

test('highlights a project whose coverage decreases even when the overall result passes', () => {
  const baseline = summaryOf([
    { path: 'src/FishingLogBook.Api/A.cs', lines: [[1, 1], [2, 1], [3, 0], [4, 1, '1/2']] },
    { path: 'src/FishingLogBook.Web/B.cs', lines: [[1, 0], [2, 0], [3, 0], [4, 0, '0/2']] },
  ]);
  const current = summaryOf([
    { path: 'src/FishingLogBook.Api/A.cs', lines: [[1, 1], [2, 0], [3, 0], [4, 1, '0/2']] },
    { path: 'src/FishingLogBook.Web/B.cs', lines: [[1, 1], [2, 1], [3, 1], [4, 1, '2/2']] },
  ]);
  const result = compareSummaries(baseline, current);
  assert.equal(result.passed, true);
  assert.equal(result.projects.find((project) => project.name === 'FishingLogBook.Api').linesDecreased, true);
  assert.match(renderComparisonMarkdown(result), /FishingLogBook\.Api .*⚠️ decreased/);
});

test('fails when the coverage configuration or exclusions change', () => {
  const baseline = summaryOf(baselineFiles);
  const changedSettings = summaryOf(baselineFiles, { config: { configSha: 'c', runsettingsSha: 'other', excludePathPatterns: ['/obj/', '.g.cs'] } });
  assert.match(compareSummaries(baseline, changedSettings).failures.join('\n'), /configuration .* differs/);
  const moreExclusions = summaryOf(baselineFiles, { config: { configSha: 'c', runsettingsSha: 'r', excludePathPatterns: ['/obj/', '.g.cs', '/Api/'] } });
  assert.match(compareSummaries(baseline, moreExclusions).failures.join('\n'), /New coverage exclusions are not allowed: \/Api\//);
});

test('parses a unified diff and measures coverage of the changed lines', () => {
  const diff = [
    'diff --git a/src/FishingLogBook.Api/A.cs b/src/FishingLogBook.Api/A.cs',
    '--- a/src/FishingLogBook.Api/A.cs',
    '+++ b/src/FishingLogBook.Api/A.cs',
    '@@ -1,0 +1,2 @@',
    '+x',
    '+y',
    '@@ -9 +4 @@',
    '+z',
    'diff --git a/src/FishingLogBook.Api/Gone.cs b/src/FishingLogBook.Api/Gone.cs',
    '--- a/src/FishingLogBook.Api/Gone.cs',
    '+++ /dev/null',
    '@@ -1,3 +0,0 @@',
  ].join('\n');
  const changed = parseUnifiedDiff(diff, config.excludePathPatterns);
  assert.deepEqual([...changed.get('FishingLogBook.Api/A.cs')].sort(), [1, 2, 4]);
  assert.equal(changed.has('FishingLogBook.Api/Gone.cs'), false);
  const merged = mergeReports([parseCobertura(cobertura(baselineFiles), 'fixture', config)]);
  const coverage = changedLineCoverage(merged, changed);
  assert.deepEqual(coverage.lines, { covered: 1, total: 3 });
  assert.deepEqual(coverage.branches, { covered: 1, total: 2 });
});

test('selects the baseline run for exactly the target commit', () => {
  const run = (overrides) => ({ id: 1, head_sha: 'base', head_branch: 'main', event: 'push', status: 'completed', conclusion: 'success', created_at: '2026-01-01T00:00:00Z', ...overrides });
  assert.equal(selectBaselineRun([run({ head_sha: 'other' })], 'base').status, 'missing');
  assert.equal(selectBaselineRun([run({ head_branch: 'feature' })], 'base').status, 'missing');
  assert.equal(selectBaselineRun([run({ event: 'pull_request' })], 'base').status, 'missing');
  assert.equal(selectBaselineRun([run({ conclusion: 'failure' })], 'base').status, 'failed');
  assert.equal(selectBaselineRun([run({ status: 'in_progress', conclusion: null })], 'base').status, 'pending');
  const newest = selectBaselineRun([run({ id: 1 }), run({ id: 2, created_at: '2026-02-01T00:00:00Z' }), run({ id: 3, conclusion: 'failure', created_at: '2026-03-01T00:00:00Z' })], 'base');
  assert.deepEqual([newest.status, newest.run.id], ['ready', 2]);
});

function runCli(args, env = {}) {
  return spawnSync(process.execPath, [join(here, 'coverage.mjs'), ...args], { encoding: 'utf8', env: { ...process.env, GITHUB_STEP_SUMMARY: '', ...env } });
}

function writeReport(directory, name, files) {
  mkdirSync(directory, { recursive: true });
  writeFileSync(join(directory, `${name}.cobertura.xml`), cobertura(files));
}

function mergeInto(directory, files, sha) {
  writeReport(join(directory, 'raw'), 'one', files);
  const result = runCli([
    'merge', '--input', join(directory, 'raw'), '--output', join(directory, 'out'), '--sha', sha,
    '--config', join(here, '../../coverage/coverage.config.json'), '--runsettings', join(here, '../../../coverage.runsettings'),
  ]);
  assert.equal(result.status, 0, result.stderr);
  return join(directory, 'out', 'coverage-summary.json');
}

test('the CLI gate passes for equal coverage and fails with a non-zero exit for a regression', () => {
  const root = mkdtempSync(join(tmpdir(), 'coverage-cli-'));
  const srcDir = join(root, 'src');
  mkdirSync(join(srcDir, 'FishingLogBook.Api'), { recursive: true });
  mkdirSync(join(srcDir, 'FishingLogBook.Web'), { recursive: true });
  const baseline = mergeInto(join(root, 'baseline'), baselineFiles, 'main-sha');
  const same = mergeInto(join(root, 'same'), baselineFiles, 'pr-sha');
  const worse = mergeInto(join(root, 'worse'), [
    { path: 'src/FishingLogBook.Api/A.cs', lines: [[1, 0], [2, 0], [3, 1, '1/2'], [4, 0, '1/2']] },
    baselineFiles[1],
  ], 'pr-sha');

  const pass = runCli(['compare', '--baseline', baseline, '--current', same, '--src-dir', srcDir]);
  assert.equal(pass.status, 0, pass.stderr);
  assert.match(pass.stdout, /Result: PASS/);
  assert.match(pass.stdout, /\| Line coverage \|/);
  assert.match(pass.stdout, /\| Branch coverage \|/);

  const fail = runCli(['compare', '--baseline', baseline, '--current', worse, '--src-dir', srcDir]);
  assert.equal(fail.status, 1);
  assert.match(fail.stdout, /Result: FAIL/);
});

test('the CLI fails when no coverage reports exist, a report is invalid, or the baseline is unreadable', () => {
  const root = mkdtempSync(join(tmpdir(), 'coverage-cli-'));
  const common = ['--config', join(here, '../../coverage/coverage.config.json'), '--runsettings', join(here, '../../../coverage.runsettings')];
  mkdirSync(join(root, 'empty'), { recursive: true });
  const none = runCli(['merge', '--input', join(root, 'empty'), '--output', join(root, 'o1'), ...common]);
  assert.equal(none.status, 1);
  assert.match(none.stderr, /Expected at least 1 Cobertura report/);

  mkdirSync(join(root, 'bad'), { recursive: true });
  writeFileSync(join(root, 'bad', 'x.cobertura.xml'), '<coverage lines-valid="0"></coverage>');
  const invalid = runCli(['merge', '--input', join(root, 'bad'), '--output', join(root, 'o2'), ...common]);
  assert.equal(invalid.status, 1);

  const missing = runCli(['merge', '--input', join(root, 'does-not-exist'), '--output', join(root, 'o3'), ...common]);
  assert.equal(missing.status, 1);

  const noBaseline = runCli(['compare', '--baseline', join(root, 'nope.json'), '--current', join(root, 'nope.json'), '--src-dir', root]);
  assert.equal(noBaseline.status, 1);
});

test('the CLI requires every expected report when --min-reports is supplied', () => {
  const root = mkdtempSync(join(tmpdir(), 'coverage-cli-'));
  writeReport(join(root, 'raw'), 'only', baselineFiles);
  const result = runCli([
    'merge', '--input', join(root, 'raw'), '--output', join(root, 'out'), '--min-reports', '7',
    '--config', join(here, '../../coverage/coverage.config.json'), '--runsettings', join(here, '../../../coverage.runsettings'),
  ]);
  assert.equal(result.status, 1);
  assert.match(result.stderr, /at least 7/);
});

test('resolves coverlet file names that are relative to the report source root', () => {
  assert.deepEqual(
    normaliseSourceFile('FishingLogBook.Domain\\Trips\\Trip.cs', [], ['C:\\git\\fishing-logbook\\src\\']),
    { project: 'FishingLogBook.Domain', key: 'FishingLogBook.Domain/Trips/Trip.cs' },
  );
  assert.deepEqual(
    normaliseSourceFile('src/FishingLogBook.Api/A.cs', [], ['/home/runner/work/r/r/']),
    { project: 'FishingLogBook.Api', key: 'FishingLogBook.Api/A.cs' },
  );
  assert.equal(normaliseSourceFile('Features/T.cs', [], ['/home/runner/work/r/r/tests/FishingLogBook.Web.Tests/']), null);
  assert.equal(normaliseSourceFile('FishingLogBook.Web/obj/X.cs', ['/obj/'], ['/r/src/']), null);
});

test('reads the source roots declared in the report', () => {
  const xml = '<coverage lines-valid="1"><sources><source>C:\\repo\\src\\</source></sources><packages><package><classes>'
    + '<class name="C" filename="FishingLogBook.Domain\\A.cs"><lines><line number="1" hits="1" branch="False"/></lines></class>'
    + '</classes></package></packages></coverage>';
  const files = parseCobertura(xml, 'relative', config);
  assert.deepEqual([...files.keys()], ['FishingLogBook.Domain/A.cs']);
});

function twoProjectSource() {
  const root = mkdtempSync(join(tmpdir(), 'coverage-cli-'));
  const srcDir = join(root, 'src');
  mkdirSync(join(srcDir, 'FishingLogBook.Api'), { recursive: true });
  mkdirSync(join(srcDir, 'FishingLogBook.Web'), { recursive: true });
  return { root, srcDir };
}

test('the CLI gate fails on a branch-only regression', () => {
  const { root, srcDir } = twoProjectSource();
  const baseline = mergeInto(join(root, 'baseline'), baselineFiles, 'main-sha');
  const worse = mergeInto(join(root, 'worse'), [
    { path: 'src/FishingLogBook.Api/A.cs', lines: [[1, 1], [2, 0], [3, 1, '0/2'], [4, 0, '1/2']] },
    baselineFiles[1],
  ], 'pr-sha');
  const result = runCli(['compare', '--baseline', baseline, '--current', worse, '--src-dir', srcDir]);
  assert.equal(result.status, 1);
  assert.match(result.stdout, /Branch coverage decreased/);
  assert.doesNotMatch(result.stdout, /Line coverage decreased/);
});

test('the CLI gate passes when coverage increases', () => {
  const { root, srcDir } = twoProjectSource();
  const baseline = mergeInto(join(root, 'baseline'), baselineFiles, 'main-sha');
  const better = mergeInto(join(root, 'better'), [
    { path: 'src/FishingLogBook.Api/A.cs', lines: [[1, 1], [2, 1], [3, 1, '2/2'], [4, 1, '2/2']] },
    baselineFiles[1],
  ], 'pr-sha');
  const result = runCli(['compare', '--baseline', baseline, '--current', better, '--src-dir', srcDir]);
  assert.equal(result.status, 0, result.stderr);
});

test('the CLI gate fails when the coverage settings or exclusions differ from the baseline', () => {
  const { root, srcDir } = twoProjectSource();
  const baseline = mergeInto(join(root, 'baseline'), baselineFiles, 'main-sha');

  const settingsDirectory = join(root, 'changed-settings');
  writeReport(join(settingsDirectory, 'raw'), 'one', baselineFiles);
  const changedRunsettings = join(settingsDirectory, 'coverage.runsettings');
  writeFileSync(changedRunsettings, `${readFileSync(join(here, '../../../coverage.runsettings'), 'utf8')}<!-- edited -->`);
  const merged = runCli([
    'merge', '--input', join(settingsDirectory, 'raw'), '--output', join(settingsDirectory, 'out'), '--sha', 'pr-sha',
    '--config', join(here, '../../coverage/coverage.config.json'), '--runsettings', changedRunsettings,
  ]);
  assert.equal(merged.status, 0, merged.stderr);
  const settings = runCli(['compare', '--baseline', baseline, '--current', join(settingsDirectory, 'out', 'coverage-summary.json'), '--src-dir', srcDir]);
  assert.equal(settings.status, 1);
  assert.match(settings.stdout, /Coverage configuration .* differs from the baseline/);

  const exclusionDirectory = join(root, 'new-exclusion');
  writeReport(join(exclusionDirectory, 'raw'), 'one', baselineFiles);
  const changedConfig = join(exclusionDirectory, 'coverage.config.json');
  const original = JSON.parse(readFileSync(join(here, '../../coverage/coverage.config.json'), 'utf8'));
  writeFileSync(changedConfig, JSON.stringify({ excludePathPatterns: [...original.excludePathPatterns, '/FishingLogBook.Web/'] }));
  const excluded = runCli([
    'merge', '--input', join(exclusionDirectory, 'raw'), '--output', join(exclusionDirectory, 'out'), '--sha', 'pr-sha',
    '--config', changedConfig, '--runsettings', join(here, '../../../coverage.runsettings'),
  ]);
  assert.equal(excluded.status, 0, excluded.stderr);
  const exclusion = runCli(['compare', '--baseline', baseline, '--current', join(exclusionDirectory, 'out', 'coverage-summary.json'), '--src-dir', srcDir]);
  assert.equal(exclusion.status, 1);
  assert.match(exclusion.stdout, /New coverage exclusions are not allowed: \/FishingLogBook\.Web\//);
});

test('the CLI gate fails when a production project exists but its report is missing', () => {
  const { root, srcDir } = twoProjectSource();
  const baseline = mergeInto(join(root, 'baseline'), baselineFiles, 'main-sha');
  const missingWeb = mergeInto(join(root, 'missing'), [baselineFiles[0]], 'pr-sha');
  const result = runCli(['compare', '--baseline', baseline, '--current', missingWeb, '--src-dir', srcDir]);
  assert.equal(result.status, 1);
  assert.match(result.stdout, /FishingLogBook\.Web still exists under src\/ but has no coverage data/);
});
