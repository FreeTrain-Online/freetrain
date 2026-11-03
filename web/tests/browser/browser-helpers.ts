import { mkdir } from 'node:fs/promises';
import { join } from 'node:path';
import { expect, type Page, type WebSocketRoute } from '@playwright/test';
import { readFileSync } from 'node:fs';
const sprites = JSON.parse(readFileSync('src/generated/sprites.json', 'utf8')).sprites as Record<string, { height: number; offset: number; mask: string[] }>;
export const bootstrap = async (page: Page) => (await (await page.request.get('/api/bootstrap?protocolVersion=6&contentVersion=prototype-content-1')).json()).snapshot;
export async function canonicalState(page:Page) {
  const response=await(await page.request.get('/api/bootstrap?protocolVersion=6&contentVersion=reference-spatial-content-2&contentVersion=terrain-content-1&contentVersion=mixed-railway-content-1')).json();
  if(response.snapshot)return response.snapshot;
  // Large worlds bootstrap with metadata. Creation tests need the current
  // revision for their first replacement; Owners can read it from the save.
  if(response.role==='owner') {
    const saved=await(await page.request.get('/api/saves/download')).json();
    return {...response,revision:saved.revision};
  }
  return response;
}
// Client lifecycle fixture only: no save loading or server world replacement is implied.
export async function worldReplacementFixture(page:Page) {
  let client:WebSocketRoute|undefined,replaced=false;
  await page.routeWebSocket('**/api/session?*',route=>{
    const server=route.connectToServer();client=route;
    server.onMessage(message=>{
      const data=JSON.parse(String(message));
      if(!replaced||!['snapshot','delta','soundEvent'].includes(data.type))route.send(message);
    });
  });
  return async()=>{
    const next=await bootstrap(page);next.worldEpoch=String(BigInt(next.worldEpoch)+1n);
    next.trains.forEach((train:any)=>{train.controlLease=null;});replaced=true;
    if(!client)throw new Error('Fixture connection missing');
    client.send(JSON.stringify(next));return next;
  };
}
export async function ready(page: Page, language = 'en') {
  await expect(page.locator('.command-bar')).toBeVisible();
  await expect(page.getByTestId('connection')).toHaveText(language === 'ja' ? 'ゲームに接続済み' : 'Connected to game');
  await expect(page.locator('canvas.world-canvas')).toHaveAttribute('data-scene', 'live');
}
export async function closePanel(page: Page) {
  const close = page.locator('.primary-panel .panel-title button:last-child');
  if (await close.count()) await close.click();
}
export async function previewRemovalLine(page:Page,from:number[],to:number[],level=1,ja=false){
 const tool=page.getByRole('button',{name:ja?'撤去':'Remove',exact:true});
 if(await tool.getAttribute('aria-pressed')!=='true')await tool.click();
 await page.getByLabel(ja?'撤去範囲':'Removal selection',{exact:true}).selectOption('line');
 for(const [label,value] of [[ja?'始点 X':'Start X',from[0]],[ja?'始点 Y':'Start Y',from[1]],[ja?'終点 X':'End X',to[0]],[ja?'終点 Y':'End Y',to[1]],[ja?'撤去する高さ':'Removal level',level]] as const)
  await page.getByLabel(label,{exact:true}).fill(String(value));
 await page.getByRole('button',{name:ja?'撤去をプレビュー':'Preview removal',exact:true}).click();
 await expect(page.getByRole('button',{name:ja?'撤去を確定':'Confirm removal',exact:true})).toBeEnabled();
}
export async function confirmRemoval(page:Page,ja=false){
 await page.getByRole('button',{name:ja?'撤去を確定':'Confirm removal',exact:true}).click();
 await expect(page.locator('.command-feedback')).toContainText(ja?'サーバーで確定':'Confirmed by server');
 await expect(page.getByRole('button',{name:ja?'撤去を確定':'Confirm removal',exact:true})).toBeDisabled();
}
export async function openPanel(page: Page, id: string, language = 'en') {
  const labels: Record<string, string[]> = { newWorld: ['New world', '新しい世界'], inspector: ['Selected object', '選択した対象'], catalog: ['Building catalog', '建物一覧'], saves: ['Save and restore', '保存と復元'], view: ['View', '表示'], settings: ['Settings', '設定'], session: ['Session', '接続情報'], help: ['Help', 'ヘルプ'] };
  await closePanel(page);
  await page.locator('.command-bar').getByRole('button', { name: language === 'ja' ? 'ゲーム ▾' : 'Game ▾', exact: true }).click();
  await page.locator('#game-menu').getByRole('button', { name: labels[id][language === 'ja' ? 1 : 0], exact: true }).click();
}
export async function at(page: Page, p: { x: number; y: number; z: number }, offset = { x: 0, y: 0 }) {
  return page.locator('canvas').evaluate((el, args) => {
    const canvas = el as HTMLCanvasElement, [cx, cy] = canvas.dataset.camera!.split(',').map(Number), zoom = Number(canvas.dataset.zoom), r = canvas.getBoundingClientRect();
    return { x: r.x + r.width / 2 + (16 * (args.p.x + args.p.y) - cx + args.offset.x) * zoom, y: r.y + r.height / 2 + (8 * (args.p.y - args.p.x) - 16 * args.p.z - cy + args.offset.y) * zoom };
  }, { p, offset });
}
export async function clickTile(page: Page, p: { x: number; y: number; z: number }) { const q = await at(page, p); await page.mouse.click(q.x, q.y); }
export async function clickSprite(page: Page, p: { x: number; y: number; z: number }, name: string) {
  const s = sprites[name]; let pixel = { x: 16, y: Math.floor(s.height / 2) };
  if (s.mask[pixel.y][pixel.x] !== '1') { const y = s.mask.findIndex(row => row.includes('1')); pixel = { x: s.mask[y].indexOf('1'), y }; }
  const q = await at(page, p, { x: pixel.x - 16 + .5, y: pixel.y - s.offset - 8 + .5 }); await page.mouse.click(q.x, q.y);
}
export async function selectTrain(page: Page) {
  await closePanel(page);
  const train = (await bootstrap(page)).trains[0];
  await clickSprite(page, train.position, `train-${({0:'north',2:'east',4:'south',6:'west'} as Record<number,string>)[train.heading]}`);
  await expect(page.getByTestId('train-position')).toBeVisible();
}
export async function selectStation(page: Page) {
  await closePanel(page);
  const station = (await bootstrap(page)).stations[0], q = await at(page, station.position, { x: 0, y: -12 });
  await page.mouse.click(q.x, q.y); await expect(page.getByTestId('confirmed-name')).toBeVisible();
}

/** Store measurements alongside the current run, grouped by behavior. */
export async function evidenceDirectory(category: string) {
  const directory = join(process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!, category);
  await mkdir(directory, { recursive: true });
  return directory;
}
