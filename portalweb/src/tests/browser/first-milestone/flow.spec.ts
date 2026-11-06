import {test,expect,APIRequestContext,BrowserContext} from "@playwright/test";
import {randomBytes,randomUUID} from "node:crypto";
import path from "node:path";
import fs from "node:fs/promises";
import type {Item} from "@/shared/api/client";

test.describe.configure({mode:"serial"});
const headers={"X-Telemedicina-Client":"milestone"};
const artifacts=path.resolve(process.cwd(),"../../hitos/portalweb/primer-hito-portalweb/evidencias/playwright/capturas");
let cookies:Awaited<ReturnType<BrowserContext["cookies"]>>=[];
let patient:APIRequestContext;
let appointmentId="";
let patientEmail="";

test.beforeAll(async()=>{
  expect(process.env.MILESTONE_TEST_SYNTHETIC,"Synthetic test opt-in").toBe("1");
  for(const name of ["Milestone__AdmissionEmail","Milestone__AdmissionPassword",
    "Milestone__MailboxKey"]){
    if(!process.env[name]) throw new Error("Missing private environment variable: "+name);
  }
  await fs.mkdir(artifacts,{recursive:true});
});
test.afterAll(async()=>{await patient?.dispose();});
test.beforeEach(async({context})=>{if(cookies.length) await context.addCookies(cookies);});

test("unauthenticated navigation redirects to login",async({page})=>{
  await page.goto("/admision");
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole("heading",{name:"Acceso al portal"})).toBeVisible();
});

test("password login creates HttpOnly cookies without exposing tokens",async({page,context})=>{
  await page.goto("/login");
  await page.getByLabel("Correo",{exact:true}).fill(process.env.Milestone__AdmissionEmail!);
  await page.getByLabel("Contraseña",{exact:true}).fill(process.env.Milestone__AdmissionPassword!);
  const loginResponse=page.waitForResponse(r=>r.url().endsWith("/auth/login")&&r.request().method()==="POST");
  await page.getByRole("button",{name:"Entrar",exact:true}).click();
  const response=await loginResponse;
  expect(response.status()).toBe(200);
  const data=(await response.json()).data;
  expect(Object.keys(data).includes("accessToken")).toBe(false);
  expect(Object.keys(data).includes("refreshToken")).toBe(false);
  await expect(page.getByRole("heading",{name:"Admisión y cobertura",exact:true})).toBeVisible();
  cookies=(await context.cookies()).filter(c=>c.name==="tm_access"||c.name==="tm_refresh");
  expect(cookies.length).toBe(2);
  expect(cookies.every(c=>c.httpOnly&&c.sameSite==="Strict")).toBe(true);
});

