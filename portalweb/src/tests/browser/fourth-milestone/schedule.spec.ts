import {test,expect,type APIRequestContext,type BrowserContext} from "@playwright/test";
import {randomUUID} from "node:crypto";

test.describe.configure({mode:"serial"});
test.skip(process.env.MILESTONE_TEST_SYNTHETIC!=="1","Requires explicit synthetic H04 execution");
const headers={"X-Telemedicina-Client":"milestone","X-Session-Mode":"cookie"};
let admin:APIRequestContext;
let cookies:Awaited<ReturnType<BrowserContext["cookies"]>>=[];
let serviceA="",serviceB="",personA="",personB="";
let serviceNameA="",serviceNameB="",personNameA="",personNameB="";
let slotIds:string[]=[];
const begins=new Date(Date.now()+2*24*3600000);
begins.setUTCHours(13,0,0,0);
const from=new Date(begins.getTime()-24*3600000).toISOString();
const to=new Date(begins.getTime()+2*24*3600000).toISOString();
function localInput(date:Date){return new Date(date.getTime()-4*3600000).toISOString().slice(0,16);}

test.beforeAll(async({playwright,baseURL})=>{
  expect(baseURL&&["localhost","127.0.0.1","[::1]"].includes(new URL(baseURL).hostname)).toBe(true);
  for(const key of ["Milestone__AdmissionEmail","Milestone__AdmissionPassword"])
    if(!process.env[key])throw new Error("Missing private environment variable: "+key);
  admin=await playwright.request.newContext({baseURL,extraHTTPHeaders:headers});
  expect((await admin.post("/api/backend/auth/login",{data:{
    email:process.env.Milestone__AdmissionEmail,password:process.env.Milestone__AdmissionPassword}})).status()).toBe(200);
  // Cookie metadata is used only in memory, never persisted as storageState or traces.
  cookies=(await admin.storageState()).cookies;
  const suffix=randomUUID();
  serviceNameA="Servicio H04 A "+suffix;serviceNameB="Servicio H04 B "+suffix;
  personNameA="Profesional H04 A "+suffix;personNameB="Profesional H04 B "+suffix;
  async function service(name:string){
    const r=await admin.post("/api/backend/admin/catalog/services",{data:{name,specialty:"Pruebas H04",
      durationMinutes:30,active:true,isSynthetic:true,approvalReference:"",version:0}});
    expect(r.status()).toBe(201);return (await r.json()).data.id as string;
  }
  serviceA=await service(serviceNameA);serviceB=await service(serviceNameB);
  async function person(name:string,serviceId:string){
    const r=await admin.post("/api/backend/admin/catalog/practitioners",{data:{name,licenseRef:"SYNTHETIC",
      active:true,serviceIds:[serviceId],version:0}});
    expect(r.status()).toBe(201);return (await r.json()).data.id as string;
  }
  personA=await person(personNameA,serviceA);personB=await person(personNameB,serviceB);
  for(let index=0;index<6;index++){
    const r=await admin.post("/api/backend/admin/slots",{data:{serviceId:serviceA,practitionerId:personA,
      startAt:new Date(begins.getTime()+index*3600000).toISOString()}});
    expect(r.status()).toBe(201);slotIds.push((await r.json()).data.id);
  }
});
test.afterAll(async()=>{
  if(!admin)return;
  for(const [id,name] of [[personA,personNameA],[personB,personNameB]])if(id)
    await admin.patch("/api/backend/admin/catalog/practitioners/"+id,{data:{name,licenseRef:"SYNTHETIC",active:false,serviceIds:[],version:1}});
  for(const [id,name] of [[serviceA,serviceNameA],[serviceB,serviceNameB]])if(id)
    await admin.patch("/api/backend/admin/catalog/services/"+id,{data:{name,specialty:"Pruebas H04",durationMinutes:30,
      active:false,isSynthetic:true,approvalReference:"",version:1}});
  await admin.post("/api/backend/auth/logout",{data:{}});await admin.dispose();
});
test.beforeEach(async({page,context})=>{
  await context.addCookies(cookies);await page.goto("/agenda");
  await expect(page.getByRole("heading",{name:"Horarios",exact:true})).toBeVisible();
  await page.getByLabel("Servicio").selectOption(serviceA);
  await page.getByLabel("Profesional").selectOption(personA);
  await page.getByLabel("Desde (hora de Santo Domingo)",{exact:true}).fill(localInput(new Date(from)));
  await page.getByLabel("Hasta (hora de Santo Domingo)",{exact:true}).fill(localInput(new Date(to)));
});

test("associated selection, real pagination and versioned block/unblock",async({page})=>{
  const people=page.getByLabel("Profesional");
  await expect(people.getByRole("option",{name:personNameA,exact:true})).toHaveCount(1);
  await expect(people.getByRole("option",{name:personNameB,exact:true})).toHaveCount(0);
  await page.getByLabel("Horarios por página").selectOption("5");
  await page.getByRole("button",{name:"Consultar horarios",exact:true}).click();
  await expect(page.getByRole("table",{name:"Agenda administrativa"}).locator("tbody tr")).toHaveCount(5);
  await page.getByRole("button",{name:"Página siguiente de horarios",exact:true}).click();
  await expect(page.getByRole("table",{name:"Agenda administrativa"}).locator("tbody tr")).toHaveCount(1);
  const row=page.getByRole("table",{name:"Agenda administrativa"}).locator("tbody tr").first();
  const response=page.waitForResponse(r=>r.url().includes("/block")&&r.request().method()==="PATCH");
  await row.getByRole("button",{name:"Bloquear",exact:true}).click();
  const blocked=await response;expect(blocked.status()).toBe(200);
  expect(Number(blocked.request().postDataJSON().version)).toBeGreaterThan(0);
  await page.getByLabel("Bloqueo").selectOption("true");
  await page.getByRole("button",{name:"Consultar horarios",exact:true}).click();
  await expect(row).toContainText("Bloqueado");
  const unblocked=page.waitForResponse(r=>r.url().includes("/block")&&r.request().method()==="PATCH");
  await row.getByRole("button",{name:"Desbloquear",exact:true}).click();
  expect((await unblocked).status()).toBe(200);
  await expect(page.getByText("No hay horarios que coincidan con estos filtros.",{exact:true})).toBeVisible();
});

