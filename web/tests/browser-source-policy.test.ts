import { browserSourceViolations, validateBrowserSources } from './browser/source-policy.ts';
function assert(value: boolean) { if (!value) throw new Error('Source policy regression'); }
Deno.test('browser source policy rejects raw state helpers and unbounded state/save comparisons', () => {
  for (const source of [
    `const latest = () => messages.filter(m => m.type === 'delta').at(-1);`,
    `expect(latest(0).mixed).toEqual(full.mixed);`,
    `expect(messages.filter(m=>m.type==='delta').at(-1)).toEqual(snapshot);`,
    `await expect.poll(() => save(page)).toEqual(bytes);`,
    `await assertExact(latestCanonicalPublication(history), target, 'phase');`,
    `if(next.cityHeatBaseRevision===previous.revision)next={...next,heat:previous.heat};`,
    `await page.routeWebSocket('**/api/session?*', () => {});`,
  ]) assert(browserSourceViolations('probe.spec.ts', source).length > 0);
});
Deno.test('browser source policy permits reconstructed checkpoints and explicit raw transport probes', () => {
  validateBrowserSources({ 'probe.spec.ts': `
    const latest = () => session.latest();
    await sessions.checkpoint(owner, 'after step');
    await assertExact(latest().mixed, expected.mixed, 'independent oracle');
    // transport-test: Inspect compact heat reference representation.
    await page.routeWebSocket('**/api/session?*', route => {});
    expect(raw.cityHeatBaseRevision).toBe('3');
  ` });
});
Deno.test('all browser specs comply with the state observation policy', async () => {
  const sources: Record<string, string> = {}, directory = new URL('./browser/', import.meta.url);
  for await (const entry of Deno.readDir(directory)) if (entry.name.endsWith('.spec.ts')) sources[entry.name] = await Deno.readTextFile(new URL(entry.name, directory));
  validateBrowserSources(sources);
});
