import type {
  FullConfig,
  FullResult,
  Reporter,
  TestCase,
  TestError,
  TestResult,
} from '@playwright/test/reporter';
import { existsSync, mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { stripVTControlCharacters } from 'node:util';

export const failureLineLimit = 50;
/** Also bound single-line diffs; the complete original remains in the artifact. */
export function boundedLines(
  value: string,
  limit = failureLineLimit,
): string[] {
  const lines = stripVTControlCharacters(value).split(/\r?\n/);
  const shown = lines.slice(0, limit).map((line) =>
    line.length > 500 ? line.slice(0, 500) + ' … [line truncated]' : line
  );
  if (lines.length > limit) {
    shown[limit - 1] = `… ${
      lines.length - limit + 1
    } more lines in full error artifact`;
  }
  return shown;
}

/** Console-only formatting: never mutate errors consumed by JSON or IDE reporters. */
export default class BoundedReporter implements Reporter {
  private root = '';
  private results: object[] = [];
  private globalErrors: string[] = [];
  private sequence = 0;
  private rejectedCoverage = false;
  onBegin(config: FullConfig) {
    this.root = resolve(
      process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT ??
        join(config.rootDir, 'test-results'),
    );
    mkdirSync(join(this.root, 'errors'), { recursive: true });
    console.log(`Browser artifacts: ${this.root}`);
  }
  private errorArtifact(errors: TestError[], label: string) {
    const path = join(this.root, 'errors', `${++this.sequence}-${label}.json`);
    writeFileSync(path, JSON.stringify(errors, null, 2));
    return path;
  }
  onTestEnd(test: TestCase, result: TestResult) {
    if (result.status === 'skipped' || result.retry > 0) {
      this.rejectedCoverage = true;
    }
    const title = test.titlePath().join(' › ');
    const failed = result.status !== 'passed' && result.status !== 'skipped';
    let errorPath: string | undefined;
    if (failed || result.errors.length) {
      errorPath = this.errorArtifact(
        result.errors,
        `worker-${result.workerIndex}-retry-${result.retry}`,
      );
      result.attachments.push({
        name: 'complete-errors',
        path: errorPath,
        contentType: 'application/json',
      });
      // Worker setup can fail before any test-scoped fixture is entered.
      // Associate its run-owned logs here as a fallback for that case.
      const processes = join(
        this.root,
        'processes',
        `worker-${result.workerIndex}`,
      );
      const directories = [processes, join(this.root, 'processes', 'configuration')].filter(existsSync);
      if (!result.attachments.some((a) => a.name === 'process-association') && directories.length) {
        const files = directories.flatMap(directory => readdirSync(directory).map(name => ({
          name: `process-${name}`, path: join(directory, name), contentType: 'text/plain',
        })));
        result.attachments.push(...files);
        const path = join(
          this.root,
          'errors',
          `${this.sequence}-process-association.json`,
        );
        writeFileSync(
          path,
          JSON.stringify(
            {
              testId: test.id,
              title,
              workerIndex: result.workerIndex,
              directory: processes,
              directories,
              files,
            },
            null,
            2,
          ),
        );
        result.attachments.push({
          name: 'process-association',
          path,
          contentType: 'application/json',
        });
      }
      const manifest = join(
        this.root,
        'errors',
        `${this.sequence}-attachments.json`,
      );
      writeFileSync(manifest, JSON.stringify(result.attachments, null, 2));
      const references = result.attachments.filter((a) => a.path).map((a) =>
        `${a.name}: ${a.path}`
      );
      const header = [
        `${result.status.toUpperCase()}: ${title}`,
        `Source: ${test.location.file}:${test.location.line}:${test.location.column}`,
        `Complete errors: ${errorPath}`,
        `All attachments: ${manifest}`,
        ...references.slice(0, 8),
        ...(references.length > 8
          ? [`${references.length - 8} more attachments in manifest.`]
          : []),
      ];
      const errors = result.errors.map((e) =>
        e.stack ?? e.message ?? e.value ?? String(e)
      ).join('\n');
      console.log(
        [
          ...boundedLines(header.join('\n'), 14),
          ...boundedLines(
            errors,
            failureLineLimit - Math.min(header.length, 14),
          ),
        ].join('\n'),
      );
    } else console.log(`${result.status}: ${boundedLines(title, 1)[0]}`);
    this.results.push({
      title,
      testId: test.id,
      location: test.location,
      status: result.status,
      expectedStatus: test.expectedStatus,
      retry: result.retry,
      workerIndex: result.workerIndex,
      duration: result.duration,
      errorPath,
      attachments: result.attachments,
    });
  }
  onError(error: TestError) {
    const path = this.errorArtifact([error], 'global');
    this.globalErrors.push(path);
    console.error(
      [
        `Browser runner error; complete error: ${path}`,
        `Process logs: ${join(this.root, 'processes')}`,
        ...boundedLines(
          error.stack ?? error.message ?? error.value ?? String(error),
          failureLineLimit - 2,
        ),
      ].join('\n'),
    );
  }
  onEnd(result: FullResult) {
    const status = result.status === 'passed' && this.rejectedCoverage
      ? 'failed'
      : result.status;
    const summary = join(this.root, 'summary.json');
    writeFileSync(
      summary,
      JSON.stringify(
        {
          ...result,
          status,
          results: this.results,
          globalErrors: this.globalErrors,
          report: join(this.root, 'report.json'),
          artifacts: this.root,
          processes: join(this.root, 'processes'),
        },
        null,
        2,
      ),
    );
    console.log(
      `Browser run ${status}: ${this.results.length} results. Summary: ${summary}\nMachine-readable report: ${
        join(this.root, 'report.json')
      }`,
    );
  }
  printsToStdio() {
    return true;
  }
}
