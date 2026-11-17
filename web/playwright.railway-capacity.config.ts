import { browserArtifacts, failureArtifacts } from './tests/browser/artifact-config.ts';
import {defineConfig} from '@playwright/test';
export default defineConfig({...browserArtifacts(),testDir:'./tests/browser',testMatch:'railway-capacity.spec.ts',workers:1,timeout:600000,
  use:{...failureArtifacts,baseURL:'http://127.0.0.1:4493',viewport:{width:1880,height:1040}},expect:{timeout:60000}});
