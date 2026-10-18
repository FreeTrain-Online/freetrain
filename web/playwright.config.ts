import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { defineConfig } from '@playwright/test';
const saves = mkdtempSync(join(tmpdir(), 'freetrain-browser-saves-'));
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
