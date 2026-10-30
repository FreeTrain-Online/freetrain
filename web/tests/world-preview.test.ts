import { compassVector, previewCell } from '../src/lib/world-preview.ts';
function equal(a:unknown,b:unknown) {if(JSON.stringify(a)!==JSON.stringify(b))throw new Error(`${JSON.stringify(a)} != ${JSON.stringify(b)}`);}
Deno.test('preview compass retains literal canonical N/E/S/W in north-up and source isometric views',()=>{
  equal([0,2,4,6].map(d=>compassVector('topDown',d as 0|2|4|6)),[{x:0,y:-24},{x:24,y:0},{x:0,y:24},{x:-24,y:0}]);
  equal([0,2,4,6].map(d=>compassVector('isometric',d as 0|2|4|6)),[{x:-24,y:-12},{x:24,y:-12},{x:24,y:12},{x:-24,y:12}]);
});
Deno.test('asymmetric staggered cells retain exact source centers and quarter elevations',()=>{
  equal(previewCell('isometric',2,3,2,[1,2,3,4],1),{
    base:[{x:96,y:24},{x:112,y:32},{x:96,y:40},{x:80,y:32}],
    top:[{x:96,y:-12},{x:112,y:-8},{x:96,y:-4},{x:80,y:-16}]
  });
  equal(previewCell('topDown',2,3,2,[1,2,3,4],1).top,[{x:24,y:56},{x:24,y:72},{x:8,y:72},{x:8,y:56}]);
});
Deno.test('sampled isometric overview scales elevation consistently with sample spacing',()=>{
  equal(previewCell('isometric',0,0,6,[0,0,0,0],12).top,[{x:16,y:-8},{x:32,y:0},{x:16,y:8},{x:0,y:0}]);
  equal(previewCell('topDown',2,3,0,[0,0,0,0],1),previewCell('topDown',2,3,63,[4,4,4,4],12));
});
