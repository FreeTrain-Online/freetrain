/** Reuse only equal immutable data. Incoming records are compared recursively,
 * never trusted merely because their revision matches. Inputs are not mutated. */
export function retainUnchanged<T>(before:unknown,after:T):T {
 if(Object.is(before,after))return after;
 if(!before||!after||typeof before!=='object'||typeof after!=='object'||!Object.isFrozen(before)||Array.isArray(before)!==Array.isArray(after))return after;
 const a=before as Record<string,unknown>,b=after as Record<string,unknown>;
 const keys=Object.keys(b),old=Object.keys(a);
 if(keys.length!==old.length||keys.some(k=>!Object.hasOwn(a,k)))return after;
 let equal=true,result:Record<string,unknown>|undefined;
 for(const key of keys){
  const child=retainUnchanged(a[key],b[key]);
  if(!Object.is(child,a[key]))equal=false;
  if(!Object.is(child,b[key])){
   result??=Array.isArray(after)?[...after] as unknown as Record<string,unknown>:{...b};
   result[key]=child;
  }
 }
 return equal?before as T:(result as T|undefined)??after;
}
