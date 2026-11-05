// Browser WebSockets allow application close codes 3000–4999 and at most
// 123 UTF-8 bytes of reason. Keep diagnostics safe for both API and log output.
export function sessionCloseReason(reason:string):string {
  const encoder=new TextEncoder();let result='';
  for(const char of reason.replace(/[\r\n\t]/g,' ')) {
    if(encoder.encode(result+char).length>123)break;
    result+=char;
  }
  return result;
}