test("patient request is reviewed in the browser and persists in PostgreSQL",async({page,playwright,baseURL})=>{
  const setup=await playwright.request.newContext({baseURL,extraHTTPHeaders:headers});
  patientEmail="synthetic-browser-"+randomBytes(8).toString("hex")+"@example.invalid";
  const password=randomBytes(24).toString("hex");
  expect((await setup.post("/api/backend/auth/register",{data:{email:patientEmail,password}})).status()).toBe(202);
  const challenge=await setup.get("/api/backend/auth/dev-mailbox",{
    params:{email:patientEmail,purpose:"verify"},headers:{"X-Dev-Mailbox-Key":process.env.Milestone__MailboxKey!}});
  expect(challenge.status()).toBe(200);
  const token=(await challenge.json()).data.token;
  expect((await setup.post("/api/backend/auth/verify",{data:{email:patientEmail,token}})).status()).toBe(200);
  const login=await setup.post("/api/backend/auth/login",{data:{email:patientEmail,password}});
  expect(login.status()).toBe(200);
  const access=(await login.json()).data.accessToken;
  await setup.dispose();
  patient=await playwright.request.newContext({
    baseURL,extraHTTPHeaders:{...headers,Authorization:"Bearer "+access}
  });
  expect((await patient.patch("/api/backend/patients/me",{data:{
    firstName:"Paciente",lastName:"Navegador sintético",birthDate:"1990-01-01",
    phone:"8095550101",address:"Dirección de prueba",emergencyName:"Contacto sintético",
    emergencyPhone:"8095550102",allergies:[],medications:[],conditions:[],version:0
  }})).status()).toBe(200);
  const services=(await (await patient.get("/api/backend/services")).json()).data;
  const slots=(await (await patient.get("/api/backend/availability",{
    params:{serviceId:services[0].id}})).json()).data;
  expect(slots.length>0).toBe(true);
  const slot=slots[0];
  const created=await patient.post("/api/backend/appointments",{
    headers:{"Idempotency-Key":randomUUID()},
    data:{slotId:slot.id,insurer:"Aseguradora sintética de navegador",memberLast4:"0000"}
  });
  expect(created.status()).toBe(201);
  appointmentId=(await created.json()).data.id;
  await page.goto("/admision");
  const localInput=(date:Date)=>new Date(date.getTime()-4*3600000).toISOString().slice(0,16);
  await page.getByLabel("Desde (hora de Santo Domingo)",{exact:true}).fill(localInput(new Date(new Date(slot.startAt).getTime()-60000)));
  await page.getByLabel("Hasta (hora de Santo Domingo)",{exact:true}).fill(localInput(new Date(new Date(slot.startAt).getTime()+60000)));
  await page.getByRole("button",{name:"Consultar citas",exact:true}).click();
  const row=page.getByRole("row").filter({hasText:patientEmail});
  await expect(row).toContainText("Pendiente");
  await row.getByRole("button",{name:"Ver detalle y revisar",exact:true}).click();
  await page.getByLabel("Referencia de evidencia (sin texto clínico)").fill("Evidencia sintética de Playwright");
  const valid=new Date(new Date(slot.endAt).getTime()+2*3600000-4*3600000).toISOString().slice(0,16);
  await page.getByLabel("Vigencia hasta (hora de Santo Domingo)").fill(valid);
  const reviewed=page.waitForResponse(r=>r.url().includes("/validation")&&r.request().method()==="PATCH");
  await page.getByRole("button",{name:"Registrar revisión favorable y confirmar cita",exact:true}).click();
  expect((await reviewed).status()).toBe(200);
  await expect(row).toContainText("Confirmada");
  await expect(row).toContainText("Revisión manual favorable");
  const persisted=(await (await patient.get("/api/backend/appointments/"+appointmentId)).json()).data;
  expect(persisted.status).toBe("Confirmed");
  expect(persisted.coverageStatus).toBe("Approved");
  expect(persisted.reviewedBy).toBeTruthy();
  await page.screenshot({path:path.join(artifacts,"admision-confirmada.png"),fullPage:true});
});

test("audit and schedule navigation show server data",async({page,context})=>{
  await page.goto("/admin");
  await expect(page.getByRole("heading",{name:"Auditoría",exact:true})).toBeVisible();
  await expect(page.getByRole("button",{name:"Actualizar",exact:true})).toBeEnabled();
  for(let i=0;i<20;i++){
    if(await page.getByText(appointmentId,{exact:false}).count()) break;
    const next=page.waitForResponse(r=>r.url().includes("/admin/audit-events"));
    await page.getByRole("button",{name:"Siguiente página",exact:true}).click();
    expect((await next).status()).toBe(200);
  }
  await expect(page.getByText(appointmentId,{exact:false}).first()).toBeVisible();
  await page.screenshot({path:path.join(artifacts,"auditoria.png"),fullPage:false});
  await page.goto("/agenda");
  await expect(page.getByRole("heading",{name:"Horarios",exact:true})).toBeVisible();
  const available=page.waitForResponse(r=>r.url().includes("/admin/slots?"));
  await page.getByRole("button",{name:"Consultar horarios",exact:true}).click();
  expect((await available).status()).toBe(200);
  await expect(page.getByRole("button",{name:"Bloquear",exact:true}).first()).toBeVisible();
  const blocked=page.waitForResponse(r=>r.url().includes("/block")&&r.request().method()==="PATCH");
  await page.getByRole("button",{name:"Bloquear",exact:true}).first().click();
  const response=await blocked;
  expect(response.status()).toBe(200);
  const id=response.url().match(/\/slots\/([^/]+)\/block/)![1];
  // Restore this synthetic schedule after verifying the browser action.
  expect((await context.request.patch("/api/backend/admin/slots/"+id+"/block",{
    headers,data:{blocked:false}})).status()).toBe(200);
  await page.screenshot({path:path.join(artifacts,"horarios.png"),fullPage:false});
});

