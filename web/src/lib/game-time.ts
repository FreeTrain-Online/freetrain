import { interpolate, locales, type Locale } from './localization.ts';
// Presentation of source absolute minutes; never use the host calendar/timezone.
export const maximumGameMinute = 1128717404863199n;
const months = [31,28,31,30,31,30,31,31,30,31,30,31];
export function gameDate(absoluteMinute:string) {
  const minute = BigInt(absoluteMinute);
  if(minute<0n||minute>maximumGameMinute)throw new RangeError('Game time out of range');
  let day=Number(minute/1440n%365n),month=0;
  while(day>=months[month]){day-=months[month];month++;}
  return {year:Number(minute/525600n)+1,month:month+1,day:day+1,weekday:Number(minute/1440n%7n),hour:Number(minute/60n%24n),minute:Number(minute%60n)};
}
export function formatGameTime(absoluteMinute:string,locale:Locale,exactMinute=false):string {
  const date=gameDate(absoluteMinute),pad=(n:number)=>String(n).padStart(2,'0');
  const format=locales[locale].formats;
  // Source displayString deliberately truncates to ten-minute precision.
  const time=`${pad(date.hour)}:${exactMinute?pad(date.minute):`${Math.floor(date.minute/10)}0`}`;
  return interpolate(format.gameTime,{...date,weekday:format.weekdays[date.weekday],time});
}
