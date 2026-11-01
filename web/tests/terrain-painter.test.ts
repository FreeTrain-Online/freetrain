import {createTerrainPainter,type PaintState,type PaintTarget} from '../src/lib/terrain-painter.ts';
import type {ShellModel,ShellState} from '../src/lib/shell-model.ts';
import type {CornerTerrainQuery,CornerTerrainPreview,CommandPayload} from '../src/generated/contracts.ts';
const assert=(x:unknown)=>{if(!x)throw new Error('Assertion failed');};
const deferred=<T>()=>{let resolve!:(value:T)=>void;const promise=new Promise<T>(r=>resolve=r);return{promise,resolve};};
async function until(test:()=>boolean){const limit=performance.now()+2000;while(!test()){if(performance.now()>limit)throw new Error('Deadline');await new Promise(r=>setTimeout(r,1));}}
const target=(x=20):PaintTarget=>({position:{x,y:20,z:1},offsetX:16,offsetY:2,reverse:false});
async function harness(){
 const read=JSON.parse(await Deno.readTextFile(new URL('../../prototype/protocol/fixtures/snapshot-terrain.json',import.meta.url)));
 read.map.width=30;read.map.height=42;read.terrain.cells=Array.from({length:1260},()=>({ground:1,surface:'natural'}));
 const state={read,connection:'ready',role:'owner',pending:false,feedback:'idle',leaving:false,signedOut:false} as ShellState;
 const queries:CornerTerrainQuery[]=[],commands:CommandPayload[]=[],events:PaintState[]=[];
 let previewGate:ReturnType<typeof deferred<void>>|undefined,sendGate:ReturnType<typeof deferred<boolean>>|undefined;
 const model={get state(){return state;},async previewCorner(q:CornerTerrainQuery){queries.push(q);await previewGate?.promise;return {worldEpoch:q.expectedWorldEpoch,geometryRevision:q.expectedGeometryRevision,position:q.position,action:q.action,legal:true,reason:null,changes:[],skipped:[],cost:'0'} as CornerTerrainPreview;},async send(c:CommandPayload){commands.push(c);state.pending=true;const accepted=sendGate?await sendGate.promise:true;state.pending=false;if(accepted){if(state.read!.mixed)state.read!.mixed.geometryRevision=String(BigInt(state.read!.mixed.geometryRevision)+1n);else state.read!.terrain!.geometryRevision=String(BigInt(state.read!.terrain!.geometryRevision)+1n);state.feedback='accepted';}return accepted;}} as Pick<ShellModel,'state'|'previewCorner'|'send'>;
 const painter=createTerrainPainter(model,e=>events.push(e),1);
 return{state,queries,commands,events,painter,previewGate:(g:ReturnType<typeof deferred<void>>)=>previewGate=g,sendGate:(g:ReturnType<typeof deferred<boolean>>)=>sendGate=g};
}
Deno.test('held painting repeats with fresh revisions, serializes commits and coalesces movement into the latest target',async()=>{
 const h=await harness(),gate=deferred<boolean>();h.sendGate(gate);
 try{
  assert(h.painter.start(target(),'raise'));await until(()=>h.commands.length===1);
  for(let x=21;x<30;x++)h.painter.move(target(x));
  assert(h.queries.length===1&&h.commands.length===1);
  gate.resolve(true);await until(()=>h.commands.length>=2);h.painter.stop();
  const a=h.commands[0],b=h.commands[1];assert(a.kind==='editCorner'&&b.kind==='editCorner');
  if(a.kind==='editCorner'&&b.kind==='editCorner'){assert(BigInt(b.query.expectedGeometryRevision)>BigInt(a.query.expectedGeometryRevision));assert(b.query.position.x===29);}
 }finally{h.painter.stop();gate.resolve(true);}
});
Deno.test('release during an outstanding preview prevents every later commit; no stroke is replayed',async()=>{
 const h=await harness(),gate=deferred<void>();h.previewGate(gate);
 assert(h.painter.start(target(),'lower'));await until(()=>h.queries.length===1);h.painter.stop();gate.resolve();
 await new Promise(r=>setTimeout(r,10));assert(h.commands.length===0&&!h.painter.state.active);
});
Deno.test('role, epoch and geometry changes fence a captured paint preview before commit',async()=>{
 for(const change of ['role','epoch','geometry'] as const){const h=await harness(),gate=deferred<void>();h.previewGate(gate);
  assert(h.painter.start(target(),'raise'));await until(()=>h.queries.length===1);
  if(change==='role')h.state.role='observer';else if(change==='epoch')h.state.read!.worldEpoch='99';else h.state.read!.terrain!.geometryRevision='99';
  gate.resolve();await until(()=>!h.painter.state.active);assert(h.commands.length===0);h.painter.stop();
 }
});
Deno.test('reverse target reverses paint and an ambiguous outcome stops without resubmission',async()=>{
 const h=await harness(),gate=deferred<boolean>();h.sendGate(gate);
 assert(h.painter.start({...target(),reverse:true},'raise'));await until(()=>h.commands.length===1);
 assert(h.commands[0].kind==='editCorner'&&h.commands[0].query.action==='lower');h.state.feedback='unresolved';gate.resolve(false);
 await until(()=>!h.painter.state.active);await new Promise(r=>setTimeout(r,10));assert(h.commands.length===1&&!h.painter.start(target(),'raise'));h.painter.stop();
});

Deno.test('mixed-world painting uses infrastructure revision and fences accessory-only edits',async()=>{
 const h=await harness();
 h.state.read!.mixed={geometryRevision:'18'} as NonNullable<typeof h.state.read>['mixed'];
 const terrainRevision=h.state.read!.terrain!.geometryRevision;
 try{
  assert(h.painter.start(target(),'raise'));await until(()=>h.commands.length>=2);h.painter.stop();
  assert(h.queries[0].expectedGeometryRevision==='18'&&h.queries[1].expectedGeometryRevision==='19');
  assert(h.state.read!.terrain!.geometryRevision===terrainRevision);
  const gate=deferred<void>();h.previewGate(gate);const count=h.commands.length;
  assert(h.painter.start(target(),'raise'));await until(()=>h.queries.length>count);
  h.state.read!.mixed!.geometryRevision='99';gate.resolve();await until(()=>!h.painter.state.active);
  assert(h.commands.length===count);
 }finally{h.painter.stop();}
});
