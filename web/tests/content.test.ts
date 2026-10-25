const root = new URL('../../', import.meta.url);
const file = (path: string) => Deno.readFile(new URL(path, root));
const sha = async (bytes: Uint8Array) => [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))].map(b => b.toString(16).padStart(2, '0')).join('');
function assert(value: unknown, message = 'Assertion failed'): asserts value { if (!value) throw new Error(message); }

Deno.test('server and browser prepared catalogs have the same exact bytes and digest', async () => {
  const server = await file('src/FreeTrain.Online.Content/bundled-v2.json');
  const browser = await file('web/public/content/bundled-v2/catalog.json');
  const hash = new TextDecoder().decode(await file('web/public/content/bundled-v2/catalog.sha256')).trim();
  assert(await sha(server) === hash && await sha(browser) === hash);
  const catalog = JSON.parse(new TextDecoder().decode(browser));
  assert(catalog.source.revision === '883cf7d81cf49db25ff38cd3a25d0f140afe31a0');
  assert(catalog.packId === 'exav-stable-bundled' && catalog.contributions.length === 448);
  assert(catalog.contributions.every((d: {status: string}) => d.status === 'prepared; gameplay-unavailable'));
});

Deno.test('prepared resources keep their hashes, native dimensions and explicit playback limits', async () => {
  const catalog = JSON.parse(new TextDecoder().decode(await file('web/public/content/bundled-v2/catalog.json')));
  for (const asset of catalog.assets) {
    for (const mode of ['day', 'night', 'native']) {
      if (!asset[mode]) continue;
      const descriptor = asset[mode];
      assert(descriptor.url.startsWith('/content/bundled-v2/media/') && !descriptor.url.includes('..'));
      const bytes = await file(`web/public${descriptor.url}`);
      assert(await sha(bytes) === descriptor.sha256 && bytes.length === descriptor.bytes, asset.path);
      if (asset.mediaType === 'bmp') {
        const header = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
        assert(header.getUint32(16) === asset.width && header.getUint32(20) === asset.height, asset.path);
      }
    }
  }
  assert(catalog.assets.filter((a: {status: string}) => a.status === 'retained-midi; playback-adapter-pending').length === 3);
  assert(catalog.pictures.filter((p: {status: string}) => p.status === 'prepared-picture').length === 141);
});
