import { test as base } from '@playwright/test';
import { spawn, type ChildProcess } from 'node:child_process';
import { appendFileSync, existsSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { writeTimings } from './run-timings.ts';
import { BrowserDiagnostics } from './diagnostics.ts';
import { SessionFixture } from './session.ts';
import { attachProcessEvidence, ProcessEvidence, processEvidenceDirectory, processLogOffsets } from './process-evidence.ts';
import { clearCompletedTraceScratch } from './trace-storage.ts';

export * from '@playwright/test';

type WorldServer = { pid?: () => number | undefined; baseURL?: string; restart?: () => Promise<void>; reset?: () => Promise<void> };
const managedProjects = ['canonical', 'prototype-regression', 'startup'];

async function freePort(): Promise<number> {
  const server = createServer();
  await new Promise<void>((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const address = server.address();
  if (!address || typeof address === 'string') throw new Error('No local test port');
  await new Promise<void>((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
  return address.port;
}

async function running(evidence: ProcessEvidence, label: string, command: string, args: string[], env: NodeJS.ProcessEnv, url: string, cwd = process.cwd()) {
  const logs = evidence.start(label, command);
  const child = spawn(command, args, { cwd, env, stdio: ['ignore', 'pipe', 'pipe'] });
  let output = '', error: Error | undefined;
  child.on('error', value => { error = value; });
  const capture = (bytes: Buffer) => { output = (output + bytes.toString()).slice(-8000); };
  child.stdout!.on('data', bytes => { capture(bytes); appendFileSync(logs.stdout, bytes); });
  child.stderr!.on('data', bytes => { capture(bytes); appendFileSync(logs.stderr, bytes); });
  child.on('exit', (code, signal) => logs.event('exit', { code, signal }));
  try {
    const deadline = Date.now() + 60_000;
    while (Date.now() < deadline) {
      if (error) throw error;
      if (child.exitCode !== null || child.signalCode !== null) throw new Error(`Test server exited (${child.signalCode ?? child.exitCode}): ${output}\nLogs: ${logs.stdout}, ${logs.stderr}`);
      try { if ((await fetch(url, { signal: AbortSignal.timeout(1000) })).ok) { logs.event('ready', { pid: child.pid, url }); return child; } } catch { /* Starting. */ }
      await new Promise(resolve => setTimeout(resolve, 50));
    }
    throw new Error(`Test server did not become ready at ${url}: ${output}`);
  } catch (error) {
    logs.event('startup-failed', { error: String(error) });
    await stop(child);
    throw new Error(`${String(error)}\nProcess evidence: ${logs.stdout}, ${logs.stderr}`, { cause: error });
  }
}

async function stop(child: ChildProcess) {
  if (child.exitCode !== null || child.signalCode !== null || !child.pid) return;
  const exited = new Promise<void>(resolve => child.once('exit', () => resolve()));
  child.kill('SIGTERM');
  const force = setTimeout(() => child.kill('SIGKILL'), 5000);
  try { await exited; } finally { clearTimeout(force); }
}

export const test = base.extend<{ resetWorld: void; diagnostics: BrowserDiagnostics | undefined; processEvidence: void; sessions: SessionFixture }, { worldServer: WorldServer }>({
  sessions: async ({}, use, testInfo) => { await use(new SessionFixture(testInfo)); },
  diagnostics: [async ({ browser }, use, testInfo) => {
    clearCompletedTraceScratch(browser, testInfo);
    const diagnostics = new BrowserDiagnostics(testInfo);
    diagnostics.install(browser);
    try { await use(diagnostics); } finally { await diagnostics.finish(); }
  }, { auto: true }],
  processEvidence: [async ({}, use, testInfo) => {
    const directory = processEvidenceDirectory(testInfo.workerIndex);
    const offsets = processLogOffsets(directory);
    const configurationDirectory = resolve(process.env.FREETRAIN_BROWSER_ARTIFACT_ROOT!, 'processes', 'configuration');
    const configurationOffsets = processLogOffsets(configurationDirectory);
    try { await use(); } finally {
      try { await attachProcessEvidence(testInfo, directory, offsets);
        if (existsSync(configurationDirectory))
          await attachProcessEvidence(testInfo, configurationDirectory, configurationOffsets, 'configuration-processes'); }
      catch (error) { testInfo.annotations.push({ type: 'diagnostic-capture-error', description: String(error) }); }
    }
  }, { auto: true }],
  worldServer: [async ({}, use, workerInfo) => {
    const project = workerInfo.project.name;
    // Dedicated optional-content/capacity configurations continue to own servers.
    if (!managedProjects.includes(project)) {
      await use({}); return;
    }
    const configuration = process.env.FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION ?? 'Release';
    if (!['Debug', 'Release'].includes(configuration)) throw new Error('Invalid test server configuration');
    const offset = workerInfo.parallelIndex * 4 + (project === 'prototype-regression' ? 0 : 1);
    const port = process.env.FREETRAIN_ONLINE_TEST_SERVER_PORT
      ? Number(process.env.FREETRAIN_ONLINE_TEST_SERVER_PORT) + offset : await freePort();
    const webPort = process.env.FREETRAIN_ONLINE_TEST_WEB_PORT
      ? Number(process.env.FREETRAIN_ONLINE_TEST_WEB_PORT) + offset : await freePort();
    const apiURL = `http://127.0.0.1:${port}`, baseURL = `http://127.0.0.1:${webPort}`;
    const directory = mkdtempSync(join(tmpdir(), 'freetrain-browser-worker-'));
    const evidence = new ProcessEvidence(processEvidenceDirectory(workerInfo.workerIndex));
    const children: ChildProcess[] = [];
    const started = performance.now();
    let backendMs = 0, previewMs = 0;
    const prototype = project === 'prototype-regression';
    const builder = prototype ? 0 : 1, viewer = prototype ? 1 : 0;
    try {
      // Restore a real, small canonical save before startup. Creation APIs require
      // a connected peer; checkpoint restoration also avoids generating a large
      // world that these tests immediately replace. Startup has no checkpoint.
      const baseline = project === 'canonical'
        ? readFileSync('../prototype/protocol/saves/terrain-v11.json') : undefined;
      if (baseline) writeFileSync(join(directory, 'checkpoint.json'), baseline);
      const backendArgs = [
        resolve(`../src/FreeTrain.Online.Server/bin/${configuration}/net10.0/FreeTrain.Online.Server.dll`),
        ...(prototype ? ['--prototype-regression-harness'] : []), '--urls', apiURL,
      ];
      const backendEnv = { ...process.env, FreeTrainOnline__Saves__Directory: directory,
        [`FreeTrainOnline__Participants__${builder}__Id`]: 'builder',
        [`FreeTrainOnline__Participants__${builder}__Role`]: 'operator',
        [`FreeTrainOnline__Participants__${builder}__Secret`]: 'operator-test',
        [`FreeTrainOnline__Participants__${viewer}__Id`]: 'viewer',
        [`FreeTrainOnline__Participants__${viewer}__Role`]: 'observer',
        [`FreeTrainOnline__Participants__${viewer}__Secret`]: 'observer-test',
      };
      // Reuse the exact web/restart assertions against a portable Linux runtime.
      // Mount current managed output read-only; each worker still owns its saves.
      const containerImage = process.env.FREETRAIN_ONLINE_TEST_CONTAINER_IMAGE;
      const startBackend = () => containerImage ? running(evidence, 'backend', 'docker', [
        'run', '--rm', '--init', '--ulimit', 'core=0', '-p', `127.0.0.1:${port}:8080`,
        '-e', 'FreeTrainOnline__OperatorSecret=fleet-container-test',
        '--mount', `type=bind,source=${resolve('..')},target=${resolve('..')},readonly`,
        '--mount', `type=bind,source=${directory},target=/data/saves`,
        '--workdir', resolve('../src/FreeTrain.Online.Server'),
        ...Object.entries(backendEnv).filter(([key]) => key.startsWith('FreeTrainOnline__'))
          .flatMap(([key,value]) => ['-e', `${key}=${key === 'FreeTrainOnline__Saves__Directory' ? '/data/saves' : value}`]),
        '--entrypoint', 'dotnet', containerImage, backendArgs[0],
        ...(prototype ? ['--prototype-regression-harness'] : []), '--urls', 'http://0.0.0.0:8080',
      ], process.env, `${apiURL}/health`) : running(evidence, 'backend', 'dotnet', backendArgs, backendEnv,
        `${apiURL}/health`, resolve('../src/FreeTrain.Online.Server'));
      const backendStarted = performance.now();
      children.push(await startBackend());
      backendMs = performance.now() - backendStarted;
      const previewStarted = performance.now();
      children.push(await running(evidence, 'preview', 'deno', ['run', '-A', 'npm:vite@8.3.4', ...(process.env.FREETRAIN_ONLINE_TEST_DEV_CLIENT==='1'?[]:['preview']), '--host', '127.0.0.1',
        '--port', String(webPort), '--strictPort'], { ...process.env, FREETRAIN_ONLINE_API_TARGET: apiURL }, baseURL));
      previewMs = performance.now() - previewStarted;
      writeTimings(`worker-${workerInfo.workerIndex}`, { project, backendMs, previewMs,
        startupMs: performance.now() - started });
      let first = true;
      await use({ baseURL, pid: () => children[0].pid, restart: async () => {
        await stop(children[0]);
        children[0] = await startBackend();
      }, reset: async () => {
        if (first) { first = false; return; }
        // Also isolate access throttles, credentials, receipts and save slots.
        // A world upload alone leaves those server-wide states behind.
        await stop(children[0]);
        // Keep the bind-mount root stable across Docker Desktop restarts.
        for (const name of readdirSync(directory)) rmSync(join(directory,name), { recursive: true, force: true });
        if (baseline) writeFileSync(join(directory, 'checkpoint.json'), baseline);
        children[0] = await startBackend();
      } });
    } finally {
      try { for (const child of children.reverse()) await stop(child); }
      finally { rmSync(directory, { recursive: true, force: true }); }
    }
  }, { scope: 'worker', timeout: 120_000 }],
  baseURL: async ({ worldServer }, use, testInfo) => {
    await use(worldServer.baseURL ?? testInfo.project.use.baseURL);
  },
  resetWorld: [async ({ diagnostics, processEvidence, worldServer }, use) => {
    diagnostics?.record('world-reset');
    await worldServer.reset?.();
    await use();
  }, { auto: true }],
});

/** Full language/direction lifecycle matrices remain in comprehensive validation. */
export const extended = () => test.info().config.metadata.extended === true;
