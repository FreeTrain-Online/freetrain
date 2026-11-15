import type {ReadState} from './session-client.ts';

export const quoteValidityMinutes=720n;
export type QuoteIdentity={expectedWorldEpoch:string;expectedContentVersion?:string;expectedMinute?:string;
 expectedRevision?:string;expectedGeometryRevision?:string};

// Global revisions describe everything the simulation has done. A confirmation
// is instead re-reviewed by the server against its original affected-site quote.
export function currentQuote(read:ReadState|undefined,query:QuoteIdentity):boolean {
 if(!read||read.worldEpoch!==query.expectedWorldEpoch||query.expectedContentVersion!==undefined&&read.contentVersion!==query.expectedContentVersion)return false;
 const revision=query.expectedRevision,geometry=query.expectedGeometryRevision;
 if(revision!==undefined&&(BigInt(revision)<0n||BigInt(revision)>BigInt(read.revision)))return false;
 const actual=read.mixed?.geometryRevision??read.terrain?.geometryRevision;
 if(geometry!==undefined&&(actual===undefined||BigInt(geometry)<0n||BigInt(geometry)>BigInt(actual)))return false;
 if(query.expectedMinute===undefined)return geometry===undefined||geometry===actual;
 const minute=BigInt(query.expectedMinute),now=BigInt(read.tick);
 return minute>=0n&&minute<=now&&now-minute<=quoteValidityMinutes;
}
export const constructionFence=(read?:ReadState)=>`${read?.worldEpoch}/${read?.contentVersion}/${read?.terrain?.constructionMode}`;
