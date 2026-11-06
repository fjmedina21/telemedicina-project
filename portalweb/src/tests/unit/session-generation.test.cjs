const {test}=require("node:test");
const assert=require("node:assert/strict");
const fs=require("node:fs");
const path=require("node:path");
const vm=require("node:vm");
const ts=require("typescript");
const client=fs.readFileSync(path.resolve(__dirname,"../../src/shared/api/client.ts"),"utf8");
const code=ts.transpileModule(client,{
  compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}
}).outputText;
for(const scenario of [200,401,"invalid-json"]){
  test("delayed "+scenario+" body cannot affect a new session",async()=>{
    let release,ready;
    const started=new Promise(resolve=>{ready=resolve;});
    let invalidations=0;
    const context={
      exports:{},AbortSignal,Event,
      window:{dispatchEvent(){invalidations++;}},
      fetch:async(url)=>url.endsWith("/auth/login")?{
        status:200,ok:true,json:async()=>({data:{user:"new"}})
      }:{
        status:scenario===200?200:401,ok:scenario===200,
        json:()=>{
          ready();
          return new Promise((resolve,reject)=>{
            release=()=>scenario==="invalid-json"
              ?reject(new Error("Invalid JSON"))
              :resolve({data:{user:"old"}});
          });
        }
      }
    };
    vm.runInNewContext(code,context);
    const previous=context.exports.api("/auth/refresh","POST",{},false)
      .then(value=>({value}),error=>({error}));
    await started;
    const current=await context.exports.api("/auth/login","POST",{});
    assert.equal(current.user,"new");
    release();
    assert.equal((await previous).error.constructor.name,"StaleResponse");
    assert.equal(invalidations,0);
  });
}
