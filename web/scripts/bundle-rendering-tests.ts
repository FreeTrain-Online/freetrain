import {build} from 'vite';
// Use the installed/pinned bundler. No test harness is shipped in web/dist.
const result=await build({configFile:false,logLevel:'error',build:{write:false,minify:false,
 lib:{entry:'tests/helpers/train-rendering-harness.ts',formats:['es']}}});
const outputs=Array.isArray(result)?result:[result];
for(const output of outputs)if('output' in output)for(const chunk of output.output)if(chunk.type==='chunk'&&chunk.isEntry)console.log(chunk.code);
