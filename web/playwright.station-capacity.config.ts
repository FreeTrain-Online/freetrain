import {defineConfig} from '@playwright/test';
export default defineConfig({testDir:'./tests/browser',testMatch:'station-capacity.spec.ts',workers:1,timeout:600000,
  use:{viewport:{width:1880,height:1040}},projects:[{name:'canonical'}],expect:{timeout:60000}});
