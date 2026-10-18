import { mkdirSync, mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { defineConfig } from '@playwright/test';
const saves = mkdtempSync(join(tmpdir(), 'freetrain-browser-saves-'));
const evidenceParent = resolve(__dirname, '../tmp');
mkdirSync(evidenceParent, { recursive: true });
const evidence = process.env.FREETRAIN_BROWSER_EVIDENCE ?? mkdtempSync(join(evidenceParent, 'browser-evidence-'));
mkdirSync(evidence, { recursive: true });
process.env.FREETRAIN_BROWSER_EVIDENCE = evidence;
process.env.FREETRAIN_UX1_EVIDENCE ??= join(evidence, 'ux1');
process.env.FREETRAIN_UX2_EVIDENCE ??= join(evidence, 'ux2');
process.env.FREETRAIN_UX3_EVIDENCE ??= join(evidence, 'ux3');
const serverURL = `http://127.0.0.1:${process.env.FREETRAIN_TEST_SERVER_PORT ?? '5080'}`;
const webPort = process.env.FREETRAIN_TEST_WEB_PORT ?? '4173';
const webURL = `http://127.0.0.1:${webPort}`;
export default defineConfig({
  testDir: './tests/browser',
  use: { baseURL: webURL, viewport: { width: 1280, height: 900 } },
  workers: 1,
  webServer: [{ command: `dotnet run --project ../src/FreeTrain.Server -c Release --no-build --no-restore --no-launch-profile -- --urls ${serverURL}`, env: { FreeTrain__Saves__Directory: saves,
    FreeTrain__Participants__0__Id: 'builder', FreeTrain__Participants__0__Role: 'operator', FreeTrain__Participants__0__Secret: 'operator-test',
    FreeTrain__Participants__1__Id: 'viewer', FreeTrain__Participants__1__Role: 'observer', FreeTrain__Participants__1__Secret: 'observer-test' }, url: `${serverURL}/health`, reuseExistingServer: false }, { command: `deno task preview --port ${webPort} --strictPort`, env: { FREETRAIN_API_TARGET: serverURL }, url: webURL, reuseExistingServer: false }],
});
