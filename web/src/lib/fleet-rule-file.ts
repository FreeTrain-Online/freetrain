import type {FleetRule} from '../generated/contracts.ts';
import {maximumLocationRules,validFleetRule,stationActions} from './fleet-controllers.ts';
import {readLegacyRuleFile,maximumRuleFileBytes} from './fleet-rule-nrbf.ts';

export {maximumRuleFileBytes} from './fleet-rule-nrbf.ts';
export const ruleFileFormat='freetrain-online-timetable';
const fields=['month','day','dayOfWeek','hour','minutes','action'] as const;
export function validateFileRules(value:unknown):FleetRule[]{
 if(!Array.isArray(value)||value.length>maximumLocationRules)throw new Error('rules');
 return value.map(r=>{
  if(!r||typeof r!=='object'||Object.keys(r).length!==fields.length||!fields.every(k=>Object.hasOwn(r,k))||!validFleetRule(r,false))throw new Error('rule');
  return {...r};
 });
}
export function readRuleFile(bytes:Uint8Array):{format:'legacy'|'portable';rules:FleetRule[]}{
 if(!bytes.length||bytes.length>maximumRuleFileBytes)throw new Error('size');
 if(bytes[0]===0)return {format:'legacy',rules:validateFileRules(readLegacyRuleFile(bytes))};
 const value=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(bytes));
 if(!value||Object.keys(value).sort().join(',')!=='format,rules,version'||value.format!==ruleFileFormat||value.version!==1)throw new Error('format');
 return {format:'portable',rules:validateFileRules(value.rules)};
}
export function writeRuleFile(rules:readonly FleetRule[]):string{
 return JSON.stringify({format:ruleFileFormat,version:1,rules:validateFileRules(rules)},null,2)+'\n';
}
// Six unsigned bytes per rule: signed mask bytes (255 = wildcard), then the
// source action ordinal. Base64 keeps a full 1024-rule append below 16 KiB.
export function encodeAppendRules(rules:readonly FleetRule[]):string{
 return btoa(validateFileRules(rules).map(r=>String.fromCharCode(...fields.slice(0,5).map(f=>Number(r[f])&255),stationActions.indexOf(r.action as typeof stationActions[number]))).join(''));
}
