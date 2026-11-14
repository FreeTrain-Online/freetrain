const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { cases, marker, render, validate } = require('./backend-benchmark-report.cjs');
const publish = require('./publish-backend-benchmark.cjs');

function report(repetitions = 3) {
  const result = { schemaVersion: 1, pullRequest: 7, baseSha: 'a'.repeat(40), headSha: 'b'.repeat(40), repetitions, runs: [] };
  for (let i = 1; i <= repetitions; i++) for (const side of ['base', 'head'])
    result.runs.push({ side, repetition: i, exitCode: 0, measurements: cases.map(name => ({
      name, operations: 10, allocatedBytes: side === 'base' ? 1000 : 2000,
      elapsedMs: side === 'base' ? 100 : 120, initialSha256: 'c'.repeat(64), finalSha256: 'd'.repeat(64),
    })) });
  return result;
}

test('reports raw medians, ranges and percentage changes without a pass/fail threshold', () => {
  const body = render(report());
  assert.match(body, /10\.000 \[10\.000–10\.000\]/);
  assert.match(body, /\+20\.0%/);
  assert.match(body, /\+100\.0%/);
  assert.match(body, /Informational only/);
});

test('uses the median and exposes outliers rather than averaging them away', () => {
  const r = report();
  r.runs.filter(run => run.side === 'base').forEach((run, i) => { run.measurements[0].elapsedMs = [100, 200, 1000][i]; });
  assert.match(render(r), /20\.000 \[10\.000–100\.000\]/);
});

test('arbitrarily slower or larger measurements remain reportable', () => {
  const r = report();
  for (const run of r.runs.filter(run => run.side === 'head'))
    for (const row of run.measurements) { row.allocatedBytes = 10 ** 12; row.elapsedMs = 3000000; }
  assert.doesNotThrow(() => render(r));
});

test('different final states suppress percentage comparisons', () => {
  const r = report();
  for (const run of r.runs.filter(run => run.side === 'head')) run.measurements[0].finalSha256 = 'f'.repeat(64);
  const row = render(r).split('\n').find(line => line.startsWith('| minutes-small |'));
  assert.match(row, /differs/);
  assert.doesNotMatch(row, /%/);
});

test('different operation counts suppress percentage comparisons', () => {
  const r = report(); r.runs[0].measurements[0].operations++;
  assert.match(render(r).split('\n').find(line => line.startsWith('| minutes-small |')), /differs/);
});

test('incomplete samples and failing test processes stay visible', () => {
  const r = report(); r.runs.pop(); r.runs[0].exitCode = 1;
  const body = render(r);
  assert.match(body, /incomplete/);
  assert.match(body, /Some builds\/tests did not complete successfully/);
});

for (const [name, mutate] of [
  ['unknown schema', r => { r.schemaVersion = 2; }],
  ['untrusted case label', r => { r.runs[0].measurements[0].name = '<script>bad</script>'; }],
  ['NaN', r => { r.runs[0].measurements[0].elapsedMs = NaN; }],
  ['numeric strings', r => { r.runs[0].measurements[0].allocatedBytes = '123'; }],
  ['duplicate sample', r => { r.runs.push(r.runs[0]); }],
  ['duplicate case', r => { r.runs[0].measurements.push(r.runs[0].measurements[0]); }],
]) test(`rejects ${name}`, () => { const r = report(); mutate(r); assert.throws(() => validate(r)); });

function api(t, options = {}) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'backend-comment-test-'));
  t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  const file = path.join(directory, 'report.json'); fs.writeFileSync(file, JSON.stringify(options.report ?? report()));
  const pr = { number: 7, state: 'open', head: { sha: 'b'.repeat(40), repo: { full_name: 'fork/repo' } },
    base: { repo: { full_name: 'owner/repo' } } };
  const posted = [];
  const github = { rest: {
    repos: { listPullRequestsAssociatedWithCommit() {} },
    pulls: { get: async () => ({ data: options.currentPr ?? pr }) },
    issues: { listComments() {}, createComment: async value => posted.push({ action: 'create', ...value }),
      updateComment: async value => posted.push({ action: 'update', ...value }) },
  } };
  github.paginate = async route => route === github.rest.repos.listPullRequestsAssociatedWithCommit ? [pr] : options.comments ?? [];
  const context = { repo: { owner: 'owner', repo: 'repo' }, serverUrl: 'https://github.com', payload: { workflow_run: {
    id: 42, run_attempt: 1, event: 'pull_request', head_sha: options.runSha ?? 'b'.repeat(40),
    head_repository: { full_name: 'fork/repo' }, pull_requests: [],
  } } };
  return { args: { github, context, core: { info() {} } }, file, posted, pr };
}

test('publishes to a fork PR even when workflow_run.pull_requests is empty', async t => {
  const a = api(t); await publish(a.args, a.file);
  assert.equal(a.posted.length, 1); assert.equal(a.posted[0].issue_number, 7);
  assert.match(a.posted[0].body, /actions\/runs\/42/);
});

test('updates its previous bot comment', async t => {
  const a = api(t, { comments: [{ id: 9, user: { login: 'github-actions[bot]' }, body: marker }] });
  await publish(a.args, a.file);
  assert.equal(a.posted[0].action, 'update'); assert.equal(a.posted[0].comment_id, 9);
});

test('publishes only to the triggering PR when a commit has multiple open PRs', async t => {
  const a = api(t);
  a.args.github.paginate = async route => route === a.args.github.rest.repos.listPullRequestsAssociatedWithCommit
    ? [a.pr, { ...a.pr, number: 8 }] : [];
  await publish(a.args, a.file);
  assert.equal(a.posted.length, 1); assert.equal(a.posted[0].issue_number, 7);
});

test('rejects a PR identity inconsistent with workflow metadata', async t => {
  const a = api(t);
  a.args.context.payload.workflow_run.pull_requests = [{ number: 8, head: { sha: 'b'.repeat(40) } }];
  await assert.rejects(() => publish(a.args, a.file), /triggering PR/);
  assert.equal(a.posted.length, 0);
});

test('does not attach an old result to a newer PR head', async t => {
  const a = api(t); a.args.github.rest.pulls.get = async () => ({ data: { ...a.pr, head: { sha: 'e'.repeat(40) } } });
  await publish(a.args, a.file); assert.equal(a.posted.length, 0);
});

test('does not replace a newer benchmark run', async t => {
  const a = api(t, { comments: [{ id: 9, user: { login: 'github-actions[bot]' },
    body: marker + '\n<!-- backend-benchmark-run:43:1 -->' }] });
  await publish(a.args, a.file); assert.equal(a.posted.length, 0);
});

test('rejects an artifact bound to a different triggering commit', async t => {
  const a = api(t, { runSha: 'e'.repeat(40) });
  await assert.rejects(() => publish(a.args, a.file), /triggering commit/);
  assert.equal(a.posted.length, 0);
});
