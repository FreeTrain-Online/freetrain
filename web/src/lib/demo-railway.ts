import { composeTrain, instantiate, traversal, type Layer, type RenderCatalog, type RecipeImageCache, type ReadyScene, type View } from './content-recipes.ts';
import { demoLoop, demoCar } from './demo-railway-path.ts';
import type { TerrainScene } from './terrain-renderer.ts';
import { demoSpace, demoDimensions } from './demo-terrain.ts';
const centerX=demoSpace.centerX;
/** Prepared art and a cached static backdrop. No protocol train/rail state,
 * session clock, gameplay random stream, command or picking target is created. */
export class DemoRailway {
  private background?:OffscreenCanvas;
  private backgroundKey='';
  private count=0;
  private constructor(private ready:ReadyScene,private rails:Layer[],private cars:Layer[][]) {}
  static async prepare(catalog:RenderCatalog,cache:RecipeImageCache):Promise<DemoRailway> {
    const recipes=new Map(catalog.renderRecipes.map(r=>[r.id,r]));
    const rails=demoLoop.flatMap(({position:p,pattern},i)=>{
      const recipe=recipes.get(`system:pattern/${pattern}`);
      if(!recipe)throw new Error('Decorative rail recipe unavailable');
      return instantiate(recipe,`demo-rail:${i}`,[p.x-centerX,p.y,p.z]);
    });
    const cars=composeTrain(catalog,'{3983B298-ADB1-4905-94E5-03B7AAE5A221}',3).map((id,i)=>{
      const recipe=recipes.get(id);if(!recipe)throw new Error('Decorative car recipe unavailable');
      return Array.from({length:16},(_,angle)=>{
        const layer=instantiate(recipe,`demo-car:${i}`,[0,0,demoDimensions.waterLevel],{angle:recipe.fields.asymmetric===false?angle%8:angle})[0];
        if(!layer||layer.frame.status!=='ready')throw new Error('Decorative car frame unavailable');
        // Train.Car.draw applies this offset before invoking the car recipe.
        return {...layer,frame:{...layer.frame,anchor:[0,9] as [number,number]}};
      });
    });
    if(rails.some(l=>l.frame.status!=='ready'))throw new Error('Decorative rail frame unavailable');
    return new DemoRailway(await cache.ready(catalog,[...rails,...cars.flat()],{season:'spring',night:false}),rails,cars);
  }
  draw(ctx:CanvasRenderingContext2D,terrain:TerrainScene,view:View,width:number,height:number,dpr:number,time:number):number {
    const key=JSON.stringify([view,width,height,dpr]);
    if(!this.background||this.backgroundKey!==key) {
      this.background=new OffscreenCanvas(Math.round(width*dpr),Math.round(height*dpr));
      const paint=this.background.getContext('2d')!;paint.setTransform(dpr,0,0,dpr,0,0);
      this.count=terrain.draw(paint as unknown as CanvasRenderingContext2D,view,width,height);
      this.ready.draw(paint as unknown as CanvasRenderingContext2D,view,{kind:'normal'},traversal(this.rails));
      this.backgroundKey=key;
    }
    ctx.drawImage(this.background,0,0,width,height);
    const cars=this.cars.map((frames,i)=>{
      const {position:p,angle}=demoCar(time,i);
      return {...frames[angle],position:[p.x-centerX,p.y,p.z] as [number,number,number]};
    });
    this.ready.draw(ctx,view,{kind:'normal'},traversal(cars));
    return this.count+this.rails.length+cars.length;
  }
}
