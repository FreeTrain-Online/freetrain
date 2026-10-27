import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { defineConfig } from '@playwright/test';
const fixtureRoot=mkdtempSync(join(tmpdir(),'freetrain-optional-browser-'));
execFileSync('python3',['../scripts/tests/test_optional_preparation.py','--write-fixtures',fixtureRoot]);
let selected=[join(fixtureRoot,'fixture-two'),join(fixtureRoot,'fixture-one')];
if(process.env.FREETRAIN_ONLINE_OPTIONAL_PREPARED==='1') {
  selected=JSON.parse(execFileSync('python3',['../scripts/prepare-optional-content.py','--plugin-dir',selected[0],'--plugin-dir',selected[1],'--output-dir',join(fixtureRoot,'distribution')],{encoding:'utf8'}));
  rmSync(join(fixtureRoot,'fixture-one'),{recursive:true});rmSync(join(fixtureRoot,'fixture-two'),{recursive:true});
}
const port=Number(process.env.FREETRAIN_ONLINE_OPTIONAL_TEST_PORT??'5092');
const serverURL=`http://127.0.0.1:${port}`, webURL=`http://127.0.0.1:${port+1}`;
export default defineConfig({
  testDir:'./tests/browser',testMatch:'optional-content.spec.ts',workers:1,use:{baseURL:webURL,viewport:{width:600,height:800}},
  webServer:[
    {command:`dotnet run --project ../src/FreeTrain.Online.Server -c Release --no-build --no-restore --no-launch-profile -- --urls ${serverURL} --plugin-dir "${selected[0]}" --plugin-dir "${selected[1]}"`,
     env:{FreeTrainOnline__Saves__Directory:join(fixtureRoot,'saves')},url:`${serverURL}/health`,reuseExistingServer:false},
    {command:`deno task preview --port ${port+1} --strictPort`,env:{FREETRAIN_ONLINE_API_TARGET:serverURL},url:webURL,reuseExistingServer:false},
  ],
});
