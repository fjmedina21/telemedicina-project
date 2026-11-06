"use client";
import {useCallback,useEffect,useRef,useState} from "react";
import {RefreshCw,LogOut} from "lucide-react";
import {useRouter} from "next/navigation";
import {api,ApiError,Item,StaleResponse} from "@/shared/api/client";
import type {Identity} from "@/shared/api/identity";
import CatalogPanel from "@/features/catalog/components/catalog-panel";
import AdmissionQueue from "@/features/admission/components/admission-queue";
import AuditEvents from "@/features/audit/components/audit-events";
import SchedulePanel from "@/features/scheduling/components/schedule-panel";
import DeliveryPanel from "@/features/notifications/components/delivery-panel";

export default function PortalConsole({view="queue"}:{view?:"queue"|"audit"|"schedule"|"catalog"|"delivery"}) {
  const router=useRouter();
  const [user,setUser]=useState<Identity|null>(null);
  const [items,setItems]=useState<Item[]>([]),[busy,setBusy]=useState(true),[error,setError]=useState("");
  const [services,setServices]=useState<Item[]>([]),[practitioners,setPractitioners]=useState<Item[]>([]);

  const alive=useRef(true),generation=useRef(0),loadSequence=useRef(0),running=useRef(false);
  const load=useCallback(async()=>{
    const epoch=generation.current,request=++loadSequence.current;
    try{
      const me=await api<Identity>("/auth/me");
      const permission={queue:"admission.manage",audit:"audit.read",schedule:"schedule.manage",catalog:"catalog.manage",delivery:"delivery.read"}[view];
      if(!me.permissions?.includes(permission))throw new ApiError(403,"No tiene permiso para acceder a esta sección.");
      let records:Item[]=[],s:Item[]=[],p:Item[]=[];
      if(view==="audit")records=await api<Item[]>("/admin/audit-events");
      else if(view==="queue")records=[];
      else if(view==="delivery")records=await api<Item[]>("/admin/email-delivery");
      else [s,p]=await Promise.all([api<Item[]>("/admin/catalog/services"),api<Item[]>("/admin/catalog/practitioners")]);
      if(!alive.current||epoch!==generation.current||request!==loadSequence.current)return;
      setUser(me);setItems(records);setServices(s);setPractitioners(p);
    }catch(error){
      if(alive.current&&epoch===generation.current&&request===loadSequence.current&&!(error instanceof StaleResponse))throw error;
    }
  },[view]);
  const failure=useCallback((e:unknown)=>{
    if(!alive.current||e instanceof StaleResponse)return;
    if(e instanceof ApiError && e.status===401) {
      generation.current++;loadSequence.current++;
      setUser(null); setItems([]); setServices([]); setPractitioners([]); router.replace("/login");
    } else if(e instanceof ApiError && e.status===403) {
      generation.current++;loadSequence.current++;
      setUser(null); setItems([]); setServices([]); setPractitioners([]); setError(e.message);
    } else setError(e instanceof Error?e.message:"Error de conexión");
  },[router]);
  useEffect(()=>{
    const clear=()=>{generation.current++;loadSequence.current++;setUser(null);setItems([]);setServices([]);setPractitioners([]);router.replace("/login");};
    window.addEventListener("tm-session-invalidated",clear);
    return ()=>window.removeEventListener("tm-session-invalidated",clear);
  },[router]);
  useEffect(()=>{
    alive.current=true;
    let mounted=true;
    load().catch(e=>{if(mounted) failure(e);}).finally(()=>{if(mounted) setBusy(false);});
    return ()=>{mounted=false;alive.current=false;generation.current++;loadSequence.current++;};
  },[load,failure]);
  async function run(action:()=>Promise<void>) {
    if(busy||running.current||!alive.current)return false;
    running.current=true;
    const epoch=generation.current;
    setBusy(true);setError("");
    try{await action();return alive.current&&epoch===generation.current;}
    catch(e){if(alive.current&&epoch===generation.current)failure(e);return false;}
    finally{running.current=false;if(alive.current)setBusy(false);}
  }
  if(!user) return <p role="status">{busy?"Verificando sesión…":error||"Se requiere iniciar sesión."}</p>;
  const titles={queue:"Admisión y cobertura",audit:"Auditoría",catalog:"Catálogo",delivery:"Entregas de correo",schedule:"Horarios"};
  const descriptions={queue:"Revisa las solicitudes y mantén al día cada cita.",audit:"Consulta el registro de acciones del equipo.",catalog:"Organiza los servicios y profesionales disponibles.",delivery:"Consulta el estado de los mensajes enviados.",schedule:"Organiza la disponibilidad de los profesionales."};
  return <section className="space-y-6" aria-busy={busy}>
    <div className="page-header">
      <div><span className="eyebrow">TU ESPACIO DE TRABAJO</span><h1>{titles[view]}</h1><p className="page-description">{descriptions[view]}</p></div>
      <div className="account-actions"><span>{user.email}<br/>Admisión</span><button className="button-secondary" disabled={busy}
        onClick={()=>run(async()=>{
          try{await api("/auth/logout","POST",{});}
          finally{generation.current++;loadSequence.current++;setUser(null);setItems([]);setServices([]);setPractitioners([]);router.replace("/login");}
        })}><LogOut size={15} aria-hidden="true"/>Salir</button></div>
    </div>
    {error&&<p role="alert" className="notice error-notice">{error}</p>}
    {view!=="queue"&&<div className="flex justify-end"><button disabled={busy} className="button-secondary" onClick={()=>run(load)}>
      <RefreshCw size={15} aria-hidden="true"/>{busy?"Procesando…":"Actualizar"}</button></div>}

    {view==="catalog" && <CatalogPanel services={services} practitioners={practitioners} busy={busy}
      onSave={async(path,method,body)=>run(async()=>{await api(path,method,body);await load();})}/>}
    {view==="queue" && <AdmissionQueue key={user.userId}/>}
    {view==="audit" && <AuditEvents items={items} busy={busy} run={run} onItemsChange={setItems}/>}
    {view==="schedule" && <SchedulePanel services={services} practitioners={practitioners} busy={busy}
      run={run} onMessage={setError}/>}
    {view==="delivery" && <DeliveryPanel items={items}/>}
  </section>;
}
