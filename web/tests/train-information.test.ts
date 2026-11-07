import assert from 'node:assert/strict';
import {trainInformation,trainSelectionOptions,trainLocation,resolveTrainLabels} from '../src/lib/train-information.ts';
import {colorMapXML,palette,recolorPixels,defaultTrainColors} from '../src/lib/train-colors.ts';
import profile from '../src/generated/fleet-profile.json' with {type:'json'};
import type {FleetTrain,Fleet,Snapshot} from '../src/generated/contracts.ts';
const stock=profile.cars.find(c=>c.normal>0)!;
const train={id:'10',name:'Name',passengers:stock.normal*2,cars:[{id:'11',definition:stock.reference,state:{kind:'inside',location:{x:3,y:5,z:1}}}]} as FleetTrain;
Deno.test('information uses current consist normal/seated/packed capacities, truncates percent and permits over 100%',()=>{
 const info=trainInformation(train)!;assert.equal(info.normal,stock.normal);assert.equal(info.percent,200);
 assert.equal(trainInformation({...train,passengers:1})!.percent,Math.trunc(100/stock.normal));
 const empty=profile.cars.find(c=>c.normal===0)!;
 assert.deepEqual(trainInformation({...train,passengers:0,cars:[{...train.cars[0],definition:empty.reference}]}),{normal:0,seated:0,packed:0,passengers:0,percent:undefined});
});
Deno.test('nested train selector preserves authoritative mixed member order, names and stable IDs',()=>{
 const f={rootGroupId:'1',groups:[{id:'1',name:'Root',members:['10','2']},{id:'2',name:'Branch',members:['20']}],trains:[train,{...train,id:'20',name:'Night'}]} as Fleet;
 assert.deepEqual(trainSelectionOptions(f,'All trains'),[{id:'1',name:'All trains',depth:0,group:true},{id:'10',name:'Name',depth:1,group:false},{id:'2',name:'Branch',depth:1,group:true},{id:'20',name:'Night',depth:2,group:false}]);
});
Deno.test('framing follows current head identity through reversal, Outside and return, with no Unplaced/sold fallback',()=>{
 const read={mixed:{fleet:{trains:[train]}}} as Snapshot;
 assert.deepEqual(trainLocation(read,'10'),{x:3,y:5,z:1});
 const outside={...train,cars:[{...train.cars[0],state:{...train.cars[0].state,kind:'outside' as const,location:{x:-2,y:12,z:1},countdown:50}}]};
 assert.deepEqual(trainLocation({...read,mixed:{...read.mixed!,fleet:{...read.mixed!.fleet!,trains:[outside]}}},'10'),{x:-2,y:12,z:1});
 assert.equal(trainLocation({...read,mixed:{...read.mixed!,fleet:{...read.mixed!.fleet!,trains:[]}}},'10'),undefined);
 assert.equal(trainLocation({...read,mixed:{...read.mixed!,fleet:{...read.mixed!.fleet!,trains:[{...train,cars:[{...train.cars[0],state:{kind:'unplaced',location:null,heading:null,countdown:null}}]}]}}},'10'),undefined);
});
Deno.test('local label settings reject arbitrary fonts, sizes and canvas styles',()=>{
 assert.deepEqual(resolveTrainLabels({visible:true,font:'url(x)',size:100,color:'red'}),{visible:true,font:'sans-serif',size:12,color:'#ffff00'});
 assert.equal(resolveTrainLabels({color:'#123456\n'}).color,'#ffff00');
 assert.deepEqual(resolveTrainLabels({visible:true,font:'serif',size:24,color:'#01abcd'}),{visible:true,font:'serif',size:24,color:'#01abcd'});
});
Deno.test('color mapping preserves source palette, 180/255 shades, backward night precedence and transparency',()=>{
 const colors=['#123456','#fedcba','#0080ff','#808080'];
 const bytes=new Uint8ClampedArray([0,0,255,255,0,0,180,255,0,255,0,255,255,0,0,255,255,255,0,255,0,0,255,0,12,23,34,255]);
 recolorPixels(bytes,colors,false);
 assert.deepEqual([...bytes],[18,52,86,255,12,36,60,255,254,220,186,255,0,128,255,255,128,128,128,255,0,0,255,0,12,23,34,255]);
 const night=new Uint8ClampedArray([0,0,63,255,0,0,45,255,0,0,255,255]);recolorPixels(night,colors,true);
 assert.deepEqual([...night],[4,13,21,255,3,9,15,255,18,52,86,255]);assert.equal(palette(colors,true).length,16);
});
Deno.test('colorMap export keeps source RGB triplets and escapes XML attributes',()=>{
 assert.equal(colorMapXML(defaultTrainColors),'<colorMap picture="{243D9F53-67FB-432E-B985-3B9B696D8CE0}-1" base="255,0,0" line1="0,0,255" line2="0,255,0" line3="255,255,0" />');
 assert(colorMapXML({...defaultTrainColors,template:{...defaultTrainColors.template,contributionId:'a"&<>'}}).includes('a&quot;&amp;&lt;&gt;'));
});
