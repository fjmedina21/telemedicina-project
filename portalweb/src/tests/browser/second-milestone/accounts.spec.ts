import {test,expect,type Page} from "@playwright/test";
import {randomBytes} from "node:crypto";

test.describe.configure({mode:"serial"});
test.skip(process.env.MILESTONE_TEST_SYNTHETIC!=="1"||process.env.MILESTONE_TEST_PHASE!=="second",
  "Requires explicit synthetic second-milestone execution");
const headers={"X-Telemedicina-Client":"milestone","X-Session-Mode":"cookie"};
async function login(page:Page,email:string,password:string){
  await page.goto("/login");
  await page.getByLabel("Correo",{exact:true}).fill(email);
  await page.getByLabel("Contraseña",{exact:true}).fill(password);
  await page.getByRole("button",{name:"Entrar",exact:true}).click();
}
async function findRow(page:Page,email:string){
  for(let index=0;index<30;index++){
    await expect(page.getByRole("button",{name:"Actualizar",exact:true})).toBeEnabled();
    const row=page.getByRole("row").filter({hasText:email});
    if(await row.count())return row;
    const next=page.getByRole("button",{name:"Página siguiente",exact:true});
    if(await next.isDisabled())break;
    const loaded=page.waitForResponse(response=>response.url().includes("/admin/staff?"));
    await next.click();await loaded;
  }
  throw new Error("Synthetic account not found in paginated list.");
}
test.beforeEach(async({page,baseURL})=>{
  expect(baseURL&&["localhost","127.0.0.1","[::1]"].includes(new URL(baseURL).hostname)).toBe(true);
  for(const key of ["Milestone__AdministratorEmail","Milestone__AdministratorPassword","Milestone__MailboxKey"])
    if(!process.env[key])throw new Error("Missing private environment variable: "+key);
  page.on("dialog",dialog=>dialog.accept());
});

test("administrator invites; staff activates and receives no operative access",async({page,browser,baseURL})=>{
  await login(page,process.env.Milestone__AdministratorEmail!,process.env.Milestone__AdministratorPassword!);
  await expect(page.getByRole("heading",{name:"Cuentas de personal",exact:true})).toBeVisible();
  await expect(page.getByRole("link",{name:"Admisión",exact:true})).toHaveCount(0);
  const email="synthetic-browser-h02-"+randomBytes(8).toString("hex")+"@example.invalid";
  await page.getByLabel("Correo de personal",{exact:true}).fill(email);
  await page.getByRole("combobox",{name:"Rol de personal",exact:true}).selectOption("Support");
  const invited=page.waitForResponse(response=>response.url().endsWith("/staff/invitations")&&response.request().method()==="POST");
  await page.getByRole("button",{name:"Invitar cuenta",exact:true}).click();
  expect((await invited).status()).toBe(201);
  await expect(await findRow(page,email)).toContainText("Pendiente de activación");
  const mailbox=await page.request.get("/api/backend/auth/dev-mailbox",{params:{email,purpose:"staff-activation"},
    headers:{"X-Dev-Mailbox-Key":process.env.Milestone__MailboxKey!}});
  expect(mailbox.status()).toBe(200);
  const token=(await mailbox.json()).data.token as string;
  const password=randomBytes(24).toString("hex");
  const context=await browser.newContext({baseURL});
  try{
    const staff=await context.newPage();
    await staff.goto("/activar-personal");
    await staff.getByLabel("Invitación",{exact:true}).fill(token);
    await staff.getByLabel("Nueva contraseña",{exact:true}).fill(password);
    await staff.getByRole("button",{name:"Activar cuenta",exact:true}).click();
    await expect(staff.getByRole("status")).toContainText("Cuenta activada");
    expect(new URL(staff.url()).search).toBe("");
    await expect(staff.getByLabel("Invitación",{exact:true})).toHaveCount(0);
    await login(staff,email,password);
    await expect(staff.getByText("Sin funciones operativas habilitadas.",{exact:true})).toBeVisible();
    expect((await context.request.get("/api/backend/patients/me",{headers})).status()).toBe(403);
    expect((await context.request.get("/api/backend/admin/staff",{headers})).status()).toBe(403);
    await staff.goto("/personal");
    await expect(staff.getByRole("status")).toContainText("No tiene permiso");
    expect(new URL(staff.url()).pathname).toBe("/personal"); // 403 must not become a login loop
  }finally{await context.close();}
});