test("catalog editor persists synthetic services and professional assignments",async({page,context})=>{
  const suffix=randomBytes(5).toString("hex");
  const serviceName="Servicio sintético navegador "+suffix;
  const personName="Profesional sintético navegador "+suffix;
  await page.goto("/catalogo");
  await expect(page.getByRole("heading",{name:"Catálogo",exact:true})).toBeVisible();
  await page.getByLabel("Nombre del servicio",{exact:true}).fill(serviceName);
  await page.getByLabel("Especialidad",{exact:true}).fill("Pruebas");
  await page.getByLabel("Duración en minutos",{exact:true}).fill("20");
  await expect(page.getByLabel("Servicio sintético",{exact:true})).toBeChecked();
  await page.getByLabel("Servicio activo",{exact:true}).check();
  const created=page.waitForResponse(response=>response.url().endsWith("/catalog/services")&&response.request().method()==="POST");
  await page.getByRole("button",{name:"Guardar servicio",exact:true}).click();
  const serviceResponse=await created;expect(serviceResponse.status()).toBe(201);
  const serviceId=(await serviceResponse.json()).data.id;
  await page.getByLabel("Nombre del profesional",{exact:true}).fill(personName);
  await page.getByLabel("Referencia profesional",{exact:true}).fill("SYNTHETIC");
  await page.getByLabel("Profesional activo",{exact:true}).check();
  await page.getByRole("checkbox",{name:serviceName,exact:true}).check();
  const assigned=page.waitForResponse(response=>response.url().endsWith("/catalog/practitioners")&&response.request().method()==="POST");
  await page.getByRole("button",{name:"Guardar profesional",exact:true}).click();
  const personResponse=await assigned;expect(personResponse.status()).toBe(201);
  const personId=(await personResponse.json()).data.id;
  await expect(page.getByText(personName,{exact:false}).first()).toBeVisible();
  const publicServices=(await (await patient.get("/api/backend/services")).json()).data;
  expect(publicServices.some((item:Item)=>item.id===serviceId)).toBe(true);
  await page.screenshot({path:path.join(artifacts,"catalogo.png"),fullPage:true});
  expect((await context.request.patch("/api/backend/admin/catalog/practitioners/"+personId,{
    headers,data:{name:personName,licenseRef:"SYNTHETIC",active:false,serviceIds:[],version:1}})).status()).toBe(200);
  expect((await context.request.patch("/api/backend/admin/catalog/services/"+serviceId,{
    headers,data:{name:serviceName,specialty:"Pruebas",durationMinutes:20,active:false,isSynthetic:true,
      approvalReference:"",version:1}})).status()).toBe(200);
});

test("delivery monitoring shows metadata while patient notifications remain private",async({page})=>{
  await page.goto("/entregas");
  await expect(page.getByRole("heading",{name:"Entregas de correo",exact:true})).toBeVisible();
  await expect(page.getByRole("table")).toBeVisible();
  await expect(page.getByRole("columnheader",{name:"Último código",exact:true})).toBeVisible();
  await page.screenshot({path:path.join(artifacts,"entregas.png"),fullPage:false});
  const notices=(await (await patient.get("/api/backend/notifications/me")).json()).data;
  expect(notices.filter((item:Item)=>item.resourceId===appointmentId).length).toBe(2);
});

test("logout revokes the cookie session and protects navigation again",async({page,context})=>{
  await page.goto("/admision");
  await expect(page.getByRole("heading",{name:"Admisión y cobertura",exact:true})).toBeVisible();
  await expect(page.getByRole("button",{name:"Salir",exact:true})).toBeEnabled();
  await page.getByRole("button",{name:"Salir",exact:true}).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto("/admin");
  await expect(page).toHaveURL(/\/login$/);
  expect((await context.cookies()).filter(c=>c.name==="tm_access"||c.name==="tm_refresh").length).toBe(0);
  if(patient&&appointmentId){
    const detail=(await (await patient.get("/api/backend/appointments/"+appointmentId)).json()).data;
    expect((await patient.post("/api/backend/appointments/"+appointmentId+"/cancel",{
      data:{version:detail.version}})).status()).toBe(200);
    await patient.post("/api/backend/auth/logout",{data:{}});
  }
});
