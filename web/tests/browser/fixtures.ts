import { test as base } from '@playwright/test';
import { spawn, type ChildProcess } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

export * from '@playwright/test';

type WorldServer = { pid?: () => number | undefined; baseURL?: string; restart?: () => Promise<void>; reset?: () => Promise<void> };

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

async function running(command: string, args: string[], env: NodeJS.ProcessEnv, url: string, cwd = process.cwd()) {
  const child = spawn(command, args, { cwd, env, stdio: ['ignore', 'pipe', 'pipe'] });
  let output = '', error: Error | undefined;
  child.on('error', value => { error = value; });
  const capture = (bytes: Buffer) => { output = (output + bytes.toString()).slice(-8000); };
  child.stdout!.on('data', capture); child.stderr!.on('data', capture);
  try {
    const deadline = Date.now() + 60_000;
    while (Date.now() < deadline) {
      if (error) throw error;
      if (child.exitCode !== null) throw new Error(`Test server exited (${child.exitCode}): ${output}`);
      try { if ((await fetch(url, { signal: AbortSignal.timeout(1000) })).ok) return child; } catch { /* Starting. */ }
      await new Promise(resolve => setTimeout(resolve, 50));
    }
    throw new Error(`Test server did not become ready at ${url}: ${output}`);
  } catch (error) { await stop(child); throw error; }
}

async function stop(child: ChildProcess) {
  if (child.exitCode !== null || child.signalCode !== null || !child.pid) return;
  const exited = new Promise<void>(resolve => child.once('exit', () => resolve()));
  child.kill('SIGTERM');
  const force = setTimeout(() => child.kill('SIGKILL'), 5000);
  try { await exited; } finally { clearTimeout(force); }
}

export const test = base.extend<{ resetWorld: void }, { worldServer: WorldServer }>({
  worldServer: [async ({}, use, workerInfo) => {
    const project = workerInfo.project.name;
    // Dedicated optional-content/capacity configurations continue to own servers.
    if (!['canonical', 'prototype-regression', 'startup'].includes(project)) {
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
    const children: ChildProcess[] = [];
    const prototype = project === 'prototype-regression';
    const builder = prototype ? 0 : 1, viewer = prototype ? 1 : 0;
    try {
      // Restore a real, small canonical save before startup. Creation APIs require
      // a connected peer; checkpoint restoration also avoids generating a large
      // world that these tests immediately replace. Startup has no checkpoint.
      const baseline = project === 'canonical'
        ? readFileSync('../prototype/protocol/saves/terrain-v9.json') : undefined;
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
      const startBackend = () => containerImage ? running('docker', [
        'run', '--rm', '--init', '--ulimit', 'core=0', '-p', `127.0.0.1:${port}:8080`,
        '-e', 'FreeTrainOnline__OperatorSecret=fleet-container-test',
        '--mount', `type=bind,source=${resolve('..')},target=${resolve('..')},readonly`,
        '--mount', `type=bind,source=${directory},target=/data/saves`,
        '--workdir', resolve('../src/FreeTrain.Online.Server'),
        ...Object.entries(backendEnv).filter(([key]) => key.startsWith('FreeTrainOnline__'))
          .flatMap(([key,value]) => ['-e', `${key}=${key === 'FreeTrainOnline__Saves__Directory' ? '/data/saves' : value}`]),
        '--entrypoint', 'dotnet', containerImage, backendArgs[0],
        ...(prototype ? ['--prototype-regression-harness'] : []), '--urls', 'http://0.0.0.0:8080',
      ], process.env, `${apiURL}/health`) : running('dotnet', backendArgs, backendEnv,
        `${apiURL}/health`, resolve('../src/FreeTrain.Online.Server'));
      children.push(await startBackend());
      children.push(await running('deno', ['run', '-A', 'npm:vite@8.3.4', ...(process.env.FREETRAIN_ONLINE_TEST_DEV_CLIENT==='1'?[]:['preview']), '--host', '127.0.0.1',
        '--port', String(webPort), '--strictPort'], { ...process.env, FREETRAIN_ONLINE_API_TARGET: apiURL }, baseURL));
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
  resetWorld: [async ({ worldServer }, use) => {
    await worldServer.reset?.();
    await use();
  }, { auto: true }],
});

/** Full language/direction lifecycle matrices remain in comprehensive validation. */
export const extended = () => test.info().config.metadata.extended === true;
