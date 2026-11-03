import {stocks,formationCars,stockText,currentQuote,tradeQuery,stockAvailable,permittedLengths} from '../src/lib/stock-catalog.ts';
import {parseFleetTradePreview} from '../src/lib/protocol.ts';
import fleet from '../../prototype/protocol/fixtures/snapshot-fleet.json' with {type:'json'};
import assert from 'node:assert/strict';
const assertEquals=assert.deepEqual,assertThrows=assert.throws;

Deno.test('stock catalog retains six exact identities, source labels and permitted formation endpoints',()=>{
  assertEquals(stocks.length,6);
  assertEquals(permittedLengths([1,2,3,5,7,8]),'1–3, 5, 7–8');
  assertEquals(new Set(stocks.map(s=>s.id)).size,6);
  const raicho=stocks.find(s=>s.name==='雷鳥')!;
  assertEquals(stockText(raicho,'name','en'),'Raicho');assertEquals(stockText(raicho,'name','ja'),'雷鳥');
  assertEquals(stockText(raicho,'company','en'),'JR East');
  assertEquals(stockText({...raicho,name:'changed'},'name','en'),'changed');
  assertEquals(formationCars(raicho,1),['{3983B298-ADB1-4905-94E5-03B7AAE5A221}-t']);
  assertEquals(formationCars(raicho,3),['{3983B298-ADB1-4905-94E5-03B7AAE5A221}-h','{3983B298-ADB1-4905-94E5-03B7AAE5A221}-m','{3983B298-ADB1-4905-94E5-03B7AAE5A221}-t']);
  assertEquals(formationCars(raicho,0),[]);assertEquals(formationCars(raicho,43),[]);
  for(const s of stocks)for(const length of s.lengths)assertEquals(formationCars(s,length).length,length);
});
Deno.test('fleet quote identity covers world, clock, revision, content and registered pack bytes',()=>{
  const read=fleet as any,stock=stocks[0],q=tradeQuery(read,stock,3,2);
  assertEquals(stockAvailable(stock,read),true);assertEquals(currentQuote(read,q),true);
  for(const k of ['worldEpoch','revision','tick','contentVersion'])assertEquals(currentQuote({...read,[k]:k==='contentVersion'?'other':String(BigInt(read[k])+1n)},q),false);
  assertEquals(stockAvailable(stock,{...read,requiredPacks:[]}),false);
  assertEquals(q.definition,stock.binding);
});
Deno.test('untrusted trade quotes retain exact Int64 prices and reject malformed totals',()=>{
  const query=tradeQuery(fleet as any,stocks[0],3,2);
  const p={query,price:'9007199254740993',normalCapacity:'792',seatedCapacity:'396',packedCapacity:'1188'};
  assertEquals(parseFleetTradePreview(p).price,'9007199254740993');
  for(const bad of [{...p,price:1},{...p,price:'-1'},{...p,packedCapacity:'1189'},{...p,seatedCapacity:'793'},{...p,extra:0}])assertThrows(()=>parseFleetTradePreview(bad));
});
