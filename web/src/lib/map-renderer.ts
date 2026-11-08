import {TrainColorCatalog} from './train-colors.ts';
import { TrainMotionPlayer } from './train-motion.ts';
import { TrainCompositor } from './train-compositor.ts';
import { terrainCornerPoint } from './terrain-edit.ts';
import content from '../generated/sprites.json' with { type: 'json' };
import type { Position, Track } from '../generated/contracts.ts';
import type { ReadState } from './session-client.ts';
import { opaqueAt, toScreen, visible, type Camera } from './map-display.ts';
import { offset, type SpatialMap, type Point } from './map-geometry.ts';
import { normalPorts } from './world-identity.ts';
import { ports } from './prototype/rail-geometry.ts';
import { mapSpace } from './map-space.ts';
import { TerrainScene, type SceneTarget } from './terrain-renderer.ts';
import { TerrainViewport } from './terrain-viewport.ts';
import { loadRenderCatalog, RecipeImageCache, type RenderCatalog, type Variant } from './content-recipes.ts';
import { isTerrainWorld, isMixedWorld } from './world-identity.ts';
import { DemoRailway } from './demo-railway.ts';
import { demoCar, demoLoop } from './demo-railway-path.ts';
import type { RailwayPickPolicy } from './railway-renderer.ts';
import {stationNamePositions,type StationLabels} from './station-labels.ts';
export type SpriteKey = keyof typeof content.sprites;
export type Selection = SceneTarget | { kind: 'train' | 'station' | 'building' | 'track'; id: string; position: Position };
export interface MapOptions { stationLabels?:StationLabels; trainLabels?:import('./train-information.ts').TrainLabels; smoothTrains?:boolean; motionTime?:number; fleetPreview?:import('../generated/contracts.ts').FleetDeploymentCar[]; terrainCorner?:Position; slopePreview?:import('../generated/contracts.ts').SlopePreview; railPreview?:import('../generated/contracts.ts').RailwayPreview; railPoints?:{position:Position;label:string}[]; drawPoles?:boolean; drawSpeedLimits?:boolean; heightCut: number; level: number; night: boolean; winter: boolean; season?:Variant['season']; demoTime?:number; recolor: boolean; selection?: Selection; preview?: Position; rectangleEnd?: Position; terrainSites?: {position:Position;skipped:boolean}[]; removalPath?:Position[]; anchor?: Position; shape: Track['shape']; remove: boolean; }
interface Item { position: Position; kind: 'train'|'station'|'building'|'track'|'sample'; id: string; sprite?: SpriteKey; heading?: number; shape?: Track['shape']; cut?: boolean; }
export async function loadImages() {
  const images = new Map<string, HTMLImageElement>();
  await Promise.all(Object.values(content.sprites).flatMap(sprite => [sprite.image,sprite.nightImage,sprite.tintImage]).map(async path => {
    const image = new Image(); image.src=path; await image.decode(); images.set(path,image);
  }));
  return images;
}
export class MapRenderer {
  private motion=new TrainMotionPlayer();
  private compositor=new TrainCompositor();
  private renderCatalog?:RenderCatalog;
  private motionPainted=false;
  resetTrainMotion() {this.motion.reset();this.motionPainted=false;}
  resumeTrainMotion(now:number) {this.motion.resume(now);this.motionPainted=false;}
  private items: Item[]=[];
  private hits: { item: Item; x: number; y: number }[]=[];
  private read?: ReadState;
  private space?: SpatialMap;
  private buildings = new Map<string,ReadState['buildings'][number]>();
  private terrainCache=new RecipeImageCache();
  private colorCatalog=new TrainColorCatalog(this.terrainCache);
  private catalog?:Promise<RenderCatalog>;
  private catalogHash='';
  private terrainKey='';
  private terrainPreparedKey='';
  private terrainWorldKey='';
  private terrainScene?:TerrainScene;
  private terrainViewport?:TerrainViewport;
  needsFrame=false;
  private demoRailway?:DemoRailway;
  private terrainError?:unknown;
  private terrainView?:{x:number;y:number;zoom:number};
  constructor(private canvas: HTMLCanvasElement, private images: Map<string, HTMLImageElement>,private invalidate=()=>{}) {}
  setRead(read: ReadState) {
    this.motion.update(read,performance.now());
    this.read=read;
    delete this.canvas.dataset.demoTrain;delete this.canvas.dataset.demoLoop;
    this.space=mapSpace(read.map);
    this.buildings=new Map(read.buildings.map(building=>[building.id,building]));
    this.items = [
      ...read.tracks.map(track => ({ position: track.position, kind:'track' as const,id:track.entityId??`${track.position.x},${track.position.y},${track.position.z}`,shape:track.shape })),
      ...read.stations.map(station => ({position:station.position,kind:'station' as const,id:station.id})),
      ...read.trains.flatMap(train => train.carPositions.map((position,index) => {
        // Server supplies each car's heading, including followers at corners.
        const heading=train.carHeadings[index];
        const sprite=read.map.coordinateSystem==='prototypeXY'?({0:'train-north',2:'train-east',4:'train-south',6:'train-west'} as Record<number,SpriteKey>)[heading]:undefined;
        return {position,kind:'train' as const,id:train.id,heading,sprite};
      })),
      ...(read.map.coordinateSystem==='prototypeXY'?[{sprite:'fence' as const,position:{x:18,y:18,z:0}},{sprite:'slope' as const,position:{x:19,y:18,z:0}}].map(item=>({...item,kind:'sample' as const,id:item.sprite})):[]),
      ...read.buildings.flatMap(building => Array.from({length:building.height},(_,z) => ({
        position:{...building.position,z:building.position.z+z}, kind:'building' as const,id:building.id,
        sprite:(building.height===1?'pier':z===0?'condo-bottom':z===building.height-1?'condo-top':'condo-middle') as SpriteKey,
      }))),
    ].sort((a,b) => (a.position.y-a.position.x)-(b.position.y-b.position.x) || a.position.z-b.position.z || this.layer(a)-this.layer(b));
  }
  trainCenter(id:string):Point|undefined {
    const train=this.read?.trains.find(t=>t.id===id);if(!this.space)return;
    const head=this.read?.mixed?.fleet?.trains.find(t=>t.id===id)?.cars[0];
    const location=head?.state.location??train?.position;if(!location)return;
    const p=head&&this.motionPainted?this.compositor.position(head.id)??location:location;
    if(this.read!.map.coordinateSystem!=='hv')return this.space.center(p);
    const centerX=Math.trunc((this.read!.map.height-1)/2);
    return {x:16*(p.x-centerX+p.y)+16,y:8*(p.y-p.x+centerX)-16*p.z+8};
  }
  private layer(item: Item) { return {track:0,sample:1,building:1,station:2,train:3}[item.kind]; }
  draw(camera: Camera, width: number, height: number, dpr: number, options: MapOptions) {
    this.needsFrame=false;
    const start=performance.now(), ctx=this.canvas.getContext('2d')!;
    if (this.canvas.width!==Math.round(width*dpr) || this.canvas.height!==Math.round(height*dpr)) {
      this.canvas.width=Math.round(width*dpr); this.canvas.height=Math.round(height*dpr);
    }
    ctx.setTransform(dpr,0,0,dpr,0,0); ctx.imageSmoothingEnabled=false;
    const decorative=options.demoTime!==undefined&&this.read?.worldEpoch==='local-demo-only';
    ctx.fillStyle=decorative?'#0072bc':options.night?'#17232b':'#d8e5d9'; ctx.fillRect(0,0,width,height);
    this.hits=[]; let drawn=0, culled=0;
    if (!this.read || !this.space) return false;
    if(isTerrainWorld(this.read)||isMixedWorld(this.read)) {
      const read=this.read,variant={season:options.winter?'winter' as const:options.season??'spring',night:options.night,drawPoles:options.drawPoles!==false,drawSpeedLimits:options.drawSpeedLimits!==false};
      const worldKey=JSON.stringify([read.worldEpoch,read.requiredPacks,read.map,decorative]);
      const presentationKey=JSON.stringify([worldKey,variant,options.heightCut,read.mixed?.fleet?.colorTest]);
      const key=JSON.stringify([presentationKey,read.terrain!.geometryRevision,read.mixed?.geometryRevision,
        read.mixed?.fleet?.colorTest,read.mixed?.fleet?.trains.map(t=>[t.isReversed,t.cars])]);
      if(this.terrainKey!==key) {
        this.canvas.dataset.presentation='pending';
        // Keep the front buffer during edits and sprite/cut changes in this world.
        // setScene renders into the spare buffer and flips only when complete.
        if(this.terrainWorldKey!==worldKey){this.terrainScene=undefined;this.terrainViewport=undefined;this.demoRailway=undefined;this.terrainView=undefined;}
        this.terrainKey=key;this.terrainWorldKey=worldKey;this.terrainError=undefined;
        const hash=read.requiredPacks[0].catalogHash;
        if(this.catalogHash!==hash){this.catalogHash=hash;this.catalog=undefined;this.terrainCache=new RecipeImageCache();this.colorCatalog=new TrainColorCatalog(this.terrainCache);}
        this.catalog??=loadRenderCatalog(hash);
        this.catalog.then(c=>this.colorCatalog.prepare(c,read.mixed?.fleet?.colorTest)).then(c=>Promise.all([TerrainScene.prepare(read,c,this.terrainCache,variant,options.heightCut,this.terrainScene),decorative?DemoRailway.prepare(c,this.terrainCache):undefined,Promise.resolve(c)])).then(([scene,railway,c])=>{
          if(this.terrainKey===key){
            this.renderCatalog=c;this.terrainScene=scene;this.terrainPreparedKey=key;
            if(!decorative&&scene.supportsViewport&&typeof OffscreenCanvas!=='undefined') {
              if(this.terrainViewport)this.terrainViewport.setScene(scene);
              else this.terrainViewport=new TerrainViewport(scene);
            }else this.terrainViewport=undefined;
            this.demoRailway=railway;this.invalidate();
          }
        }).catch(error=>{if(this.terrainKey===key){this.terrainError=error;this.invalidate();}});
      }
      if(this.terrainError){this.canvas.dataset.assets='failed';throw this.terrainError;}
      this.canvas.dataset.assets=this.terrainScene?'ready':'loading';
      if(!this.terrainScene)return false;
      this.terrainView={x:width/2-camera.x*camera.zoom,y:height/2-camera.y*camera.zoom,zoom:camera.zoom};
      const progressive=this.terrainViewport?.draw(ctx,this.terrainView,width,height,dpr);
      this.needsFrame=progressive?.pending??false;
      // The previous scene stays usable while replacement assets/raster work
      // finish. Expose completion separately from retained-buffer readiness.
      this.canvas.dataset.presentation=this.terrainPreparedKey===key&&(progressive?progressive.settled:true)?'ready':'pending';
      if(this.canvas.dataset.presentation==='ready')this.canvas.dataset.renderRevision=read.revision;
      this.canvas.dataset.refinement=progressive?.pending?'pending':'ready';
      this.canvas.dataset.terrainUpdate=progressive?.updateKind??'full';
      this.canvas.dataset.renderZoom=String(progressive?.renderZoom??camera.zoom);
      if(progressive&&!progressive.ready){this.canvas.dataset.assets='loading';return false;}
      const count=progressive?.drawn??(this.demoRailway
        ?this.demoRailway.draw(ctx,this.terrainScene,this.terrainView,width,height,dpr,options.demoTime!)
        :this.terrainScene.draw(ctx,this.terrainView,width,height));
      if(this.demoRailway){this.canvas.dataset.demoLoop=String(demoLoop.length);this.canvas.dataset.demoTrain=JSON.stringify(demoCar(options.demoTime!,0));}
      this.motion.setEnabled(options.smoothTrains!==false);
      const moving=this.motion.sample(options.motionTime??performance.now());
      this.needsFrame ||= moving.length>0;
      const completed=this.terrainViewport?.presentation??{scene:this.terrainScene,operations:this.terrainScene.operations};
      this.motionPainted=false;
      if(this.renderCatalog&&moving.length) {
        const stats=this.compositor.draw(ctx,this.terrainView,width,height,completed.scene,completed.operations,this.renderCatalog,moving,read.map.width,Math.trunc((read.map.height-1)/2),'view' in completed?completed as {view:import('./content-recipes.ts').View;width:number;height:number}:undefined);
        this.motionPainted=stats.cars>0;
        this.canvas.dataset.movingCars=String(stats.cars);this.canvas.dataset.motionOperations=String(stats.operations);
      }else {this.canvas.dataset.movingCars='0';this.canvas.dataset.motionOperations='0';}
      this.canvas.dataset.trainMotion=this.motionPainted?'smooth':'discrete';
      this.canvas.dataset.trainMotionPhase=this.motionPainted?(this.motion.buffering?'buffering':'playing'):'discrete';
      this.canvas.dataset.trainMotionPose=this.motionPainted&&moving[0]?JSON.stringify(this.compositor.position(moving[0].carId)):'';
      const selected=options.selection;
      const selectedPosition=selected?.kind==='train'&&'carId' in selected
        ?(this.motionPainted?this.compositor.position(selected.carId):undefined)??read.mixed?.fleet?.trains.find(t=>t.id===selected.id)?.cars.find(c=>c.id===selected.carId)?.state.location??undefined:selected?.position;
      for(const [position,color] of [[options.preview,'#126e79'],[selectedPosition,'#ffeaa0']] as const) {
        if(!position||position.z>options.heightCut)continue;
        const q={x:16*(position.x-Math.trunc((read.map.height-1)/2)+position.y)+16,y:8*(position.y-position.x+Math.trunc((read.map.height-1)/2))-16*position.z+8};
        const p={x:(q.x-camera.x)*camera.zoom+width/2,y:(q.y-camera.y)*camera.zoom+height/2},z=camera.zoom;
        ctx.beginPath();ctx.moveTo(p.x,p.y-8*z);ctx.lineTo(p.x+16*z,p.y);ctx.lineTo(p.x,p.y+8*z);ctx.lineTo(p.x-16*z,p.y);ctx.closePath();ctx.strokeStyle=color;ctx.lineWidth=2;ctx.stroke();
      }
      const vertex=options.terrainCorner&&terrainCornerPoint(read,options.terrainCorner);
      this.canvas.dataset.terrainCorner=options.terrainCorner?`${options.terrainCorner.x},${options.terrainCorner.y}`:'';
      if(vertex&&options.terrainCorner!.z<=options.heightCut){
        const x=(vertex.x-camera.x)*camera.zoom+width/2,y=(vertex.y-camera.y)*camera.zoom+height/2;
        ctx.beginPath();ctx.arc(x,y,5.5*camera.zoom,0,2*Math.PI);
        ctx.fillStyle='#ffeaa0aa';ctx.fill();ctx.strokeStyle='#126e79';ctx.lineWidth=2;ctx.stroke();
        ctx.beginPath();ctx.arc(x,y,2*camera.zoom,0,2*Math.PI);ctx.fillStyle='#126e79';ctx.fill();
      }
      for(const site of options.terrainSites??[]) {
        if(site.position.z>options.heightCut)continue;
        const p=toScreen(site.position,camera,width,height,this.space);ctx.strokeStyle=site.skipped?'#b00020':options.remove?'#bf4a19':'#126e79';ctx.fillStyle=site.skipped?'#b0002030':options.remove?'#bf4a1940':'#126e7940';ctx.lineWidth=2;
        ctx.beginPath();ctx.moveTo(p.x,p.y-8*camera.zoom);ctx.lineTo(p.x+16*camera.zoom,p.y);ctx.lineTo(p.x,p.y+8*camera.zoom);ctx.lineTo(p.x-16*camera.zoom,p.y);ctx.closePath();ctx.fill();ctx.stroke();
      }
      // The ghost uses the server's exact visited cells and patterns. A height
      // cut hides the overlay at that level just as it hides constructed rail.
      ctx.save();
      const removal=options.removalPath??[];
      this.canvas.dataset.removalPathCells=String(removal.length);
      if(removal.length>1){
        ctx.strokeStyle='#bf4a19';ctx.lineWidth=2;ctx.setLineDash([5,3]);ctx.beginPath();let started=false;
        for(const position of removal){
          if(position.z>options.heightCut)continue;
          const p=toScreen(position,camera,width,height,this.space);
          if(started)ctx.lineTo(p.x,p.y);else{ctx.moveTo(p.x,p.y);started=true;}
        }
        ctx.stroke();ctx.setLineDash([]);
      }
      const limits=read.mixed?.speedLimits;
      this.canvas.dataset.speedLimitCells=String(limits?new Set([...limits.low,...limits.medium,...limits.fast].map(p=>`${p.x},${p.y},${p.z}`)).size:0);
      const rail=options.railPreview;
      if(rail){
        ctx.strokeStyle=rail.legal?(rail.query.action==='remove'?'#bf4a19':'#087e63'):'#b00020';ctx.lineWidth=3;
        ctx.beginPath();let started=false;
        // Rejected topology can have no cells. Keep the requested segments
        // visible so the invalid route still has clear feedback on the map.
        const route=rail.cells.length?rail.cells.map(cell=>cell.position):rail.legal?[]:[rail.query.from,...(rail.query.via??[]),rail.query.to];
        if(rail.legal&&rail.query.via?.length&&rail.query.from.x===rail.query.to.x&&rail.query.from.y===rail.query.to.y&&rail.query.from.z===rail.query.to.z)route.push(rail.query.from);
        for(const position of route){
          if(position.z>options.heightCut)continue;
          const p=toScreen(position,camera,width,height,this.space);
          if(started)ctx.lineTo(p.x,p.y);else {ctx.moveTo(p.x,p.y);started=true;}
        }
        ctx.stroke();
        for(const loss of rail.losses){
          if(loss.position.z>options.heightCut)continue;
          const p=toScreen(loss.position,camera,width,height,this.space),r=5*camera.zoom;
          ctx.beginPath();ctx.moveTo(p.x-r,p.y-r);ctx.lineTo(p.x+r,p.y+r);ctx.moveTo(p.x+r,p.y-r);ctx.lineTo(p.x-r,p.y+r);ctx.stroke();
        }
      }
      const slope=options.slopePreview,space=this.space;
      this.canvas.dataset.slopeGhostCells=String(slope?.members.length??0);
      if(slope){
        ctx.strokeStyle=slope.legal?(slope.query.action==='remove'?'#bf4a19':'#087e63'):'#b00020';
        ctx.lineWidth=2;
        const diamond=(position:Position)=>{
          if(position.z>options.heightCut)return;
          const p=toScreen(position,camera,width,height,space),rx=14*camera.zoom,ry=7*camera.zoom;
          ctx.beginPath();ctx.moveTo(p.x-rx,p.y);ctx.lineTo(p.x,p.y-ry);ctx.lineTo(p.x+rx,p.y);ctx.lineTo(p.x,p.y+ry);ctx.closePath();ctx.stroke();
        };
        for(const m of slope.members){ctx.setLineDash(m.kind==='traffic'?[]:[5,3]);diamond(m.position);}
        ctx.setLineDash([1,3]);for(const p of slope.addedPiers)diamond(p);
        ctx.setLineDash([]);
        for(const loss of slope.losses){
          if(loss.position.z>options.heightCut)continue;
          const p=toScreen(loss.position,camera,width,height,this.space),r=5*camera.zoom;
          ctx.beginPath();ctx.moveTo(p.x-r,p.y-r);ctx.lineTo(p.x+r,p.y+r);ctx.moveTo(p.x+r,p.y-r);ctx.lineTo(p.x-r,p.y+r);ctx.stroke();
        }
      }
      this.canvas.dataset.fleetGhostCars=String(options.fleetPreview?.length??0);
      for(const [i,car] of (options.fleetPreview??[]).entries()){
        const position=car.location;if(position.z>options.heightCut)continue;
        const p=toScreen(position,camera,width,height,this.space),rx=15*camera.zoom,ry=8*camera.zoom;
        ctx.fillStyle='#0c9c8880';ctx.strokeStyle='#006451';ctx.lineWidth=2;
        ctx.beginPath();ctx.moveTo(p.x-rx,p.y);ctx.lineTo(p.x,p.y-ry);ctx.lineTo(p.x+rx,p.y);ctx.lineTo(p.x,p.y+ry);ctx.closePath();ctx.fill();ctx.stroke();
        const [dx,dy]=[[0,-1],[1,-1],[1,0],[1,1],[0,1],[-1,1],[-1,0],[-1,-1]][car.heading];
        const next=toScreen({...position,x:position.x+dx,y:position.y+dy},camera,width,height,this.space),end={x:p.x+(next.x-p.x)*.6,y:p.y+(next.y-p.y)*.6};
        const angle=Math.atan2(end.y-p.y,end.x-p.x);ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(end.x,end.y);ctx.lineTo(end.x-6*Math.cos(angle-.6),end.y-6*Math.sin(angle-.6));ctx.moveTo(end.x,end.y);ctx.lineTo(end.x-6*Math.cos(angle+.6),end.y-6*Math.sin(angle+.6));ctx.stroke();
        ctx.fillStyle='#003c32';ctx.font='bold 12px sans-serif';ctx.textAlign='center';ctx.fillText(String(i+1),p.x,p.y-12*camera.zoom);
      }
      const illegalRoute=rail?.legal===false||slope?.legal===false;
      for(const {position,label} of options.railPoints??[]){
        if(position.z>options.heightCut)continue;
        const p=toScreen(position,camera,width,height,this.space);
        ctx.fillStyle=illegalRoute?'#ffe3e8':'#fff';ctx.strokeStyle=illegalRoute?'#b00020':'#126e79';ctx.lineWidth=2;ctx.beginPath();ctx.arc(p.x,p.y,10,0,Math.PI*2);ctx.fill();ctx.stroke();
        ctx.fillStyle=illegalRoute?'#b00020':'#174b50';ctx.font='bold 12px sans-serif';ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(label,p.x,p.y);
      }
      ctx.restore();
      if(options.preview && options.rectangleEnd && options.preview.z<=options.heightCut){
        const a=options.preview,b=options.rectangleEnd;
        const vertices=[a,{x:b.x,y:a.y,z:a.z},b,{x:a.x,y:b.y,z:a.z}];
        ctx.beginPath();vertices.forEach((v,i)=>{const p=toScreen(v,camera,width,height,this.space!);if(i)ctx.lineTo(p.x,p.y);else ctx.moveTo(p.x,p.y);});
        ctx.closePath();ctx.strokeStyle='#126e79';ctx.lineWidth=2;ctx.stroke();
      }
      const stationLabels=options.stationLabels;
      if(stationLabels?.visible){
        ctx.save();ctx.font=`${stationLabels.size}px ${stationLabels.font}`;ctx.textAlign='center';ctx.textBaseline='top';
        for(const p of stationNamePositions(read)) {
          const x=(p.x-camera.x)*camera.zoom+width/2,y=(p.y-camera.y)*camera.zoom+height/2;
          if(x< -256||x>width+256||y< -stationLabels.size||y>height)continue;
          ctx.fillStyle='#000000';ctx.fillText(p.name,x+1,y+1);ctx.fillStyle=stationLabels.color;ctx.fillText(p.name,x,y);
        }ctx.restore();
      }
      const labels=options.trainLabels;
      if(labels?.visible){
        ctx.save();ctx.font=`${labels.size}px ${labels.font}`;ctx.textAlign='center';ctx.textBaseline='bottom';
        for(const train of read.mixed?.fleet?.trains??[]){
          const head=train.cars[0];if(head.state.kind!=='inside'||head.state.location!.z>options.heightCut)continue;
          const center=this.trainCenter(train.id);if(!center)continue;
          const x=(center.x-camera.x)*camera.zoom+width/2,y=(center.y-camera.y)*camera.zoom+height/2-14*camera.zoom;
          if(x< -256||x>width+256||y<0||y>height+labels.size)continue;
          ctx.lineWidth=3;ctx.strokeStyle='#202020';ctx.strokeText(train.name,x,y);ctx.fillStyle=labels.color;ctx.fillText(train.name,x,y);
        }ctx.restore();
      }
      this.canvas.dataset.drawn=String(count);this.canvas.dataset.culled=String(Math.max(0,this.terrainScene.operations.length-count));
      this.canvas.dataset.paintMs=(performance.now()-start).toFixed(2);
      this.canvas.dataset.zoom=camera.zoom.toFixed(3);this.canvas.dataset.camera=`${camera.x.toFixed(1)},${camera.y.toFixed(1)}`;
      this.canvas.dataset.coordinateSystem=read.map.coordinateSystem;this.canvas.dataset.ruleset=read.ruleset;this.canvas.dataset.worldEpoch=read.worldEpoch;
      this.canvas.dataset.headings='';
      this.canvas.dataset.terrainVariant=`${variant.season}/${variant.night?'night':'day'}`;
      this.canvas.dataset.terrainCut=String(options.heightCut);
      return true;
    }
    this.terrainKey='';this.terrainWorldKey='';this.terrainScene=undefined;this.terrainViewport=undefined;this.demoRailway=undefined;this.terrainView=undefined;
    const space=this.space;
    const screen=(p:Position)=>toScreen(p,camera,width,height,space);
    const zoom=camera.zoom;
    const diamond=(p:Point,color:string,stroke?:string) => {
      ctx.beginPath(); ctx.moveTo(p.x,p.y-8*zoom);ctx.lineTo(p.x+16*zoom,p.y);ctx.lineTo(p.x,p.y+8*zoom);ctx.lineTo(p.x-16*zoom,p.y);ctx.closePath();
      ctx.fillStyle=color;ctx.fill();if(stroke){ctx.strokeStyle=stroke;ctx.lineWidth=1;ctx.stroke();}
    };
    const sprite=(key:SpriteKey,p:Point) => {
      const s=content.sprites[key], x=p.x-16*zoom, y=p.y-(s.offset+8)*zoom;
      if (!visible(x,y,s.width*zoom,s.height*zoom,width,height)) {culled++;return false;}
      const path=options.night?s.nightImage:options.recolor?s.tintImage:s.image;
      ctx.drawImage(this.images.get(path)!,x,y,s.width*zoom,s.height*zoom);drawn++; return true;
    };
    for(const cell of space.ground()) {
      const {x,y}=cell, p=screen(cell);
      if(!visible(p.x-16*zoom,p.y-8*zoom,32*zoom,16*zoom,width,height)){culled++;continue;}
      // Decorative water beneath the fixed pier; no simulation terrain mutation.
      const water=this.read.map.coordinateSystem==='prototypeXY'&&x>=14&&x<=18&&y>=14&&y<=18;
      const color=water?(options.night?'#18334d':'#81b9c6'):options.night?'#263b35':options.winter?'#e5ecea':(x+y)%2?'#aec49f':'#b5c9a6';
      diamond(p,color,options.night?'#30463b':'#a1b795');drawn++;
    }
    for(const item of this.items) {
      if(item.position.z>options.heightCut) continue;
      const p=screen(item.position);
      const building=this.buildings.get(item.id);
      const cut=item.kind==='building' && building && building.position.z+building.height-1>options.heightCut;
      if(cut) {
        if(item.position.z!==building.position.z) continue;
        if(!visible(p.x-16*zoom,p.y-8*zoom,32*zoom,16*zoom,width,height)){culled++;continue;}
        diamond(p,'#d7b883','#705c42');this.hits.push({item:{...item,cut:true},...p});drawn++;continue;
      }
      if(item.sprite) { if(sprite(item.sprite,p)&&item.kind!=='sample')this.hits.push({item,...p});continue; }
      if(!visible(p.x-20*zoom,p.y-20*zoom,40*zoom,40*zoom,width,height)){culled++;continue;}
      if(item.kind==='train'&&item.heading!==undefined) {
        // Spatial marker for diagonal headings until P2 supplies canonical car frames.
        const d=offset(item.heading), dx=16*(d.x+d.y),dy=8*(d.y-d.x),length=Math.hypot(dx,dy);
        const ux=dx/length,uy=dy/length;
        ctx.beginPath();ctx.moveTo(p.x+ux*12*zoom,p.y+uy*12*zoom);
        ctx.lineTo(p.x-ux*9*zoom-uy*5*zoom,p.y-uy*9*zoom+ux*5*zoom);
        ctx.lineTo(p.x-ux*9*zoom+uy*5*zoom,p.y-uy*9*zoom-ux*5*zoom);ctx.closePath();ctx.fillStyle='#e3c65e';ctx.fill();
      } else if(item.shape) {
        diamond(p,options.night?'#444548':'#b5b7ad');
        // Draw each port as a connected half rail. Curves/junctions follow the host topology.
        for(const port of this.read.map.coordinateSystem==='hv' ? (normalPorts(item.shape)??[]).map(offset) : ports[item.shape as keyof typeof ports]) {
          const q={x:8*(port.x+port.y),y:4*(port.y-port.x)},base={x:0,y:0};
          ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(p.x+(q.x-base.x)*zoom,p.y+(q.y-base.y)*zoom);
          ctx.strokeStyle=options.night?'#a4b0b5':'#44575c';ctx.lineWidth=5*zoom;ctx.stroke();
          ctx.strokeStyle='#e1dacc';ctx.lineWidth=zoom;ctx.stroke();
        }
        if(item.shape==='buffer'){ctx.fillStyle='#b8593c';ctx.fillRect(p.x-3*zoom,p.y-4*zoom,6*zoom,8*zoom);}
      } else if(item.kind==='station') {
        ctx.fillStyle=options.night?'#aaa277':'#f1d58c';ctx.fillRect(p.x-16*zoom,p.y-15*zoom,32*zoom,7*zoom);
      }
      this.hits.push({item,...p});drawn++;
    }
    if(options.preview && options.preview.z<=options.heightCut) {
      const p=screen(options.preview);
      if(options.anchor) {
        const a=screen(options.anchor);ctx.beginPath();ctx.moveTo(a.x,a.y);ctx.lineTo(p.x,p.y);ctx.strokeStyle='#126e79';ctx.lineWidth=zoom;ctx.stroke();
        diamond(a,'#ffffff33','#126e79');
      }
      diamond(p,options.remove?'#d86a5377':'#4ba2a177','#fff');
      ctx.strokeStyle=options.remove?'#b63128':'#126e79';ctx.lineWidth=3*zoom;
      if(options.remove){ctx.beginPath();ctx.moveTo(p.x-7*zoom,p.y-4*zoom);ctx.lineTo(p.x+7*zoom,p.y+4*zoom);ctx.moveTo(p.x+7*zoom,p.y-4*zoom);ctx.lineTo(p.x-7*zoom,p.y+4*zoom);ctx.stroke();}
      else if(this.read.map.coordinateSystem==='prototypeXY') for(const port of (ports[options.shape as keyof typeof ports]??[])){ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(p.x+8*(port.x+port.y)*zoom,p.y+4*(port.y-port.x)*zoom);ctx.stroke();}
    }
    if(options.selection) {
      const selected=options.selection;
      const position=selected.kind==='train'?this.read.trains.find(train=>train.id===selected.id)?.position:selected.position;
      if(position&&position.z<=options.heightCut)diamond(screen(position),'#ffffff22','#ffeaa0');
    }
    this.canvas.dataset.drawn=String(drawn);this.canvas.dataset.culled=String(culled);
    this.canvas.dataset.paintMs=(performance.now()-start).toFixed(2);
    this.canvas.dataset.zoom=camera.zoom.toFixed(3);this.canvas.dataset.camera=`${camera.x.toFixed(1)},${camera.y.toFixed(1)}`;
    this.canvas.dataset.assets='ready';
    this.canvas.dataset.coordinateSystem=this.read.map.coordinateSystem;
    this.canvas.dataset.ruleset=this.read.ruleset;
    this.canvas.dataset.worldEpoch=this.read.worldEpoch;
    this.canvas.dataset.headings=this.read.trains.flatMap(train=>train.carHeadings).join(',');
    return true;
  }
  /** Controller geometry from the same completed scene as the visible pixels. */
  pickLocation(p:Point,policy:RailwayPickPolicy):Position|undefined {
    if(!this.read||!(isTerrainWorld(this.read)||isMixedWorld(this.read))||this.terrainError||!this.terrainScene||!this.terrainView)return;
    return this.terrainViewport?this.terrainViewport.pickLocation(p,this.terrainView,policy):this.terrainScene.pickLocation(p,this.terrainView,policy);
  }
  pick(p:Point,zoom:number):Selection|undefined {
    if(this.read&&(isTerrainWorld(this.read)||isMixedWorld(this.read))) {
      if(this.terrainError||!this.terrainScene||!this.terrainView)return;
      if(this.motionPainted)return this.compositor.pick(p,this.terrainView);
      return this.terrainViewport?this.terrainViewport.pick(p,this.terrainView):this.terrainScene.pick(p,this.terrainView);
    }
    for(let i=this.hits.length-1;i>=0;i--) {
      const {item,x,y}=this.hits[i]; let hit=false;
      if(item.sprite && !item.cut) {
        const s=content.sprites[item.sprite];hit=opaqueAt(s,(p.x-x)/zoom+16,(p.y-y)/zoom+s.offset+8);
      } else if(item.kind==='station') hit=Math.abs(p.x-x)<=16*zoom && p.y>=y-15*zoom && p.y<=y-8*zoom;
      else hit=Math.abs(p.x-x)/(16*zoom)+Math.abs(p.y-y)/(8*zoom)<=1;
      if(hit&&item.kind!=='sample')return {kind:item.kind,id:item.id,position:item.position};
    }
  }
}
