"use client";
import {useCallback,useEffect,useRef,useState} from "react";
import {useRouter} from "next/navigation";
import {api,ApiError,type Item} from "@/shared/api/client";
import type {Identity} from "@/shared/api/identity";

type Staff={id:string;email:string;role:string;status:string;version:number};
type Page={items:Staff[];next:string|null};
type AuditPage={items:Item[];next:number|null};
const roles=["Admission","Practitioner","Support"];
const labels:Record<string,string>={Admission:"Admisión",Practitioner:"Profesional",Support:"Soporte",
  PendingActivation:"Pendiente de activación",Active:"Activa",Suspended:"Suspendida"};

function StaffRow({account,busy,run}:{account:Staff;busy:boolean;
  run:(account:Staff,suffix:string,method:string,body:Item)=>Promise<void>}) {
  const [role,setRole]=useState(account.role);
  return <tr>
    <td>{account.email}</td><td>{labels[account.role]}</td><td>{labels[account.status]}</td>
    <td><label>Nuevo rol para {account.email}<select disabled={busy} value={role} onChange={e=>setRole(e.target.value)}>
      {roles.map(value=><option key={value} value={value}>{labels[value]}</option>)}</select></label>
      <button className="button-secondary" disabled={busy||role===account.role}
        onClick={()=>run(account,"role","PATCH",{role})}>Cambiar rol</button>
      {account.status==="PendingActivation"?
        <button className="button-secondary" disabled={busy}
          onClick={()=>run(account,"invitation","POST",{})}>Reenviar invitación</button>:
        <button className="button-secondary" disabled={busy} onClick={()=>run(account,"active","PATCH",
          {active:account.status!=="Active"})}>{account.status==="Active"?"Suspender":"Reactivar"}</button>}
      <button className="button-secondary" disabled={busy}
        onClick={()=>run(account,"revoke-sessions","POST",{})}>Revocar sesiones</button>
    </td>
  </tr>;
}

