const fs = require('node:fs');
const { marker, render, validate } = require('./backend-benchmark-report.cjs');

module.exports = async function publish({ github, context, core }, reportPath = 'benchmark-artifact/report.json') {
  const run = context.payload.workflow_run;
  if (run.event !== 'pull_request') return;
  const stat = fs.lstatSync(reportPath);
  if (!stat.isFile() || stat.size > 2 * 1024 * 1024) throw new Error('Invalid benchmark artifact file');
  const report = validate(JSON.parse(fs.readFileSync(reportPath, 'utf8')));
  if (report.pullRequest == null) throw new Error('Benchmark report has no triggering PR');
  const linkedPulls = run.pull_requests ?? [];
  if (linkedPulls.length && !linkedPulls.some(pr => pr.number === report.pullRequest))
    throw new Error('Benchmark artifact does not match the triggering PR');
  const heads = new Set([run.head_sha, ...(run.pull_requests ?? []).map(pr => pr.head.sha)]);
  if (!heads.has(report.headSha)) throw new Error('Benchmark artifact does not match the triggering commit');

  const { owner, repo } = context.repo;
  const pulls = await github.paginate(github.rest.repos.listPullRequestsAssociatedWithCommit,
    { owner, repo, commit_sha: report.headSha, per_page: 100 });
  const candidates = pulls.filter(pr => pr.number === report.pullRequest && pr.state === 'open' &&
    pr.base.repo.full_name === `${owner}/${repo}` &&
    pr.head.sha === report.headSha && pr.head.repo?.full_name === run.head_repository.full_name);
  const url = `${context.serverUrl}/${owner}/${repo}/actions/runs/${run.id}`;
  const body = `${render(report, url)}\n<!-- backend-benchmark-run:${run.id}:${run.run_attempt} -->\n`;
  for (const candidate of candidates) {
    // Recheck immediately before posting so an old run cannot label a newer head.
    const { data: pr } = await github.rest.pulls.get({ owner, repo, pull_number: candidate.number });
    if (pr.state !== 'open' || pr.head.sha !== report.headSha) continue;
    const comments = await github.paginate(github.rest.issues.listComments,
      { owner, repo, issue_number: pr.number, per_page: 100 });
    const previous = comments.find(comment => comment.user?.login === 'github-actions[bot]' && comment.body?.includes(marker));
    const previousRun = previous?.body.match(/<!-- backend-benchmark-run:(\d+):(\d+) -->/);
    if (previousRun && (Number(previousRun[1]) > run.id ||
        Number(previousRun[1]) === run.id && Number(previousRun[2]) > run.run_attempt)) continue;
    if (previous)
      await github.rest.issues.updateComment({ owner, repo, comment_id: previous.id, body });
    else
      await github.rest.issues.createComment({ owner, repo, issue_number: pr.number, body });
    core.info(`Published backend benchmark numbers to PR #${pr.number}`);
  }
};
