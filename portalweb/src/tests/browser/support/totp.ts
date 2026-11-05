import {createHmac} from "node:crypto";

export function totp(secret:string) {
  const alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
  let buffer=0,bits=0;
  const key:number[]=[];
  for(const character of secret.replace(/=+$/,"").toUpperCase()){
    const index=alphabet.indexOf(character);
    if(index<0) throw new Error("Invalid synthetic TOTP configuration");
    buffer=(buffer<<5)|index;bits+=5;
    if(bits>=8){bits-=8;key.push((buffer>>bits)&255);}
  }
  const counter=Buffer.alloc(8);
  counter.writeBigUInt64BE(BigInt(Math.floor(Date.now()/30000)));
  const digest=createHmac("sha1",Buffer.from(key)).update(counter).digest();
  const offset=digest[digest.length-1]&15;
  return ((digest.readUInt32BE(offset)&0x7fffffff)%1000000).toString().padStart(6,"0");
}
