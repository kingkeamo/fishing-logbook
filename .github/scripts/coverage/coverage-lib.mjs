import { createHash } from 'node:crypto';

export const SCHEMA_VERSION = 1;
export const DEFAULT_TOLERANCE = 1e-9;

export class CoverageError extends Error {
  constructor(message) {
    super(message);
    this.name = 'CoverageError';
  }
}

const PROJECT_PATH = /(?:^|\/)src\/(FishingLogBook\.[^/]+)\/(.+)$/;

export function sha256(text) {
  return createHash('sha256').update(text).digest('hex');
}

const slashes = (value) => value.replaceAll('\\', '/');
const isAbsolute = (value) => value.startsWith('/') || /^[A-Za-z]:\//.test(value);

export function normaliseSourceFile(filename, excludePathPatterns = [], sources = []) {
  const name = slashes(filename);
  const candidates = isAbsolute(name)
    ? [name]
    : [...sources.map((source) => `${slashes(source).replace(/\/+$/, '')}/${name}`), name];
  for (const path of candidates) {
    if (excludePathPatterns.some((pattern) => path.includes(pattern))) {
      return null;
    }
    const match = PROJECT_PATH.exec(path);
    if (match) {
      return { project: match[1], key: `${match[1]}/${match[2]}` };
    }
  }
  return null;
}

function attribute(tag, name) {
  const match = new RegExp(`\\s${name}="([^"]*)"`).exec(tag);
  return match ? match[1] : null;
}

export function parseCobertura(xml, label, config = {}) {
  const excludePathPatterns = config.excludePathPatterns ?? [];
  const root = /<coverage\b[^>]*>/.exec(xml);
  if (!root || attribute(root[0], 'lines-valid') === null) {
    throw new CoverageError(`${label}: not a Cobertura report (missing <coverage lines-valid>).`);
  }
  const sources = [...xml.matchAll(/<source>([^<]*)<\/source>/g)].map((match) => match[1].trim());
  const classes = [...xml.matchAll(/<class\b([^>]*?)(\/>|>([\s\S]*?)<\/class>)/g)];
  if (classes.length === 0) {
    throw new CoverageError(`${label}: the report contains no classes.`);
  }

  const files = new Map();
  for (const [, openTag, , body] of classes) {
    const filename = attribute(openTag, 'filename');
    if (!filename) {
      throw new CoverageError(`${label}: a class has no filename.`);
    }
    const normalised = normaliseSourceFile(filename, excludePathPatterns, sources);
    if (!normalised || !body) {
      continue;
    }
    const file = files.get(normalised.key) ?? { project: normalised.project, lines: new Map() };
    for (const tag of body.matchAll(/<line\b[^>]*>/g)) {
      const number = Number(attribute(tag[0], 'number'));
      const hits = Number(attribute(tag[0], 'hits'));
      if (!Number.isInteger(number) || !Number.isFinite(hits)) {
        throw new CoverageError(`${label}: ${filename} has a malformed <line> element.`);
      }
      const branch = /\((\d+)\/(\d+)\)/.exec(attribute(tag[0], 'condition-coverage') ?? '');
      const covered = branch ? Number(branch[1]) : 0;
      const total = branch ? Number(branch[2]) : 0;
      mergeLine(file.lines, number, { hits, covered, total });
    }
    files.set(normalised.key, file);
  }
  return files;
}

function mergeLine(lines, number, next) {
  const current = lines.get(number);
  lines.set(
    number,
    current
      ? {
          hits: Math.max(current.hits, next.hits),
          covered: Math.max(current.covered, next.covered),
          total: Math.max(current.total, next.total),
        }
      : next,
  );
}

export function mergeReports(reports) {
  const merged = new Map();
  for (const report of reports) {
    for (const [key, file] of report) {
      const target = merged.get(key) ?? { project: file.project, lines: new Map() };
      for (const [number, line] of file.lines) {
        mergeLine(target.lines, number, line);
      }
      merged.set(key, target);
    }
  }
  return merged;
}

function emptyCounter() {
  return { covered: 0, total: 0 };
}