test("administrator lifecycle, stale version conflict, and revoked-session redirect",async({page})=>{
  await login(page,process.env.Milestone__AdministratorEmail!,process.env.Milestone__AdministratorPassword!);
  await expect(page.getByRole("heading",{name:"Cuentas de personal",exact:true})).toBeVisible();
  const email="synthetic-browser-h02-"+randomBytes(8).toString("hex")+"@example.invalid";
  const created=await page.request.post("/api/backend/admin/staff/invitations",{headers,data:{email,role:"Support"}});
  expect(created.status()).toBe(201);
  const account=(await created.json()).data;
  const mailbox=await page.request.get("/api/backend/auth/dev-mailbox",{params:{email,purpose:"staff-activation"},
    headers:{"X-Dev-Mailbox-Key":process.env.Milestone__MailboxKey!}});
  expect(mailbox.status()).toBe(200);
  const activated=await page.request.post("/api/backend/auth/staff/activate",{headers,
    data:{token:(await mailbox.json()).data.token,password:randomBytes(24).toString("hex")}});
  expect(activated.status()).toBe(200);
  await page.reload();
  let row=await findRow(page,email);
  await row.getByRole("button",{name:"Suspender",exact:true}).click();
  await expect(row).toContainText("Suspendida");
  await row.getByRole("button",{name:"Reactivar",exact:true}).click();
  await expect(row).toContainText("Activa");
  // Change via API while the rendered row still holds the previous version.
  expect((await page.request.patch("/api/backend/admin/staff/"+account.id+"/role",{headers,
    data:{role:"Practitioner",expectedVersion:account.version+3}})).status()).toBe(200);
  await row.getByRole("button",{name:"Revocar sesiones",exact:true}).click();
  await expect(page.getByRole("alert").filter({hasText:"Recargue"})).toContainText("Recargue");
  await page.getByRole("button",{name:"Recargar cuentas",exact:true}).click();
  row=await findRow(page,email);
  await expect(row).toContainText("Profesional");
  await row.getByRole("button",{name:"Revocar sesiones",exact:true}).click();
  await expect(page.getByRole("status")).toContainText("Operación confirmada");
  expect((await page.request.post("/api/backend/auth/logout",{headers,data:{}})).status()).toBe(200);
  await page.getByRole("button",{name:"Actualizar",exact:true}).click();
  await expect(page).toHaveURL(/\/login$/);
});

test("expired access preserves valid refresh; revoked cookie replay clears the session",async({page,context})=>{
  await login(page,process.env.Milestone__AdministratorEmail!,process.env.Milestone__AdministratorPassword!);
  await expect(page.getByRole("heading",{name:"Cuentas de personal",exact:true})).toBeVisible();
  const update=page.getByRole("button",{name:"Actualizar",exact:true});
  await expect(update).toBeEnabled();
  const access=(await context.cookies()).find(cookie=>cookie.name==="tm_access");
  expect(Boolean(access)).toBe(true);
  // Real browser cookie expiry: only access expires; refresh remains HttpOnly and valid.
  await context.addCookies([{...access!,expires:Math.floor(Date.now()/1000)-60}]);
  expect((await context.cookies()).some(cookie=>cookie.name==="tm_refresh")).toBe(true);
  const refreshed=page.waitForResponse(response=>response.url().endsWith("/auth/refresh")&&response.request().method()==="POST");
  await update.click();
  expect((await refreshed).status()).toBe(200);
  await expect(update).toBeEnabled();
  await expect(page.getByRole("heading",{name:"Cuentas de personal",exact:true})).toBeVisible();
  expect(new URL(page.url()).pathname).toBe("/personal");
  // Replay the in-memory cookie pair after the server has actually revoked its family.
  // Never save these cookies as storageState, logs, screenshots or traces.
  const revokedCookies=(await context.cookies()).filter(cookie=>["tm_access","tm_refresh"].includes(cookie.name));
  expect((await page.request.post("/api/backend/auth/logout",{headers,data:{}})).status()).toBe(200);
  await context.addCookies(revokedCookies);
  const rejectedRefresh=page.waitForResponse(response=>response.url().endsWith("/auth/refresh")&&response.request().method()==="POST");
  await update.click();
  const rejected=await rejectedRefresh;
  if(rejected.status()===429){
    const retryAfter=Number(rejected.headers()["retry-after"]);
    await page.waitForTimeout((Number.isFinite(retryAfter)&&retryAfter>0?retryAfter:60)*1000+1000);
    const retriedRefresh=page.waitForResponse(response=>response.url().endsWith("/auth/refresh")&&response.request().method()==="POST");
    await update.click();
    expect((await retriedRefresh).status()).toBe(401);
  }else expect(rejected.status()).toBe(401);
  await expect(page).toHaveURL(/\/login$/);
  expect((await context.cookies()).some(cookie=>["tm_access","tm_refresh"].includes(cookie.name))).toBe(false);
});
