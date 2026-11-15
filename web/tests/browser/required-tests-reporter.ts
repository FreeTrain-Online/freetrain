import type { FullResult, Reporter, TestCase, TestResult } from '@playwright/test/reporter';

/** Required functional selections must not report green with missing coverage. */
export default class RequiredTestsReporter implements Reporter {
  private skipped: string[] = [];
  onTestEnd(test: TestCase, result: TestResult) {
    if (result.status === 'skipped') this.skipped.push(test.titlePath().join(' › '));
  }
  async onEnd(result: FullResult) {
    if (result.status === 'passed' && this.skipped.length) {
      console.error(`Required browser tests were skipped:\n${this.skipped.join('\n')}`);
      return { status: 'failed' as const };
    }
  }
}
