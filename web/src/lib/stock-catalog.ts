import stocks from '../generated/stock-catalog.json' with {type:'json'};
import profile from '../generated/fleet-profile.json' with {type:'json'};
import english from '../../../content/localization/exav-stable-bundled/stock-en.json' with {type:'json'};
import type {Locale} from './localization.ts';
import type {ReadState} from './session-client.ts';
import type {FleetTradeQuery} from '../generated/contracts.ts';
export type Stock = typeof stocks[number];
export {stocks};
export function permittedLengths(lengths:number[]):string {
  const ranges:string[]=[];
  for(let i=0;i<lengths.length;i++){
    const first=lengths[i];let last=first;
    while(lengths[i+1]===last+1)last=lengths[++i];
    ranges.push(first===last?String(first):`${first}–${last}`);
  }
  return ranges.join(', ');
}
export function stockText(stock:Stock,field:'company'|'type'|'name'|'author'|'description',locale:Locale):string {
  const entry=(english.entries as Record<string,{original:string;text:string}>)[`${stock.id}/${field}`];
  return locale==='en'&&english.catalogHash===stock.binding.catalogHash&&entry?.original===stock[field]?entry.text:stock[field];
}
export function stockAvailable(stock:Stock,read:ReadState):boolean {
  return ['terrain-state-1','mixed-railway-1'].includes(read.ruleset??'') && !!read.requiredPacks?.some(p=>p.id===stock.binding.packId&&p.version===stock.binding.version&&p.catalogHash===stock.binding.catalogHash);
}
export function formationCars(stock:Stock,length:number):string[] {
  if(!stock.lengths.includes(length))return [];
  const row=profile.trains.find(t=>t.binding.contributionId===stock.id)!;
  if(!row.body)return (row.patterns as string[][]).find(p=>p.length===length)??[];
  const cars=Array<string>(length).fill(row.body);
  if(row.head)cars[0]=row.head;if(row.tail)cars[length-1]=row.tail;
  return cars;
}
export function formationPreviewCars(stock:Stock,length:number):string[] {
  const cars=formationCars(stock,length);
  const row=profile.trains.find(t=>t.binding.contributionId===stock.id)!;
  // Purchase artwork has one leading cab and connected carriages. A rear cab
  // facing backwards is useful on the map, but reads as another engine here.
  return cars.map((id,index)=>index===0?(row.head??id):(row.body??id));
}
export function tradeQuery(read:ReadState,stock:Stock,length:number,quantity:number):FleetTradeQuery {
  return {expectedWorldEpoch:read.worldEpoch,expectedRevision:read.revision,expectedMinute:read.tick,
    expectedContentVersion:read.contentVersion!,action:'buy',definition:stock.binding,length,quantity,trainId:'0'};
}
export function currentQuote(read:ReadState,query:Pick<FleetTradeQuery,'expectedWorldEpoch'|'expectedRevision'|'expectedMinute'|'expectedContentVersion'>):boolean {
  return read.worldEpoch===query.expectedWorldEpoch&&read.revision===query.expectedRevision&&read.tick===query.expectedMinute&&read.contentVersion===query.expectedContentVersion;
}
