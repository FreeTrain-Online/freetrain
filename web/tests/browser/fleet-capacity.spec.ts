import {resolve} from 'node:path';
import {registerCapacitySuite} from './capacity-harness.ts';
registerCapacitySuite(resolve(process.env.FREETRAIN_FLEET_CAPACITY_FIXTURES??'../tmp/p5-28-capacity'),
 resolve(process.env.FREETRAIN_ONLINE_PROFILE_EVIDENCE??'../tmp/p5-28-capacity/browser'),
 ['small','medium','large','network','empty'],'P5-28');
