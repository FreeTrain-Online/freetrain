import {resolve} from 'node:path';
import {registerCapacitySuite} from './capacity-harness.ts';
registerCapacitySuite(resolve(process.env.FREETRAIN_STATION_CAPACITY_FIXTURES??'../tmp/p6-26-capacity'),
 resolve(process.env.FREETRAIN_ONLINE_PROFILE_EVIDENCE??'../tmp/p6-26-capacity/browser'),
 ['small','medium','large','extent'],'P6-26',true);
