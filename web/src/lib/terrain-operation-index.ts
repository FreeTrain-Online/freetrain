import type {TerrainOperation,TerrainScene} from './terrain-renderer.ts';

type Bounds={left:number;top:number;right:number;bottom:number};
const overlaps=(a:Bounds,b:Bounds)=>a.left<=b.right&&a.right>=b.left&&a.top<=b.bottom&&a.bottom>=b.top;

/** Shared by retained terrain and animated damage replay. Local publications
 * change only their columns; spatial queries preserve exact source paint order. */
export class TerrainOperationIndex {
 readonly cars=new Map<string,TerrainOperation>();
 private columns=new Map<number,TerrainOperation[]>();
 private buckets=new Map<string,Set<TerrainOperation>>();
 private order=new WeakMap<TerrainOperation,{column:number;slot:number}>();
 constructor(private scene:TerrainScene,public operations:readonly TerrainOperation[]) {
  for(const op of operations) {
   const column=scene.columnIndex(op),group=this.columns.get(column)??[];
   group.push(op);this.columns.set(column,group);
  }
  for(const [column,group] of this.columns)group.forEach((op,slot)=>this.add(op,column,slot));
 }
 private *keys(b:Bounds) {
  for(let y=Math.floor(b.top/64);y<=Math.floor(b.bottom/64);y++)for(let x=Math.floor(b.left/64);x<=Math.floor(b.right/64);x++)yield `${x},${y}`;
 }
 private add(op:TerrainOperation,column:number,slot:number) {
  this.order.set(op,{column,slot});
  if(op.target.kind==='train')this.cars.set(op.target.carId,op);
  for(const key of this.keys(this.scene.boundsFor(op))) {
   const bucket=this.buckets.get(key)??new Set<TerrainOperation>();bucket.add(op);this.buckets.set(key,bucket);
  }
 }
 column(column:number):readonly TerrainOperation[] {return this.columns.get(column)??[];}
 compare(a:TerrainOperation,b:TerrainOperation):number {
  const x=this.order.get(a)!,y=this.order.get(b)!;return x.column-y.column||x.slot-y.slot;
 }
 query(bounds:Bounds):TerrainOperation[] {
  const found=new Set<TerrainOperation>();
  for(const key of this.keys(bounds))for(const op of this.buckets.get(key)??[])if(overlaps(this.scene.boundsFor(op),bounds))found.add(op);
  return [...found].sort((a,b)=>this.compare(a,b));
 }
 replace(scene:TerrainScene,changed:ReadonlyMap<number,readonly TerrainOperation[]>) {
  if(!changed.size){this.scene=scene;return;}
  const operations=[...this.operations];
  const lower=(column:number)=>{
   let lo=0,hi=operations.length;
   while(lo<hi){const mid=(lo+hi)>>>1;if(scene.columnIndex(operations[mid])<column)lo=mid+1;else hi=mid;}
   return lo;
  };
  // Descending replacements keep earlier source ranges stable. Copy the array
  // once so the previous scene's immutable operations remain available to picks.
  for(const [column,next] of [...changed].sort(([a],[b])=>b-a)) {
   const start=lower(column),end=lower(column+1);
   operations.splice(start,end-start,...next);
   for(const op of this.column(column)) {
    if(op.target.kind==='train')this.cars.delete(op.target.carId);
    for(const key of this.keys(this.scene.boundsFor(op))) {
     const bucket=this.buckets.get(key);bucket?.delete(op);if(!bucket?.size)this.buckets.delete(key);
    }
   }
  }
  this.scene=scene;
  // Remove all old car entries before adding any: a car may move between two
  // changed columns, whose numeric order is unrelated to its travel direction.
  for(const [column,next] of changed) {
   if(next.length)this.columns.set(column,[...next]);else this.columns.delete(column);
   next.forEach((op,slot)=>this.add(op,column,slot));
  }
  this.operations=operations;
 }
}
