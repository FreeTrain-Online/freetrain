import type {ReadState} from './session-client.ts';
type Entity=NonNullable<ReadState['entities']>[number];
const indexes=new WeakMap<readonly Entity[],ReadonlyMap<string,Entity>>();
/** Immutable transport arrays own their index weakly, so superseded worlds can
 * be collected. Never cache mutable/unvalidated caller-owned arrays. */
export function entitiesById(entities:readonly Entity[]):ReadonlyMap<string,Entity> {
 let index=Object.isFrozen(entities)?indexes.get(entities):undefined;
 if(!index){index=new Map(entities.map(e=>[e.id,e]));if(Object.isFrozen(entities))indexes.set(entities,index);}
 return index;
}