export function summarise(merged, metadata) {
  const totals = { lines: emptyCounter(), branches: emptyCounter() };
  const projects = {};
  const files = {};
  for (const [key, file] of [...merged].sort(([a], [b]) => a.localeCompare(b))) {
    const entry = { lines: emptyCounter(), branches: emptyCounter() };
    for (const line of file.lines.values()) {
      entry.lines.total += 1;
      entry.lines.covered += line.hits > 0 ? 1 : 0;
      entry.branches.total += line.total;
      entry.branches.covered += line.covered;
    }
    files[key] = entry;
    const project = (projects[file.project] ??= {
      lines: emptyCounter(),
      branches: emptyCounter(),
      files: 0,
    });
    for (const metric of ['lines', 'branches']) {
      project[metric].covered += entry[metric].covered;
      project[metric].total += entry[metric].total;
      totals[metric].covered += entry[metric].covered;
      totals[metric].total += entry[metric].total;
    }
    project.files += 1;
  }
  return { schemaVersion: SCHEMA_VERSION, ...metadata, totals, projects, files };
}

export function ratio(counter) {
  return counter.total === 0 ? Number.NaN : counter.covered / counter.total;
}

export function percent(value) {
  return Number.isNaN(value) ? 'n/a' : `${(value * 100).toFixed(2)}%`;
}

export function signedPoints(delta) {
  const points = delta * 100;
  const text = Math.abs(points) < 0.005 ? '0.00' : points.toFixed(2);
  return `${points > 0.004 ? '+' : ''}${text} pp`;
}

export function validateSummary(summary, label) {
  if (!summary || typeof summary !== 'object' || summary.schemaVersion !== SCHEMA_VERSION) {
    throw new CoverageError(
      `${label}: unsupported coverage summary schema (expected ${SCHEMA_VERSION}, found ${summary?.schemaVersion}).`,
    );
  }
  for (const metric of ['lines', 'branches']) {
    const counter = summary.totals?.[metric];
    if (!counter || !Number.isFinite(counter.covered) || !Number.isFinite(counter.total)
      || counter.total <= 0 || counter.covered < 0 || counter.covered > counter.total) {
      throw new CoverageError(`${label}: ${metric} coverage totals are missing or invalid.`);
    }
  }
  for (const key of ['projects', 'files']) {
    if (!summary[key] || typeof summary[key] !== 'object' || Array.isArray(summary[key])) {
      throw new CoverageError(`${label}: ${key} coverage data is missing or invalid.`);
    }
  }
  for (const [name, project] of Object.entries(summary.projects)) {
    for (const metric of ['lines', 'branches']) {
      const counter = project?.[metric];
      if (!counter || !Number.isFinite(counter.covered) || !Number.isFinite(counter.total)
        || counter.total < (metric === 'lines' ? 1 : 0)
        || counter.covered < 0 || counter.covered > counter.total) {
        throw new CoverageError(`${label}: ${name} ${metric} coverage data is missing or invalid.`);
      }
    }
  }
}

export function compareSummaries(baseline, current, options = {}) {
  const tolerance = options.tolerance ?? DEFAULT_TOLERANCE;
  const existingProjects = new Set(options.existingProjects ?? []);
  validateSummary(baseline, 'baseline');
  validateSummary(current, 'current');

  const failures = [];
  const notes = [];
  const metrics = ['lines', 'branches'].map((metric) => {
    const before = ratio(baseline.totals[metric]);
    const after = ratio(current.totals[metric]);
    const delta = after - before;
    const passed = after >= before - tolerance;
    if (!passed) {
      failures.push(`${metric === 'lines' ? 'Line' : 'Branch'} coverage decreased from ${percent(before)} to ${percent(after)} (${signedPoints(delta)}).`);
    }
    return { metric, before, after, delta, passed, baseline: baseline.totals[metric], current: current.totals[metric] };
  });

  const projectNames = [...new Set([...Object.keys(baseline.projects), ...Object.keys(current.projects)])].sort();
  const projects = projectNames.map((name) => {
    const before = baseline.projects[name];
    const after = current.projects[name];
    if (before && !after) {
      if (existingProjects.has(name)) {
        failures.push(`${name} still exists under src/ but has no coverage data in this run.`);
      } else {
        notes.push(`${name} was removed from the repository.`);
      }
    }
    if (!before && after) {
      notes.push(`${name} is new in this change.`);
    }
    const row = { name, state: before && after ? 'changed' : before ? (existingProjects.has(name) ? 'missing' : 'removed') : 'added' };
    for (const metric of ['lines', 'branches']) {
      row[`${metric}Before`] = before ? ratio(before[metric]) : null;
      row[`${metric}After`] = after ? ratio(after[metric]) : null;
      row[`${metric}Decreased`] = Boolean(before && after
        && ratio(after[metric]) < ratio(before[metric]) - tolerance);
      if (row[`${metric}Decreased`]) {
        const previous = row[`${metric}Before`];
        const next = row[`${metric}After`];
        failures.push(`${name}: ${metric === 'lines' ? 'Line' : 'Branch'} coverage decreased from ${percent(previous)} to ${percent(next)} (${signedPoints(next - previous)}).`);
      }
    }
    return row;
  });

  for (const key of ['configSha', 'runsettingsSha']) {
    if (baseline.config?.[key] !== current.config?.[key]) {
      failures.push('Coverage configuration (coverage.runsettings or .github/coverage/coverage.config.json) differs from the baseline. Configuration changes need a maintainer-approved baseline reset.');
      break;
    }
  }
  const baselineExclusions = new Set(baseline.config?.excludePathPatterns ?? []);
  const addedExclusions = (current.config?.excludePathPatterns ?? []).filter((pattern) => !baselineExclusions.has(pattern));
  if (addedExclusions.length > 0) {
    failures.push(`New coverage exclusions are not allowed: ${addedExclusions.join(', ')}.`);
  }

  const baselineFiles = new Set(Object.keys(baseline.files));
  const currentFiles = new Set(Object.keys(current.files));
  const addedFiles = [...currentFiles].filter((file) => !baselineFiles.has(file)).sort();
  const removedFiles = [...baselineFiles].filter((file) => !currentFiles.has(file)).sort();

  return {
    passed: failures.length === 0,
    failures,
    notes,
    metrics,
    projects,
    addedFiles,
    removedFiles,
    changed: current.changed ?? null,
  };
}

