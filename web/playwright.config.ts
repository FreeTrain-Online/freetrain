import { mkdirSync, mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { defineConfig } from '@playwright/test';
const canonicalSaves = mkdtempSync(join(tmpdir(), 'freetrain-online-canonical-saves-'));
const saves = mkdtempSync(join(tmpdir(), 'freetrain-online-browser-saves-'));
const evidenceParent = resolve(__dirname, '../tmp');
mkdirSync(evidenceParent, { recursive: true });
const evidence = process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE ?? mkdtempSync(join(evidenceParent, 'browser-evidence-'));
mkdirSync(evidence, { recursive: true });
process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE = evidence;
process.env.FREETRAIN_ONLINE_UX1_EVIDENCE ??= join(evidence, 'ux1');
process.env.FREETRAIN_ONLINE_UX2_EVIDENCE ??= join(evidence, 'ux2');
process.env.FREETRAIN_ONLINE_UX3_EVIDENCE ??= join(evidence, 'ux3');
const serverURL = `http://127.0.0.1:${process.env.FREETRAIN_ONLINE_TEST_SERVER_PORT ?? '5080'}`;
const webPort = process.env.FREETRAIN_ONLINE_TEST_WEB_PORT ?? '4173';
const webURL = `http://127.0.0.1:${webPort}`;
const canonicalServerURL = `http://127.0.0.1:${Number(process.env.FREETRAIN_ONLINE_TEST_SERVER_PORT ?? 5080)+1}`;
const canonicalWebPort = Number(webPort)+1;
const canonicalWebURL = `http://127.0.0.1:${canonicalWebPort}`;
export default defineConfig({
  testDir: './tests/browser',
  use: { baseURL: webURL, viewport: { width: 1280, height: 900 } },
  workers: 1,
  projects: [{name:'canonical',testMatch:['railway-construction.spec.ts','railway-rendering.spec.ts','startup-world.spec.ts','reference-world.spec.ts','terrain.spec.ts','corner-terrain.spec.ts','rectangle-terrain.spec.ts','terrace-terrain.spec.ts','authoring-terrain.spec.ts','paint-terrain.spec.ts'],use:{baseURL:canonicalWebURL}},
    {name:'prototype-regression',testIgnore:['railway-construction.spec.ts','railway-rendering.spec.ts','startup-world.spec.ts','reference-world.spec.ts','terrain.spec.ts','corner-terrain.spec.ts','rectangle-terrain.spec.ts','terrace-terrain.spec.ts','authoring-terrain.spec.ts','paint-terrain.spec.ts','optional-content.spec.ts','capacity.spec.ts']}],
  webServer: [{ command: `dotnet run --project ../src/FreeTrain.Online.Server -c Release --no-build --no-restore --no-launch-profile -- --prototype-regression-harness --urls ${serverURL}`, env: { FreeTrainOnline__Saves__Directory: saves,
    FreeTrainOnline__Participants__0__Id: 'builder', FreeTrainOnline__Participants__0__Role: 'operator', FreeTrainOnline__Participants__0__Secret: 'operator-test',
    FreeTrainOnline__Participants__1__Id: 'viewer', FreeTrainOnline__Participants__1__Role: 'observer', FreeTrainOnline__Participants__1__Secret: 'observer-test' }, url: `${serverURL}/health`, reuseExistingServer: false }, { command: `deno task preview --port ${webPort} --strictPort`, env: { FREETRAIN_ONLINE_API_TARGET: serverURL }, url: webURL, reuseExistingServer: false },
    {command:`dotnet run --project ../src/FreeTrain.Online.Server -c Release --no-build --no-restore --no-launch-profile -- --urls ${canonicalServerURL}`,env:{FreeTrainOnline__Saves__Directory:canonicalSaves,FreeTrainOnline__Participants__1__Id:'builder',FreeTrainOnline__Participants__1__Role:'operator',FreeTrainOnline__Participants__1__Secret:'operator-test', FreeTrainOnline__Participants__0__Id:'viewer',FreeTrainOnline__Participants__0__Role:'observer',FreeTrainOnline__Participants__0__Secret:'observer-test'},url:`${canonicalServerURL}/health`,reuseExistingServer:false},
    {command:`deno task preview --port ${canonicalWebPort} --strictPort`,env:{FREETRAIN_ONLINE_API_TARGET:canonicalServerURL},url:canonicalWebURL,reuseExistingServer:false}],
});
