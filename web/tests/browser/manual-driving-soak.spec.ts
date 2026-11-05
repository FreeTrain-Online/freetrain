import {test,expect,type Page} from './fixtures.ts';
import {canonicalState,openPanel,closePanel,ready} from './browser-helpers.ts';
import {settled} from './layout-helpers.ts';
import {readFileSync,writeFileSync} from 'node:fs';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import {validTrainMotion} from '../../src/lib/train-motion-contract.ts';

// Optional owner-save reproduction. The fixture server always receives a copy;
// no game commands or writes are sent to the user's running server/save files.
const fixture=process.env.FREETRAIN_MANUAL_STEERING_SAVE;
test.use({deviceScaleFactor:2});
test('session packet diagnostics identify the rejection and recover without logging its contents',async({page})=>{
 const warnings:any[]=[],errors:string[]=[];let client:import('@playwright/test').WebSocketRoute|undefined,connections=0;
 page.on('pageerror',error=>errors.push(String(error)));
 page.on('console',m=>{if(m.type()==='warning'&&m.text().includes('[FreeTrain] Session packet rejected'))
  void Promise.all(m.args().map(a=>a.jsonValue())).then(values=>warnings.push(values));});
 await page.routeWebSocket('**/api/session?*',route=>{connections++;client=route;route.connectToServer();});
 await page.goto('/');await ready(page);
 client!.send(JSON.stringify({type:'accepted',requestId:'DO_NOT_ECHO_REQUEST_CONTENT',worldEpoch:1,sequence:'1',tick:'0',revision:'0'}));
 await expect.poll(()=>warnings.length).toBe(1);
 expect(warnings[0][1].reason).toContain('(accepted)');expect(warnings[0][1].reason).toContain('/worldEpoch');
 expect(JSON.stringify(warnings)).not.toContain('DO_NOT_ECHO_REQUEST_CONTENT');await expect.poll(()=>connections).toBeGreaterThanOrEqual(2);await ready(page);expect(errors).toEqual([]);
});
test('receive queue overflow closes with a browser-valid code and recovers',async({page})=>{
 const errors:string[]=[],warnings:string[]=[];let client:import('@playwright/test').WebSocketRoute|undefined,connections=0;
 page.on('pageerror',error=>errors.push(String(error)));page.on('console',m=>{if(m.type()==='warning')warnings.push(m.text());});
 await page.routeWebSocket('**/api/session?*',route=>{connections++;client=route;route.connectToServer();});
 await page.goto('/');await ready(page);const snapshot=await canonicalState(page);
 // Hold the first integrity check to simulate an assembly that is slower than
 // incoming delivery. Later connections still use the normal digest function.
 await page.evaluate(()=>{
  const original=crypto.subtle.digest.bind(crypto.subtle);let first=true;
  (window as any).__restorePacketDigest=()=>{crypto.subtle.digest=original;(window as any).__releasePacketDigest?.();};
  crypto.subtle.digest=(...args:Parameters<SubtleCrypto['digest']>)=>{
   if(!first)return original(...args);first=false;(window as any).__packetDigestBlocked=true;
   return new Promise<ArrayBuffer>(resolve=>{void original(...args).then(bytes=>{(window as any).__releasePacketDigest=()=>resolve(bytes);});});
  };
 });
 const bytes=Buffer.from(JSON.stringify({message:snapshot,packedTerrain:null})),size=48*1024,count=Math.ceil(bytes.length/size),sha256=createHash('sha256').update(bytes).digest('hex');
 for(let index=0;index<count;index++)client!.send(JSON.stringify({type:'stateChunk',transferId:'a'.repeat(32),worldEpoch:snapshot.worldEpoch,revision:snapshot.revision,index,count,totalBytes:bytes.length,sha256,data:bytes.subarray(index*size,(index+1)*size).toString('base64')}));
 await expect.poll(()=>page.evaluate(()=>(window as any).__packetDigestBlocked)).toBe(true);
 for(let n=0;n<33;n++)client!.send(JSON.stringify({type:'accepted',requestId:`overflow-${n}`,worldEpoch:snapshot.worldEpoch,sequence:'1',tick:snapshot.tick,revision:snapshot.revision}));
 await expect.poll(()=>warnings.some(w=>w.includes('Session receive queue exceeded'))).toBe(true);
 await expect.poll(()=>connections).toBeGreaterThanOrEqual(2);await ready(page);
 await page.evaluate(()=>(window as any).__restorePacketDigest());expect(errors).toEqual([]);
});
async function state(page:Page){
 const metadata=await canonicalState(page);
 if(metadata.mixed)return metadata;
 const saved=await(await page.request.get('/api/saves/download')).json();
 return {...metadata,map:saved.map,tick:saved.clock.absoluteMinute,paused:saved.paused,mixed:saved.mixed};
}
async function pickTrain(page:Page,id:string){
 await closePanel(page);const s=await state(page),train=s.mixed.fleet.trains.find((t:any)=>t.id===id),car=train.cars[0],p=car.state.location,h=car.state.heading;
 const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));
 const rail=s.mixed.voxels.find((v:any)=>v.position.x===p.x&&v.position.y===p.y&&v.position.z===p.z).traffic.rail;
 const normal=JSON.parse(readFileSync('../src/FreeTrain.Online.Content/reference-spatial.json','utf8')).normalRailPorts;
 const junctions=[[1,5,4],[1,5,6],[2,6,5],[2,6,7],[3,7,6],[3,7,0],[4,0,7],[4,0,1],[5,1,0],[5,1,2],[6,2,1],[6,2,3],[7,3,2],[7,3,4],[0,4,3],[0,4,5]];
 const ports=rail.pattern<12?normal[rail.pattern]:junctions[rail.pattern-12];
 if(!ports)throw new Error('Soak fixture head must be on normal/junction rail.');
 const outgoing=ports.includes(h)?h:ports.includes((h+7)%8)?(h+7)%8:(h+1)%8;
 let turn=(outgoing-h+8)%8;if(turn===7)turn=-1;const dd=(2*outgoing+3*turn+16)%16;
 const dx=turn?(2<dd&&dd<10?3:-3):0,dy=-9+(turn?(6<dd&&dd<=14?2:-2):0);
 const recipe=catalog.renderRecipes.find((r:any)=>r.id===car.definition.recipeId),angle=((h*2+turn+16)%16)^(train.isReversed?8:0);
 const frame=recipe.frames.find((f:any)=>f.selector.angle===(recipe.fields.asymmetric===true?angle:angle&7)),mask=catalog.masks[frame.masks['spring/day']];
 let sample:{a:number;b:number}|undefined;
 for(let b=1;b<25&&!sample;b++)for(let a=3;a<29&&!sample;a++)if(mask[b][a]==='1')sample={a,b};
 expect(sample).toBeDefined();const center=Math.trunc((s.map.height-1)/2);
 const q={x:16*(p.x-center+p.y)+sample!.a+dx+.5,y:8*(p.y-p.x+center)-16*p.z+sample!.b+dy+.5};
 const canvas=page.locator('canvas.world-canvas');
 const pan=await canvas.evaluate((el,q)=>{const [x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return{deltaX:(q.x-x)*z,deltaY:(q.y-y)*z,deltaMode:0};},q);
 await canvas.dispatchEvent('wheel',pan);await expect(canvas).toHaveAttribute('data-presentation','ready');await settled(page);
 const point=await canvas.evaluate((el,q)=>{const r=el.getBoundingClientRect(),[x,y]=el.dataset.camera!.split(',').map(Number),z=Number(el.dataset.zoom);return{x:r.x+r.width/2+(q.x-x)*z,y:r.y+r.height/2+(q.y-y)*z};},q);
 await page.mouse.click(point.x,point.y);await expect(page.getByTestId('selected-object')).toContainText(` · ${id} · `);
 await closePanel(page);await canvas.focus();
}

