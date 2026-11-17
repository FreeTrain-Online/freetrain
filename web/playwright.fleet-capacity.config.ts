import { browserArtifacts, failureArtifacts } from './tests/browser/artifact-config.ts';
import {defineConfig} from '@playwright/test';
export default defineConfig({...browserArtifacts(),testDir:'./tests/browser',testMatch:'fleet-capacity.spec.ts',workers:1,timeout:600000,
  use:{...failureArtifacts,viewport:{width:1880,height:1040}},projects:[{name:'canonical'}],expect:{timeout:60000}});
