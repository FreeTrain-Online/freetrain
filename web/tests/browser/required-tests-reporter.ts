import type {
  FullResult,
  Reporter,
  TestCase,
  TestResult,
} from '@playwright/test/reporter';

/** Required functional selections must not report green with missing coverage. */
export default class RequiredTestsReporter implements Reporter {
  private skipped: string[] = [];
  private retried: string[] = [];
  onTestEnd(test: TestCase, result: TestResult) {
    if (result.status === 'skipped') {
      this.skipped.push(test.titlePath().join(' › '));
    }
    if (result.retry > 0) this.retried.push(test.titlePath().join(' › '));
  }
  async onEnd(result: FullResult) {
    if (
      result.status === 'passed' && (this.skipped.length || this.retried.length)
    ) {
      if (this.skipped.length) {
        console.error(
          `Required browser tests were skipped:\n${this.skipped.join('\n')}`,
        );
      }
      if (this.retried.length) {
        console.error(
          `Required browser tests needed retries:\n${this.retried.join('\n')}`,
        );
      }
      return { status: 'failed' as const };
    }
  }
}
