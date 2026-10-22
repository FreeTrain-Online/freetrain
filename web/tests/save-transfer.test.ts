import { downloadSave, saveRequest, SaveError } from '../src/lib/save-transfer.ts';
function assert(value:unknown):asserts value {if(!value)throw new Error('Assertion failed');}
async function rejects(action:()=>Promise<unknown>,code:string) {
  try {await action();throw new Error('Unsupported save accepted');}
  catch(error){assert(error instanceof SaveError&&error.code===code);}
}
Deno.test('unsupported save and unavailable writer codes reach the presentation layer intact',async()=>{
  const original=globalThis.fetch;
  try {
    globalThis.fetch=()=>Promise.resolve(Response.json({code:'incompatibleSave'},{status:400}));
    await rejects(()=>saveRequest('upload','1',new File(['{"saveVersion":1}'],'old.json')),'incompatibleSave');
    globalThis.fetch=()=>Promise.resolve(Response.json({code:'savingUnavailable'},{status:409}));
    await rejects(()=>downloadSave(),'savingUnavailable');
    await rejects(()=>saveRequest('slot1/save','1'),'savingUnavailable');
  } finally {globalThis.fetch=original;}
});
Deno.test('oversized save input is rejected before a transfer begins',async()=>{
  const original=globalThis.fetch;let calls=0;
  try {
    globalThis.fetch=()=>{calls++;throw new Error('Unexpected transfer');};
    await rejects(()=>saveRequest('upload','1',new File([new Uint8Array(1024*1024+1)],'large.json')),'saveTooLarge');
    assert(calls===0);
  } finally {globalThis.fetch=original;}
});
