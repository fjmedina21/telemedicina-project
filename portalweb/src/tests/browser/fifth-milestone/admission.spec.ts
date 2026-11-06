import {test,expect,type Page} from "@playwright/test";
test.skip(process.env.MILESTONE_TEST_SYNTHETIC!=="1","Explicit synthetic H05 checks required");
const id="50000000-0000-4000-8000-000000000001";
const start=new Date(Date.now()+3*86400000).toISOString();
const appointment=(version=1)=>({id,version,startAt:start,endAt:new Date(new Date(start).getTime()+1800000).toISOString(),patientEmail:"synthetic-h05@example.invalid",serviceName:"Servicio sintético H05",practitionerName:"Profesional sintético",insurer:"Seguro sintético",memberLast4:"0000",status:"Confirmed",coverageStatus:"Expired",coverageMethod:"Manual",coverageRequiresReview:true,coverageValidityReason:"Expired",evidence:null,validUntil:null});
async function mock(page:Page){
  await page.route("**/api/backend/**",async route=>{
    const path=new URL(route.request().url()).pathname;
    const data=path.endsWith("/auth/me")?{userId:"synthetic-admission",email:"synthetic-admission@example.invalid",role:"Admission",permissions:["admission.manage"],accountVersion:1}:
      path.includes("/admin/work-queue")?[appointment()]:path.endsWith("/history")?[{sequence:1,newStatus:"Confirmed",newCoverage:"Approved",version:1,occurredAt:new Date().toISOString()}]:
      path.endsWith("/appointments/"+id)?appointment():{};
    await route.fulfill({status:200,json:{success:true,status:200,data}});
  });
  await page.goto("/admision");
  await expect(page.getByRole("table",{name:"Cola de admisión"})).toBeVisible();
}
test.beforeEach(async({baseURL})=>{expect(baseURL&&["localhost","127.0.0.1","[::1]"].includes(new URL(baseURL).hostname)).toBe(true);});
test("queue filters go to server and discarded responses cannot restore old results",async({page})=>{
  await mock(page);
  let release!:()=>void,ready!:()=>void;
  const held=new Promise<void>(resolve=>{release=resolve;}),started=new Promise<void>(resolve=>{ready=resolve;});
  await page.route("**/api/backend/admin/work-queue?**",async route=>{
    const url=new URL(route.request().url());
    if(url.searchParams.get("status")==="PendingValidation"){ready();await held;await route.fulfill({json:{data:[{...appointment(),serviceName:"Respuesta abandonada"}]}});}
    else await route.fulfill({json:{data:[{...appointment(),serviceName:"Resultado actual"}]}});
  });
  await page.getByLabel("Estado de cita",{exact:true}).selectOption("PendingValidation");
  await page.getByRole("button",{name:"Consultar citas",exact:true}).click();await started;
  await page.getByLabel("Estado de cita",{exact:true}).selectOption("Confirmed");
  await page.getByLabel("Estado de revisión manual",{exact:true}).selectOption("Expired");
  await page.getByLabel("Desde (hora de Santo Domingo)",{exact:true}).fill("2040-01-01T00:00");
  await page.getByLabel("Hasta (hora de Santo Domingo)",{exact:true}).fill("2040-01-03T00:00");
  const response=page.waitForRequest(request=>request.url().includes("/admin/work-queue?")&&request.url().includes("coverageStatus=Expired"));
  await page.getByRole("button",{name:"Consultar citas",exact:true}).click();
  const url=new URL((await response).url());
  expect(url.searchParams.get("from")).toBe("2040-01-01T04:00:00.000Z");
  expect(url.searchParams.get("to")).toBe("2040-01-03T04:00:00.000Z");
  await expect(page.getByRole("table",{name:"Cola de admisión"})).toContainText("Resultado actual");
  release();
  await expect(page.getByRole("table",{name:"Cola de admisión"})).not.toContainText("Respuesta abandonada");
});
for(const failure of [400,409,"network"] as const)test("review preserves draft on "+failure+" and only confirmed reload replaces base version",async({page})=>{
  await mock(page);
  let version=1,sent=0;const versions:number[]=[];
  await page.route("**/api/backend/appointments/"+id,route=>route.fulfill({json:{data:appointment(version)}}));
  await page.route("**/api/backend/admin/appointments/"+id+"/validation",async route=>{
    sent++;versions.push(route.request().postDataJSON().version);
    if(sent===1){
      if(failure==="network")await route.abort("failed");
      else await route.fulfill({status:failure,json:{status:failure,code:failure===409?"INVALID_TRANSITION":"EVIDENCE_REQUIRED",detail:"Rechazo sintético"}});
    }else await route.fulfill({json:{data:{status:"Confirmed",coverageStatus:"Approved",version:3}}});
  });
  await page.getByRole("button",{name:"Ver detalle y revisar",exact:true}).click();
  await expect(page.getByRole("region",{name:"Detalle de cita"})).toContainText("Revisión manual vencida");
  const evidence=page.getByLabel("Referencia de evidencia (sin texto clínico)",{exact:true});
  await evidence.fill("Referencia sintética conservada");
  const until=page.getByLabel("Vigencia hasta (hora de Santo Domingo)",{exact:true});
  await until.fill("2040-01-03T00:00");
  const save=page.getByRole("button",{name:"Registrar revisión favorable y confirmar cita",exact:true});
  await save.click();await expect(page.getByRole("alert")).toBeVisible();
  await expect(evidence).toHaveValue("Referencia sintética conservada");await expect(until).toHaveValue("2040-01-03T00:00");
  expect(versions).toEqual([1]);
  if(failure!==400)await expect(save).toBeDisabled();
  version=2;
  page.once("dialog",dialog=>dialog.dismiss());
  await page.getByRole("button",{name:"Recargar cita y descartar borrador",exact:true}).click();
  await expect(evidence).toHaveValue("Referencia sintética conservada");expect(sent).toBe(1);
  page.once("dialog",dialog=>dialog.accept());
  await page.getByRole("button",{name:"Recargar cita y descartar borrador",exact:true}).click();
  await expect(evidence).toHaveValue("");expect(sent).toBe(1);
  await evidence.fill("Nueva referencia revisada");await until.fill("2040-01-03T00:00");await save.click();
  await expect(page.getByRole("region",{name:"Detalle de cita"})).toHaveCount(0);
  expect(versions).toEqual([1,2]);
});
test("logout discards a pending detail response and private draft",async({page})=>{
  await mock(page);
  let release!:()=>void,ready!:()=>void;
  const held=new Promise<void>(resolve=>{release=resolve;}),started=new Promise<void>(resolve=>{ready=resolve;});
  await page.route("**/api/backend/appointments/"+id,async route=>{ready();await held;await route.fulfill({json:{data:appointment()}});});
  await page.getByRole("button",{name:"Ver detalle y revisar",exact:true}).click();await started;
  await page.getByRole("button",{name:"Salir",exact:true}).click();
  await expect(page).toHaveURL(/\/login$/);
  release();
  await expect(page.getByRole("region",{name:"Detalle de cita"})).toHaveCount(0);
  await expect(page.getByText("Referencia sintética conservada",{exact:true})).toHaveCount(0);
});
