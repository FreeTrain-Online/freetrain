// Shared by the read-only benchmark job and the trusted PR-comment publisher.
// Artifact contents are data: accept only known cases, hashes and finite numbers.
const fs = require('node:fs');

const cases = [
  'minutes-small', 'minutes-large', 'minutes-midnight', 'replace-small', 'replace-large',
  'publish-1-peer', 'publish-4-peers', 'paused-1-peer', 'paused-4-peers',
];
const marker = '<!-- freetrain-backend-benchmarks -->';
const digest = /^[a-f0-9]{64}$/;
const commit = /^[a-f0-9]{40}$/;

function number(value, min = 0, max = Number.MAX_SAFE_INTEGER) {
  if (typeof value !== 'number' || !Number.isFinite(value) || value < min || value > max)
    throw new Error('Invalid benchmark numeric field');
  return value;
}

function validate(report) {
  if (report.schemaVersion !== 1 || typeof report.baseSha !== 'string' || typeof report.headSha !== 'string' ||
      !commit.test(report.baseSha) || !commit.test(report.headSha))
    throw new Error('Invalid benchmark report identity');
  if (!Number.isInteger(report.repetitions)) throw new Error('Invalid repetition count');
  if (report.pullRequest != null && (!Number.isSafeInteger(report.pullRequest) || report.pullRequest < 1))
    throw new Error('Invalid PR number');
  number(report.repetitions, 1, 10);
  if (!Array.isArray(report.runs) || report.runs.length > 20) throw new Error('Invalid benchmark runs');
  const seenRuns = new Set();
  for (const run of report.runs) {
    if (!['base', 'head'].includes(run.side) || !Number.isInteger(run.repetition))
      throw new Error('Invalid benchmark sample');
    number(run.repetition, 1, report.repetitions);
    if (!Number.isInteger(run.exitCode)) throw new Error('Invalid sample exit code');
    number(run.exitCode, -255, 255);
    const key = `${run.side}:${run.repetition}`;
    if (seenRuns.has(key)) throw new Error('Duplicate benchmark sample');
    seenRuns.add(key);
    if (!Array.isArray(run.measurements) || run.measurements.length > cases.length)
      throw new Error('Invalid benchmark measurements');
    const seenCases = new Set();
    for (const row of run.measurements) {
      if (!cases.includes(row.name) || seenCases.has(row.name)) throw new Error('Unknown/duplicate benchmark case');
      seenCases.add(row.name);
      if (!Number.isInteger(row.operations)) throw new Error('Invalid operation count');
      number(row.operations, 1, 10000);
      number(row.allocatedBytes);
      number(row.elapsedMs, 0, 3600000);
      if (typeof row.initialSha256 !== 'string' || typeof row.finalSha256 !== 'string' ||
          !digest.test(row.initialSha256) || !digest.test(row.finalSha256))
        throw new Error('Invalid benchmark state hash');
    }
  }
  return report;
}

function median(values) {
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
}

function samples(report, side, name) {
  return report.runs.filter(run => run.side === side).flatMap(run => run.measurements.filter(row => row.name === name));
}

function timing(rows) {
  if (!rows.length) return 'n/a';
  const ms = rows.map(row => row.elapsedMs / row.operations);
  return `${median(ms).toFixed(3)} [${Math.min(...ms).toFixed(3)}–${Math.max(...ms).toFixed(3)}]`;
}

function bytes(rows) {
  if (!rows.length) return 'n/a';
  return Math.round(median(rows.map(row => row.allocatedBytes / row.operations))).toLocaleString('en-US');
}

function change(before, after, field, comparable) {
  if (!comparable || !before.length || !after.length) return 'n/a';
  const a = median(before.map(row => row[field] / row.operations));
  const b = median(after.map(row => row[field] / row.operations));
  if (!a) return b ? 'n/a' : '0.0%';
  const delta = 100 * (b / a - 1);
  const rounded = Number(delta.toFixed(1));
  return `${rounded > 0 ? '+' : ''}${rounded.toFixed(1)}%`;
}

function render(input, runUrl) {
  const report = validate(input);
  const lines = [marker, '### Backend benchmarks', '',
    `Base \`${report.baseSha.slice(0, 12)}\` → PR \`${report.headSha.slice(0, 12)}\`.`, '',
    `Median of ${report.repetitions} samples per version on the same runner, with alternating run order.`,
    'Time is ms/operation with [min–max]; allocations are bytes/operation. Lower values use fewer resources.',
    '**Informational only: no timing or allocation thresholds.**', '',
    '| Case | Base ms/op [range] | PR ms/op [range] | Time Δ | Base bytes/op | PR bytes/op | Allocation Δ | State |',
    '|---|---:|---:|---:|---:|---:|---:|---|'];
  for (const name of cases) {
    const base = samples(report, 'base', name), head = samples(report, 'head', name);
    const complete = base.length === report.repetitions && head.length === report.repetitions;
    const rows = [...base, ...head];
    const same = rows.length > 0 && ['operations', 'initialSha256', 'finalSha256']
      .every(field => rows.every(row => row[field] === rows[0][field]));
    const state = !complete ? 'incomplete' : same ? 'same' : 'differs';
    const comparable = complete && same;
    lines.push(`| ${name} | ${timing(base)} | ${timing(head)} | ${change(base, head, 'elapsedMs', comparable)} | ${bytes(base)} | ${bytes(head)} | ${change(base, head, 'allocatedBytes', comparable)} | ${state} |`);
  }
  lines.push('', 'Operations: one simulated minute, one unchanged replacement, or one 50 ms network tick (publication/paused cases).',
    'State compares complete initial/final saves and operation counts. Missing or different workloads have no percentage comparison.');
  const environment = report.environment;
  if (environment && /^[\w.-]{1,32}$/.test(environment.os) && /^[\w.-]{1,32}$/.test(environment.architecture) &&
      Number.isInteger(environment.logicalCpus) && environment.logicalCpus > 0 && environment.logicalCpus <= 1024)
    lines.push('', `Runner: ${environment.os} / ${environment.architecture}, ${environment.logicalCpus} logical CPUs. SDK/runtime details are in the raw logs.`);
  const successful = side => report.runs.filter(run => run.side === side && run.exitCode === 0).length;
  lines.push('', `Successful test processes: base ${successful('base')}/${report.repetitions}; PR ${successful('head')}/${report.repetitions}.`);
  if (successful('base') !== report.repetitions || successful('head') !== report.repetitions || report.error)
    lines.push('**Some builds/tests did not complete successfully. Inspect the raw logs and correctness results before interpreting these numbers.**');
  lines.push('Shared-runner timing is noisy; review ranges and raw samples. Native/GC CPU and allocations on other threads are outside the allocation counter.');
  if (runUrl) {
    if (!/^https:\/\/[\w.-]+\/[\w.-]+\/[\w.-]+\/actions\/runs\/\d+$/.test(runUrl))
      throw new Error('Invalid workflow URL');
    lines.push('', `[Workflow run and raw measurements](${runUrl})`);
  }
  return lines.join('\n') + '\n';
}

module.exports = { marker, cases, validate, render };

if (require.main === module) {
  const report = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
  process.stdout.write(render(report, process.env.BENCHMARK_RUN_URL));
}