test('saved fleet sustains map-focused manual steering at Normal speed without reconnect',async({page})=>{
 test.skip(!fixture,'Set FREETRAIN_MANUAL_STEERING_SAVE to a canonical fleet save for isolated reproduction.');
 const seconds=Number(process.env.FREETRAIN_MANUAL_STEERING_SECONDS??90);
 expect(Number.isInteger(seconds)&&seconds>=1&&seconds<=180).toBe(true);test.setTimeout((seconds+90)*1000);
 const source=JSON.parse(readFileSync(fixture!,'utf8'));source.paused=true;
 const train=process.env.FREETRAIN_MANUAL_STEERING_TRAIN
  ?source.mixed.fleet.trains.find((t:any)=>t.id===process.env.FREETRAIN_MANUAL_STEERING_TRAIN)
  :[...source.mixed.fleet.trains].filter((t:any)=>t.cars[0].state.kind==='inside').sort((a:any,b:any)=>b.cars.length-a.cars.length)[0];
 expect(train).toBeDefined();const diagnostics:any[]=[],invalidPackets:any[]=[];let connections=0,ending:any;
 page.on('console',m=>{if(['warning','error'].includes(m.type()))diagnostics.push({kind:m.type(),text:m.text()});});
 page.on('pageerror',e=>diagnostics.push({kind:'pageerror',text:String(e)}));
 const before=await canonicalState(page),upload=await page.request.post('/api/saves/upload?expectedWorldEpoch='+before.worldEpoch,{data:JSON.stringify(source)});
 expect(upload.ok(),await upload.text()).toBe(true);
 await page.routeWebSocket('**/api/session?*',route=>{connections++;const server=route.connectToServer();
  server.onMessage(m=>{const d=JSON.parse(String(m));
   if((d.type==='delta'||d.type==='snapshot')&&!validTrainMotion(d.trainMotion,d.tick)){
    diagnostics.push({kind:'motion',type:d.type});if(invalidPackets.length<2)invalidPackets.push(d);
   }
   route.send(m);
  });
 });
 try{
  await page.goto('/');await ready(page);await expect(page.locator('.game-speed')).toHaveValue('1');
  await openPanel(page,'fleet');await page.getByRole('button',{name:/^Owned stock/}).click();
  await page.locator('.fleet-owned .catalog-item-main').filter({hasText:new RegExp(`#${train.id}\\b`)}).click();
  await page.getByRole('combobox',{name:'Controller',exact:true}).selectOption('manual');
  await expect(page.locator('.command-feedback')).toHaveText('Confirmed by server');
  await pickTrain(page,train.id);await page.keyboard.down('ArrowLeft');await expect(page.getByTestId('manual-input')).toHaveText('ArrowLeft');
  await page.keyboard.press('Space');await expect(page.getByRole('button',{name:'Pause',exact:false})).toBeVisible();
  for(let n=0;n<seconds*2;n++){await page.waitForTimeout(500);if(connections>1||diagnostics.length)break;}
  await page.keyboard.up('ArrowLeft');ending=await state(page);
  expect(diagnostics).toEqual([]);expect(connections).toBe(1);
  expect(BigInt(ending.tick)).toBeGreaterThan(BigInt(source.clock.absoluteMinute));
  expect(ending.mixed.fleet.trains.find((t:any)=>t.id===train.id).moveCount).toBeGreaterThan(train.moveCount);
 }finally{
  const folder=process.env.FREETRAIN_ONLINE_BROWSER_EVIDENCE!;
  writeFileSync(join(folder,'manual-driving-soak-diagnostics.json'),JSON.stringify({connections,diagnostics,ending:ending&&{tick:ending.tick,paused:ending.paused}},null,2));
  if(invalidPackets.length)writeFileSync(join(folder,'manual-driving-soak-invalid-packets.json'),JSON.stringify(invalidPackets));
 }
});