export function parseUnifiedDiff(diff, excludePathPatterns = []) {
  const changed = new Map();
  let key = null;
  for (const line of diff.split('\n')) {
    if (line.startsWith('+++ ')) {
      const path = line.slice(4).trim().replace(/^b\//, '');
      key = path === '/dev/null' ? null : (normaliseSourceFile(path, excludePathPatterns)?.key ?? null);
      continue;
    }
    const hunk = /^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@/.exec(line);
    if (hunk && key) {
      const start = Number(hunk[1]);
      const count = hunk[2] === undefined ? 1 : Number(hunk[2]);
      const set = changed.get(key) ?? new Set();
      for (let offset = 0; offset < count; offset += 1) {
        set.add(start + offset);
      }
      changed.set(key, set);
    }
  }
  return changed;
}

export function changedLineCoverage(merged, changed) {
  const result = { lines: emptyCounter(), branches: emptyCounter(), files: 0 };
  for (const [key, numbers] of changed) {
    const file = merged.get(key);
    if (!file) {
      continue;
    }
    let touched = false;
    for (const number of numbers) {
      const line = file.lines.get(number);
      if (!line) {
        continue;
      }
      touched = true;
      result.lines.total += 1;
      result.lines.covered += line.hits > 0 ? 1 : 0;
      result.branches.total += line.total;
      result.branches.covered += line.covered;
    }
    result.files += touched ? 1 : 0;
  }
  return result;
}

export function renderCobertura(summary, merged) {
  const lines = ['<?xml version="1.0" encoding="utf-8"?>'];
  const rate = (counter) => (counter.total === 0 ? 1 : counter.covered / counter.total);
  lines.push(
    `<coverage line-rate="${rate(summary.totals.lines)}" branch-rate="${rate(summary.totals.branches)}" `
    + `lines-covered="${summary.totals.lines.covered}" lines-valid="${summary.totals.lines.total}" `
    + `branches-covered="${summary.totals.branches.covered}" branches-valid="${summary.totals.branches.total}" `
    + `version="1.9" timestamp="${Math.floor(Date.parse(summary.generatedAt) / 1000)}">`,
    '  <sources><source>.</source></sources>',
    '  <packages>',
  );
  for (const [name, project] of Object.entries(summary.projects)) {
    lines.push(`    <package name="${name}" line-rate="${rate(project.lines)}" branch-rate="${rate(project.branches)}">`, '      <classes>');
    for (const [key, file] of merged) {
      if (file.project !== name) {
        continue;
      }
      const entry = summary.files[key];
      lines.push(
        `        <class name="${key}" filename="src/${key}" line-rate="${rate(entry.lines)}" branch-rate="${rate(entry.branches)}">`,
        '          <lines>',
      );
      for (const [number, line] of [...file.lines].sort(([a], [b]) => a - b)) {
        const branch = line.total > 0
          ? ` branch="True" condition-coverage="${Math.floor((100 * line.covered) / line.total)}% (${line.covered}/${line.total})"`
          : ' branch="False"';
        lines.push(`            <line number="${number}" hits="${line.hits}"${branch} />`);
      }
      lines.push('          </lines>', '        </class>');
    }
    lines.push('      </classes>', '    </package>');
  }
  lines.push('  </packages>', '</coverage>', '');
  return lines.join('\n');
}

function cell(value) {
  return value === null ? '—' : percent(value);
}

export function renderComparisonMarkdown(result, context = {}) {
  const out = ['## Coverage regression gate', ''];
  if (context.baselineSha) {
    out.push(`Baseline: \`${context.baselineSha}\` (target branch) · Current: \`${context.currentSha ?? 'this PR'}\``, '');
  }
  out.push('| Metric | Main | PR | Change | Result |', '|---|---|---|---|---|');
  for (const row of result.metrics) {
    out.push(`| ${row.metric === 'lines' ? 'Line' : 'Branch'} coverage | ${percent(row.before)} (${row.baseline.covered}/${row.baseline.total}) | ${percent(row.after)} (${row.current.covered}/${row.current.total}) | ${signedPoints(row.delta)} | ${row.passed ? '✅ Pass' : '❌ Fail'} |`);
  }
  out.push('', result.passed ? '**Result: PASS**' : '**Result: FAIL**', '');
  for (const failure of result.failures) {
    out.push(`- ❌ ${failure}`);
  }
  for (const note of result.notes) {
    out.push(`- ℹ️ ${note}`);
  }
  out.push('', '### Coverage by production project', '',
    '| Project | Line (main → PR) | Branch (main → PR) | Result |', '|---|---|---|---|');
  for (const project of result.projects) {
    const flag = project.linesDecreased || project.branchesDecreased
      ? `❌ ${[project.linesDecreased ? 'line decreased' : '', project.branchesDecreased ? 'branch decreased' : ''].filter(Boolean).join(', ')}`
      : project.state === 'missing' ? '❌ missing coverage'
        : project.state === 'added' ? 'new' : project.state === 'removed' ? 'removed' : '✅ Pass';
    out.push(`| ${project.name} | ${cell(project.linesBefore)} → ${cell(project.linesAfter)} | ${cell(project.branchesBefore)} → ${cell(project.branchesAfter)} | ${flag} |`);
  }
  if (result.changed) {
    const lines = result.changed.lines;
    const branches = result.changed.branches;
    out.push('', '### Changed-code coverage (informational)', '',
      `Instrumentable changed lines: ${lines.covered}/${lines.total} covered (${percent(ratio(lines))}); `
      + `branches on changed lines: ${branches.covered}/${branches.total} (${percent(ratio(branches))}) across ${result.changed.files} file(s).`);
  }
  if (result.addedFiles.length || result.removedFiles.length) {
    out.push('', `Files with coverage data: ${result.addedFiles.length} added, ${result.removedFiles.length} removed.`);
  }
  out.push('');
  return out.join('\n');
}

export function renderSummaryMarkdown(summary) {
  const out = ['## Coverage (this commit)', '', '| Metric | Coverage | Covered / total |', '|---|---|---|'];
  out.push(`| Line | ${percent(ratio(summary.totals.lines))} | ${summary.totals.lines.covered}/${summary.totals.lines.total} |`);
  out.push(`| Branch | ${percent(ratio(summary.totals.branches))} | ${summary.totals.branches.covered}/${summary.totals.branches.total} |`, '',
    '| Project | Line | Branch |', '|---|---|---|');
  for (const [name, project] of Object.entries(summary.projects)) {
    out.push(`| ${name} | ${percent(ratio(project.lines))} | ${percent(ratio(project.branches))} |`);
  }
  out.push('');
  return out.join('\n');
}

export function selectBaselineRun(runs, baseSha, branch = 'main') {
  const candidates = runs
    .filter((run) => run.head_sha === baseSha && run.head_branch === branch
      && (run.event === 'push' || run.event === 'workflow_dispatch'))
    .sort((a, b) => Date.parse(b.created_at) - Date.parse(a.created_at));
  if (candidates.length === 0) {
    return { status: 'missing' };
  }
  const success = candidates.find((run) => run.status === 'completed' && run.conclusion === 'success');
  if (success) {
    return { status: 'ready', run: success };
  }
  const pending = candidates.find((run) => run.status !== 'completed');
  if (pending) {
    return { status: 'pending', run: pending };
  }
  return { status: 'failed', run: candidates[0] };
}