export default function StaffPanel(){
  const router=useRouter(),generation=useRef(0);
  const [me,setMe]=useState<Identity|null>(null),[rows,setRows]=useState<Staff[]>([]);
  const [busy,setBusy]=useState(true),[error,setError]=useState(""),[message,setMessage]=useState("");
  const [email,setEmail]=useState(""),[role,setRole]=useState("Admission");
  const [roleFilter,setRoleFilter]=useState(""),[statusFilter,setStatusFilter]=useState("");
  const [cursor,setCursor]=useState(""),[previous,setPrevious]=useState<string[]>([]),[next,setNext]=useState<string|null>(null);
  const [events,setEvents]=useState<Item[]>([]),[auditNext,setAuditNext]=useState<number|null>(null);
  const clear=useCallback(()=>{
    generation.current++;setMe(null);setRows([]);setEvents([]);setEmail("");
    setPrevious([]);setNext(null);setAuditNext(null);router.replace("/login");
  },[router]);
  const failure=useCallback((error:unknown)=>{
    if(error instanceof ApiError&&error.status===401){clear();return;}
    if(error instanceof ApiError&&error.status===403){setRows([]);setEvents([]);}
    setError(error instanceof Error?error.message:"No se pudo completar la operación.");
  },[clear]);
  const load=useCallback(async(after="",pageHistory:string[]=[])=>{
    const current=generation.current;
    const query=new URLSearchParams({limit:"20"});
    if(after)query.set("after",after);
    if(roleFilter)query.set("role",roleFilter);
    if(statusFilter)query.set("status",statusFilter);
    const result=await api<Page>("/admin/staff?"+query);
    if(current!==generation.current)return;
    setRows(result.items);setNext(result.next);setCursor(after);setPrevious(pageHistory);
  },[roleFilter,statusFilter]);
  useEffect(()=>{
    const current=++generation.current;
    let live=true;
    window.addEventListener("tm-session-invalidated",clear);
    setBusy(true);setError("");setRows([]);setEvents([]);
    (async()=>{
      const user=await api<Identity>("/auth/me");
      if(!live||current!==generation.current)return;
      if(!user.permissions?.includes("staff.read"))throw new ApiError(403,"No tiene permiso para acceder a cuentas de personal.");
      setMe(user);await load();
    })().catch(error=>{if(live)failure(error);}).finally(()=>{if(live)setBusy(false);});
    return ()=>{live=false;generation.current++;window.removeEventListener("tm-session-invalidated",clear);};
  },[load,failure,clear]);
  async function action(operation:()=>Promise<void>){
    if(busy)return;
    setBusy(true);setError("");setMessage("");
    try{await operation();}catch(error){failure(error);}finally{setBusy(false);}
  }
  async function mutate(account:Staff,suffix:string,method:string,body:Item){
    if(!window.confirm("¿Confirmar la operación sobre esta cuenta? Las sesiones o invitaciones anteriores pueden quedar invalidadas."))return;
    await action(async()=>{
      await api("/admin/staff/"+account.id+"/"+suffix,method,{...body,expectedVersion:account.version});
      await load(cursor,previous);
      setEvents([]);setAuditNext(null);setMessage("Operación confirmada por el servidor. Una cuenta pendiente puede requerir una nueva invitación.");
    });
  }
  async function audit(after=0){
    const current=generation.current;
    const result=await api<AuditPage>("/admin/staff/audit-events?after="+after);
    if(current!==generation.current)return;
    setEvents(after===0?result.items:old=>[...old,...result.items]);setAuditNext(result.next);
  }
  if(!me)return <p role="status">{busy?"Verificando sesión…":error||"Se requiere iniciar sesión."}</p>;
  return <section className="space-y-6" aria-busy={busy}>
    <div className="page-header"><div><h1>Cuentas de personal</h1><p>Administración de cuentas sintéticas del equipo.</p></div>
      <div><span>{me.email}</span><button className="button-secondary" disabled={busy} onClick={()=>action(async()=>{
        await api("/auth/logout","POST",{});clear();
      })}>Salir</button></div></div>
    {error&&<div className="notice error-notice"><p role="alert">{error}</p>
      <button className="button-secondary" disabled={busy} onClick={()=>action(()=>load(cursor,previous))}>Recargar cuentas</button></div>}
    <p role="status" aria-live="polite">{message}</p>
    <form className="space-y-4" onSubmit={event=>{
      event.preventDefault();if(!window.confirm("¿Confirmar la invitación de esta cuenta de personal?"))return;
      void action(async()=>{
        await api("/admin/staff/invitations","POST",{email,role});
        setEmail("");await load();setEvents([]);setAuditNext(null);
        setMessage("Invitación creada en el buzón privado de desarrollo. Caduca en 15 minutos.");
      });
    }}>
      <h2>Invitar personal</h2>
      <label className="block">Correo de personal<input className="block w-full rounded border p-2" type="email"
        required maxLength={254} disabled={busy} value={email} onChange={event=>setEmail(event.target.value)}/></label>
      <label className="block">Rol de personal<select disabled={busy} value={role} onChange={event=>setRole(event.target.value)}>
        {roles.map(value=><option key={value} value={value}>{labels[value]}</option>)}</select></label>
      <button className="button-primary" disabled={busy}>Invitar cuenta</button>
    </form>
    <div className="flex gap-4 flex-wrap">
      <label>Filtrar por rol<select disabled={busy} value={roleFilter} onChange={e=>setRoleFilter(e.target.value)}>
        <option value="">Todos</option>{roles.map(value=><option key={value} value={value}>{labels[value]}</option>)}</select></label>
      <label>Filtrar por estado<select disabled={busy} value={statusFilter} onChange={e=>setStatusFilter(e.target.value)}>
        <option value="">Todos</option>{["PendingActivation","Active","Suspended"].map(value=><option key={value} value={value}>{labels[value]}</option>)}</select></label>
    </div>
    {!rows.length&&!busy?<p>No hay cuentas para estos filtros.</p>:<div className="overflow-x-auto">
      <table><caption className="sr-only">Personal no administrador</caption><thead><tr>
        <th scope="col">Correo</th><th scope="col">Rol</th><th scope="col">Estado</th><th scope="col">Acciones</th>
      </tr></thead><tbody>{rows.map(account=><StaffRow key={account.id+":"+account.version} account={account} busy={busy} run={mutate}/>)}</tbody></table>
    </div>}
    <div className="flex gap-4">
      <button disabled={busy||!previous.length} className="button-secondary" onClick={()=>action(()=>
        load(previous[previous.length-1],previous.slice(0,-1)))}>Página anterior</button>
      <button disabled={busy||!next} className="button-secondary" onClick={()=>action(()=>load(next!,[...previous,cursor]))}>Página siguiente</button>
      <button disabled={busy} className="button-secondary" onClick={()=>action(()=>load(cursor,previous))}>Actualizar</button>
    </div>
    {me.permissions.includes("staff.audit.read")&&<section className="space-y-4">
      <h2>Auditoría de personal</h2>
      <button disabled={busy} className="button-secondary" onClick={()=>action(()=>audit())}>Consultar auditoría</button>
      {events.length?<ul>{events.map(event=><li key={String(event.id)}>
        {String(event.occurredAt)} · {String(event.action)} · {String(event.result)} · {String(event.resourceId)}
      </li>)}</ul>:<p>Sin eventos cargados.</p>}
      {auditNext!==null&&<button disabled={busy} className="button-secondary" onClick={()=>action(()=>audit(auditNext))}>Más eventos</button>}
    </section>}
  </section>;
}
