import { readFileSync, writeFileSync } from 'node:fs';
import { test, expect, type Page } from './fixtures.ts';
import type { StationBuilding, StationPlatform } from '../../src/generated/contracts.ts';
import { canonicalState as state, closePanel, exposeMapPoint } from './browser-helpers.ts';
import { evidencePath } from './assertions.ts';
import { panMapBy, projectMapPoint, renderedCamera, renderedTool } from './map-picking.ts';
const catalog=JSON.parse(readFileSync('public/content/bundled-v2/catalog.json','utf8'));

export async function pickStationObject(page:Page,object:StationBuilding|StationPlatform,text:Record<string,string>,touch:boolean,hasController=false){return test.step("Pick the intended station or platform",async()=>{
 await closePanel(page);await page.getByRole('button',{name:text.referenceInspect,exact:true}).click();
 await renderedTool(page,'inspect');
 const recipe=('kind' in object)?object.kind==='wide'?`system:fat-${object.direction/2%2}`:`system:thin-${object.direction}`:object.definition.recipeId;
 const frame=catalog.renderRecipes.find((r:any)=>r.id===recipe).frames.find((f:any)=>f.status==='ready');
 const mask=catalog.masks[frame.masks['spring/day']];let sample:any;
 for(let y=1;y<mask.length-1&&!sample;y++)for(let x=1;x<mask[y].length-1&&!sample;x++)
  if(mask.slice(y-1,y+2).every((r:string)=>r.slice(x-1,x+2)==='111'))sample={x:x+.5,y:y+.5};
 expect(sample).toBeTruthy();const center=Math.floor(((await state(page)).map.height-1)/2);
 const location=('kind' in object)?{...object.location,x:object.location.x+object.length-1}:object.location;
 const q={x:16*(location.x+frame.cell[0]-center+location.y+frame.cell[1])-frame.anchor[0]+sample.x,
  y:8*(location.y+frame.cell[1]-location.x-frame.cell[0]+center)-16*(object.location.z+frame.cell[2])-frame.anchor[1]+sample.y};
 // Bring the exact painted opaque sample into view, then clear the narrow sheet.
 const before=await renderedCamera(page);
 const centered=await panMapBy(page,{x:(q.x-before.x)*before.zoom,y:(q.y-before.y)*before.zoom});
 await exposeMapPoint(page,(await projectMapPoint(page,q,centered)).point);
 const camera=await renderedCamera(page);
 const screenshot=evidencePath(test.info(),`pick-${object.id}`,'png');
 await page.screenshot({path:screenshot});await test.info().attach(`pick-${object.id}`,{path:screenshot,contentType:'image/png'});
 const details=evidencePath(test.info(),`pick-${object.id}`,'json');
 // Screenshot capture can yield to rendering. Compute the final coordinates
 // afterwards, directly before the one real mouse/touch input.
 const {point:p}=await projectMapPoint(page,q,camera);
 try{
  if(touch)await page.touchscreen.tap(p.x,p.y);else await page.mouse.click(p.x,p.y);
  await expect(page.getByTestId(('kind' in object)?'platform-identity':'selected-object'),`Pick must select ${('kind' in object)?'platform':'station'} ${object.id}`).toContainText(` · ${object.id} · `);
  if(hasController)await expect(page.locator('.primary-panel .fleet-controller-manager')).toBeVisible();
 }finally{
  writeFileSync(details,JSON.stringify({object,q,p,camera}));await test.info().attach(`pick-${object.id}-details`,{path:details,contentType:'application/json'});
 }
});}