test("stale version preserves creation draft until explicit table reload",async({page})=>{
  await page.getByRole("button",{name:"Consultar horarios",exact:true}).click();
  const row=page.locator('[data-slot-id="'+slotIds[0]+'"]');
  await expect(row.getByRole("button",{name:"Bloquear",exact:true})).toBeEnabled();
  const query=new URLSearchParams({from,to,serviceId:serviceA,practitionerId:personA});
  const result=await admin.get("/api/backend/admin/slots?"+query);expect(result.status()).toBe(200);
  const slot=(await result.json()).data.items.find((s:{id:string})=>s.id===slotIds[0]);
  expect((await admin.patch("/api/backend/admin/slots/"+slot.id+"/block",{data:{blocked:true,version:slot.version}})).status()).toBe(200);
  const draft=localInput(new Date(begins.getTime()+9*3600000));
  await page.getByLabel("Inicio (hora de Santo Domingo)",{exact:true}).fill(draft);
  const rejected=page.waitForResponse(r=>r.url().includes("/block")&&r.request().method()==="PATCH");
  await row.getByRole("button",{name:"Bloquear",exact:true}).click();
  expect((await rejected).status()).toBe(409);
  await expect(page.getByLabel("Inicio (hora de Santo Domingo)",{exact:true})).toHaveValue(draft);
  await expect(page.getByText(/Tu formulario se conserva/)).toBeVisible();
  await page.getByRole("button",{name:"Recargar horarios",exact:true}).click();
  await expect(row.getByRole("button",{name:"Desbloquear",exact:true})).toBeEnabled();
  await expect(page.getByLabel("Inicio (hora de Santo Domingo)",{exact:true})).toHaveValue(draft);
});

test("a delayed real response cannot reinstall results for abandoned filters",async({page})=>{
  let release!:()=>void;const held=new Promise<void>(resolve=>{release=resolve;});
  let fetched!:()=>void;const ready=new Promise<void>(resolve=>{fetched=resolve;});
  let first=true;
  await page.route("**/api/backend/admin/slots?**",async route=>{
    if(!first){await route.continue();return;}first=false;
    const response=await route.fetch();fetched();await held;await route.fulfill({response});
  });
  try{
    await page.getByRole("button",{name:"Consultar horarios",exact:true}).click();await ready;
    await page.getByLabel("Servicio").selectOption(serviceB);
    release();
    await expect(page.getByRole("button",{name:"Consultar horarios",exact:true})).toBeEnabled();
    await expect(page.locator('[data-slot-id]')).toHaveCount(0);
    await expect(page.getByText("Todavía no se han consultado estos filtros.",{exact:true})).toBeVisible();
    await expect(page.getByLabel("Profesional").getByRole("option",{name:personNameA,exact:true})).toHaveCount(0);
    await page.getByRole("button",{name:"Consultar horarios",exact:true}).click();
    await expect(page.getByText("No hay horarios que coincidan con estos filtros.",{exact:true})).toBeVisible();
  }finally{release();await page.unroute("**/api/backend/admin/slots?**");}
});

test("uncertain creation requires a successful targeted reconciliation",async({page})=>{
  const draft=localInput(new Date(begins.getTime()+10*3600000));
  await page.getByLabel("Inicio (hora de Santo Domingo)",{exact:true}).fill(draft);
  let sent=0;
  await page.route("**/api/backend/admin/slots",async route=>{
    if(route.request().method()!=="POST"){await route.continue();return;}
    sent++;
    const response=await route.fetch();
    expect(response.status()).toBe(201);
    await route.abort("failed");
  });
  await page.getByRole("button",{name:"Crear horario",exact:true}).click();
  const verify=page.getByRole("button",{name:"Comprobar creación pendiente",exact:true});
  await expect(verify).toBeVisible();
  await expect(page.getByRole("button",{name:"Crear horario",exact:true})).toBeDisabled();
  await expect(page.getByLabel("Inicio (hora de Santo Domingo)",{exact:true})).toHaveValue(draft);
  expect(sent).toBe(1);
  let failOnce=true;
  await page.route("**/api/backend/admin/slots?**",async route=>{
    if(failOnce){failOnce=false;await route.abort("failed");}
    else await route.continue();
  });
  await verify.click();
  await expect(verify).toBeEnabled();
  await expect(page.getByRole("button",{name:"Crear horario",exact:true})).toBeDisabled();
  await expect(page.getByLabel("Inicio (hora de Santo Domingo)",{exact:true})).toHaveValue(draft);
  await verify.click();
  await expect(page.getByText("La consulta confirma que el horario ya existe. No repitas la creación.",{exact:true})).toBeVisible();
  await expect(verify).toHaveCount(0);
  await expect(page.getByLabel("Inicio (hora de Santo Domingo)",{exact:true})).toHaveValue("");
  expect(sent).toBe(1);
});
