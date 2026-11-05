const {chromium,expect}=require('../../../node_modules/@playwright/test');
const path=require('node:path');
const fs=require('node:fs/promises');
const ART=__dirname;
(async()=>{
 const browser=await chromium.launch();
 try {
 const page=await browser.newPage({viewport:{width:1440,height:1000},deviceScaleFactor:1});
 await page.goto('http://localhost:5096/login');
 await expect(page.getByRole('heading',{name:'Acceso de admisión',exact:true})).toBeVisible();
 await page.screenshot({path:path.join(ART,'portal-acceso.png'),fullPage:true});
 const appointments=[
 {id:'synthetic-one',patientEmail:'paciente.demo@example.invalid',serviceName:'Medicina general',practitionerName:'Profesional de demostración',startAt:'2026-11-10T13:00:00Z',status:'PendingValidation',coverageStatus:'ManualReview',insurer:'Aseguradora sintética',memberLast4:'0000',version:1},
 {id:'synthetic-two',patientEmail:'otro.paciente@example.invalid',serviceName:'Seguimiento general',practitionerName:'Profesional de demostración',startAt:'2026-11-10T14:00:00Z',status:'Confirmed',coverageStatus:'Approved',insurer:'Aseguradora sintética',memberLast4:'0000',version:1},
 ];
 await page.route('**/api/backend/**',route=>{
  const pathname=new URL(route.request().url()).pathname;
  const data=pathname.endsWith('/auth/me')?{email:'equipo.demo@example.invalid',role:'Admission'}:pathname.endsWith('/admin/work-queue')?appointments:[];
  return route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({success:true,status:200,data})});
 });
 await page.goto('http://localhost:5096/admision');
 await expect(page.getByRole('row').filter({hasText:'paciente.demo@example.invalid'})).toBeVisible();
 await page.getByRole('searchbox',{name:'Buscar citas'}).fill('sin-coincidencias');
 await expect(page.getByText('No hay coincidencias.',{exact:true})).toBeVisible();
 await page.getByRole('searchbox',{name:'Buscar citas'}).clear();
 await page.getByRole('combobox',{name:'Filtrar por estado'}).selectOption('Confirmed');
 await expect(page.getByRole('row').filter({hasText:'otro.paciente@example.invalid'})).toBeVisible();
 await expect(page.getByRole('row').filter({hasText:'paciente.demo@example.invalid'})).toHaveCount(0);
 await page.getByRole('combobox',{name:'Filtrar por estado'}).selectOption('');
 await page.getByRole('heading',{name:'Admisión y cobertura',exact:true}).click();
 await page.screenshot({path:path.join(ART,'portal-admision.png'),fullPage:true});
 await page.setViewportSize({width:390,height:844});
 await page.screenshot({path:path.join(ART,'portal-movil.png'),fullPage:true});
 if(await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth))throw Error('Portal horizontal overflow');
 const mobile=await browser.newPage({viewport:{width:390,height:844},deviceScaleFactor:2});
 await mobile.goto('http://127.0.0.1:5097');
 await mobile.locator('flutter-view').waitFor();
 await mobile.waitForTimeout(2200);
 await mobile.screenshot({path:path.join(ART,'app-acceso.png'),fullPage:true});
 await mobile.locator('flt-semantics-placeholder').evaluate(element=>element.click());
 await mobile.route('**/api/v1/**',route=>{
  const pathname=new URL(route.request().url()).pathname;
  let data=[];
  if(pathname.endsWith('/auth/login'))data={role:'Patient',accessToken:'synthetic-preview',refreshToken:'synthetic-refresh'};
  else if(pathname.endsWith('/patients/me'))data={profile:{firstName:'Paciente',lastName:'Demostración',birthDate:'1990-01-01',phone:'8095550101',address:'Dirección sintética',emergencyName:'Contacto de demostración',emergencyPhone:'8095550102',allergies:[],medications:[],conditions:[]},version:1};
  else if(pathname.endsWith('/appointments/me'))data=[{id:'synthetic-preview',serviceName:'Medicina general',practitionerName:'Profesional de demostración',startAt:'2026-11-10T13:00:00Z',status:'Confirmed',coverageStatus:'Approved',version:1}];
  else if(pathname.endsWith('/notifications/preferences'))data={emailEnabled:false,version:0};
  return route.fulfill({status:200,headers:{'content-type':'application/json','access-control-allow-origin':'*','access-control-allow-headers':'Content-Type,Authorization,X-Telemedicina-Client','access-control-allow-methods':'GET,POST,PATCH,OPTIONS'},body:JSON.stringify({success:true,status:200,data})});
 });
 await mobile.getByRole('textbox',{name:'Correo electrónico',exact:true}).fill('paciente.demo@example.invalid');
 await mobile.getByRole('textbox',{name:'Contraseña (mínimo 12 caracteres)',exact:true}).fill('synthetic-preview-only');
 await mobile.getByRole('button',{name:'Entrar',exact:true}).click();
 await mobile.screenshot({path:path.join(ART,'app-interaccion.png'),fullPage:true});
 await fs.writeFile(path.join(ART,'app-semantics.txt'),await mobile.locator('body').ariaSnapshot());
 await mobile.getByRole('tab',{name:/Mis citas/}).or(mobile.getByRole('button',{name:/Mis citas/})).or(mobile.getByLabel(/Mis citas/)).first().click();
 await expect(mobile.getByRole('group',{name:/Medicina general.*Confirmada.*Aprobada/})).toBeVisible();
 await mobile.mouse.move(10,240);
 await mobile.waitForTimeout(1800);
 await mobile.screenshot({path:path.join(ART,'app-citas.png'),fullPage:true});
 await browser.close();
 await fs.writeFile(path.join(ART,'visual-checks.json'),JSON.stringify({desktop:1440,mobile:390,queueSearch:true,queueFilter:true,portalHorizontalOverflow:false,syntheticDataOnly:true},null,2));
 console.log('Playwright visual checks: PASS');
 } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
