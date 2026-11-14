// Stable ExAV ConstructionSite.VoxelImpl.draw. Original authors; LGPL-2.1-or-later.
/** Source pass order; connectivity is N/E/S/W in canonical XYZ. */
export function constructionParts(x:number,y:number,sizeX:number,sizeY:number,ground:boolean,c:{state:number;ground:number;needsDoor:boolean}):string[] {
  const connected=[y!==0,x!==sizeX-1,y!==sizeY-1,x!==0],parts:string[]=[];
  const fence=(i:number)=>{if(!connected[i])parts.push(`fence/${i}/${c.needsDoor?1:0}`);};
  if(ground){fence(0);fence(1);}
  if(c.state===0){if(ground)parts.push(`ground/${c.ground}`);}
  else if(c.state<4){if(ground)parts.push('ground/0');parts.push(`bones/${c.state-1}/${connected.map(n=>n?'1':'0').join('')}`);}
  else{for(const i of [0,1])if(!connected[i])parts.push(`wall/${i}`);parts.push(`bones/2/${connected.map(n=>n?'1':'0').join('')}`);for(const i of [2,3])if(!connected[i])parts.push(`wall/${i}`);}
  if(ground){fence(2);fence(3);}
  return parts;
}
