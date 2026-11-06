"use client";
import {useEffect,useRef,useState} from "react";
import {ApiError,Item,StaleResponse} from "@/shared/api/client";
import {localTime} from "@/shared/formatting/date-time";
import type {OperationRunner} from "@/shared/api/operation";
import {scheduleRepository,santoDomingoInput,scheduleInstant,type BooleanFilter,type ScheduleFilters,
  type SchedulePage,type ScheduleSlot} from "../data/schedule-repository";

type Props={services:Item[];practitioners:Item[];busy:boolean;run:OperationRunner;onMessage:(message:string)=>void};
const emptyPage:SchedulePage={items:[],totalCount:0,offset:0,limit:100,hasMore:false};
function publicService(service:Item){
  return service.active===true&&(service.isSynthetic===true||String(service.approvalReference??"").trim()!=="");
}
function assigned(person:Item,serviceId:string){
  return person.active===true&&Array.isArray(person.serviceIds)&&person.serviceIds.includes(serviceId);
}
export default function SchedulePanel({services,practitioners,busy,run,onMessage}:Props){
  const [service,setService]=useState(""),[practitioner,setPractitioner]=useState(""),[start,setStart]=useState("");
  const [from,setFrom]=useState(()=>santoDomingoInput(new Date()));
  const [to,setTo]=useState(()=>santoDomingoInput(new Date(Date.now()+14*24*3600000)));
  const [blocked,setBlocked]=useState<BooleanFilter>(""),[reserved,setReserved]=useState<BooleanFilter>("");
  const [limit,setLimit]=useState(100),[page,setPage]=useState<SchedulePage>(emptyPage);
  const [loaded,setLoaded]=useState(false),[conflict,setConflict]=useState(false);
  const [message,setMessage]=useState(""),[mutating,setMutating]=useState(false);
  const [uncertain,setUncertain]=useState<{
    serviceId:string;practitionerId:string;start:string;
  }|null>(null);
  const alive=useRef(true),session=useRef(0),revision=useRef(0),initialized=useRef(false);
  const chosenService=services.find(s=>String(s.id)===service);
  const people=service?practitioners.filter(p=>assigned(p,service)):[];
  const canCreate=Boolean(chosenService&&publicService(chosenService)&&people.some(p=>String(p.id)===practitioner));
  function invalidate(){
    revision.current++;setPage(emptyPage);setLoaded(false);setConflict(false);setMessage("");
  }
  useEffect(()=>{
    alive.current=true;
    const clear=()=>{
      session.current++;revision.current++;setPage(emptyPage);setLoaded(false);setConflict(false);
      setService("");setPractitioner("");setStart("");setMessage("");setUncertain(null);
    };
    window.addEventListener("tm-session-invalidated",clear);
    return ()=>{alive.current=false;session.current++;revision.current++;window.removeEventListener("tm-session-invalidated",clear);};
  },[]);
  useEffect(()=>{
    if(!initialized.current&&services.length){
      initialized.current=true;
      const first=services.find(publicService);
      const id=first?String(first.id):"";
      setService(id);setPractitioner(String(practitioners.find(p=>assigned(p,id))?.id??""));
      invalidate();return;
    }
    if(service&&!services.some(s=>String(s.id)===service)){
      setService("");setPractitioner("");invalidate();
    }else if(practitioner&&!practitioners.some(p=>String(p.id)===practitioner&&assigned(p,service))){
      setPractitioner("");invalidate();
    }
  },[services,practitioners,service,practitioner]);
  const filters:ScheduleFilters={serviceId:service,practitionerId:practitioner,from,to,blocked,reserved,limit};
  function current(s:number,r:number){return alive.current&&s===session.current&&r===revision.current;}
  async function fetchPage(offset:number,s=session.current,r=revision.current){
    const result=await scheduleRepository.list(filters,offset);
    if(!current(s,r))return;
    setPage(result);setLoaded(true);setConflict(false);
  }
  async function consult(offset=0){
    const s=session.current,r=revision.current;
    await run(async()=>{
      try{await fetchPage(offset,s,r);}
      catch(error){if(current(s,r)&&!(error instanceof StaleResponse))throw error;}
    });
  }
  async function create(){
    if(!canCreate||!start||mutating||uncertain)return;
    const s=session.current,r=revision.current,sent=start;
    const intention={serviceId:service,practitionerId:practitioner,start:sent};
    await run(async()=>{
      setMutating(true);
      let confirmed=false;
      try{
        await scheduleRepository.create(service,practitioner,sent);
        confirmed=true;
        if(!current(s,r))return;
        setStart(value=>value===sent?"":value);setMessage("Horario creado.");onMessage("");
        setLoaded(false);setPage(emptyPage);await fetchPage(0,s,r);
      }catch(error){
        if(!current(s,r)||error instanceof StaleResponse)return;
        if(!confirmed&&(!(error instanceof ApiError)||error.status>=500||
            (error.status>=200&&error.status<300))){
          setUncertain(intention);
          setMessage("No se pudo confirmar la creación. Comprueba el horario antes de repetirla.");
        }
        if(error instanceof ApiError&&error.status===409)setConflict(true);
        throw error;
      }finally{if(alive.current)setMutating(false);}
    });
  }
  async function reconcileCreation(){
    const pending=uncertain,s=session.current;
    if(!pending)return;
    await run(async()=>{
      try{
        const instant=scheduleInstant(pending.start);
        const result=await scheduleRepository.list({
          serviceId:pending.serviceId,practitionerId:pending.practitionerId,
          from:pending.start,
          to:santoDomingoInput(new Date(Date.parse(instant)+60000)),
          blocked:"",reserved:"",limit:100
        });
        if(!alive.current||s!==session.current)return;
        const found=result.items.some(slot=>
          slot.serviceId===pending.serviceId&&
          slot.practitionerId===pending.practitionerId&&
          Date.parse(slot.startAt)===Date.parse(instant));
        setUncertain(value=>value===pending?null:value);
        if(found){
          setStart(value=>value===pending.start?"":value);
          setMessage("La consulta confirma que el horario ya existe. No repitas la creación.");
        }else{
          setMessage("La consulta no encontró el horario. Puedes revisar el formulario y volver a crear.");
        }
        onMessage("");
      }catch(error){
        if(!alive.current||s!==session.current||error instanceof StaleResponse)return;
        throw error;
      }
    });
  }
  async function toggle(slot:ScheduleSlot){
    const s=session.current,r=revision.current;
    await run(async()=>{
      setMutating(true);
      try{
        await scheduleRepository.block(slot);
        if(!current(s,r))return;
        setMessage(slot.blocked?"Horario desbloqueado.":"Horario bloqueado.");onMessage("");
        setLoaded(false);setPage(emptyPage);await fetchPage(page.offset,s,r);
      }catch(error){
        if(!current(s,r)||error instanceof StaleResponse)return;
        if(error instanceof ApiError&&error.status===409)setConflict(true);
        throw error;
      }finally{if(alive.current)setMutating(false);}
    });
  }
  return <div className="space-y-5 rounded-xl border bg-white p-5">
    <p>Agenda manual · Hora de America/Santo_Domingo. La disponibilidad no garantiza una reserva.</p>
    <div className="grid gap-3 md:grid-cols-2">
      <label className="block">Servicio<select className="ml-2 max-w-full rounded border p-2" value={service} disabled={mutating}
        onChange={e=>{setService(e.target.value);setPractitioner("");invalidate();}}>
        <option value="">Todos los servicios</option>
        {services.map(s=><option key={String(s.id)} value={String(s.id)}>{String(s.name)}{s.active?"":" · Inactivo"}</option>)}
      </select></label>
      <label className="block">Profesional<select className="ml-2 max-w-full rounded border p-2" value={practitioner} disabled={mutating||!service}
        onChange={e=>{setPractitioner(e.target.value);invalidate();}}>
        <option value="">Todos los profesionales asociados</option>
        {people.map(p=><option key={String(p.id)} value={String(p.id)}>{String(p.name)}</option>)}
      </select></label>
      <label className="block">Desde (hora de Santo Domingo)<input className="ml-2 rounded border p-2" type="datetime-local"
        value={from} disabled={mutating} onChange={e=>{setFrom(e.target.value);invalidate();}}/></label>
      <label className="block">Hasta (hora de Santo Domingo)<input className="ml-2 rounded border p-2" type="datetime-local"
        value={to} disabled={mutating} onChange={e=>{setTo(e.target.value);invalidate();}}/></label>
      <label className="block">Bloqueo<select className="ml-2 rounded border p-2" value={blocked} disabled={mutating}
        onChange={e=>{setBlocked(e.target.value as BooleanFilter);invalidate();}}>
        <option value="">Todos</option><option value="false">No bloqueados</option><option value="true">Bloqueados</option>
      </select></label>
      <label className="block">Reserva<select className="ml-2 rounded border p-2" value={reserved} disabled={mutating}
        onChange={e=>{setReserved(e.target.value as BooleanFilter);invalidate();}}>
        <option value="">Todos</option><option value="false">Sin reserva</option><option value="true">Reservados</option>
      </select></label>
      <label className="block">Horarios por página<select className="ml-2 rounded border p-2" value={limit} disabled={mutating}
        onChange={e=>{setLimit(Number(e.target.value));invalidate();}}>
        <option value={5}>5</option><option value={25}>25</option><option value={100}>100</option>
      </select></label>
    </div>
    <p>Consulta un rango de hasta 31 días. Los horarios reservados se muestran sin datos de pacientes.</p>
    <button disabled={busy||mutating} className="button-secondary" onClick={()=>consult()}>Consultar horarios</button>
    {conflict&&<div role="alert" className="space-y-2">
      <p>El horario cambió o la operación ya no está permitida. Tu formulario se conserva; recarga los horarios antes de continuar.</p>
      <button className="button-secondary" disabled={busy} onClick={()=>consult(page.offset)}>Recargar horarios</button>
    </div>}
    {message&&<p role="status">{message}</p>}
    {!loaded&&<p role="status">{busy?"Consultando horarios…":"Todavía no se han consultado estos filtros."}</p>}
    {loaded&&<>
      <p role="status">{page.totalCount===0?"No hay horarios que coincidan con estos filtros.":
        `Mostrando ${page.offset+1}–${page.offset+page.items.length} de ${page.totalCount} horarios.`}</p>
      {page.items.length===0&&page.totalCount>0&&<p>Esta página no contiene horarios. Vuelve a la página anterior.</p>}
      <div className="overflow-x-auto"><table className="data-table" aria-label="Agenda administrativa">
        <thead><tr>{["Servicio / profesional","Inicio y fin","Estado","Acción"].map(label=><th key={label}>{label}</th>)}</tr></thead>
        <tbody>{page.items.map(slot=>{
          const future=Date.parse(slot.startAt)>Date.now();
          const offer=slot.servicePublishable&&slot.practitionerActive&&slot.assignmentActive;
          const allowed=future&&!slot.reserved&&(!slot.blocked||offer);
          return <tr key={slot.id} data-slot-id={slot.id}>
            <td><span className="cell-primary">{slot.serviceName}</span><span className="cell-secondary">{slot.practitionerName}</span></td>
            <td>{localTime(slot.startAt)}<br/>{localTime(slot.endAt)}</td>
            <td>{slot.reserved?"Reservado":"Sin reserva"} · {slot.blocked?"Bloqueado":"No bloqueado"}
              {!offer&&<span className="cell-secondary">Oferta no disponible</span>}</td>
            <td><button className="button-secondary" disabled={busy||conflict||!allowed} onClick={()=>toggle(slot)}>
              {slot.blocked?"Desbloquear":"Bloquear"}</button></td>
          </tr>;
        })}</tbody>
      </table></div>
      <div className="flex flex-wrap gap-3" aria-label="Paginación de horarios">
        <button className="button-secondary" disabled={busy||page.offset===0} onClick={()=>consult(Math.max(0,page.offset-page.limit))}>Página anterior de horarios</button>
        <button className="button-secondary" disabled={busy||!page.hasMore} onClick={()=>consult(page.offset+page.limit)}>Página siguiente de horarios</button>
      </div>
    </>}
    <section className="space-y-3 border-t pt-4" aria-label="Crear horario manual">
      <h2 className="text-lg font-bold">Crear horario manual</h2>
      {services.length===0&&<p>No hay servicios cargados para crear horarios.</p>}
      {service&&!people.length&&<p>No hay profesionales activos asociados a este servicio.</p>}
      <label className="block">Inicio (hora de Santo Domingo)<input className="ml-2 rounded border p-2" type="datetime-local"
        value={start} disabled={mutating} onChange={e=>setStart(e.target.value)}/></label>
      <button disabled={busy||mutating||Boolean(uncertain)||!start||!canCreate} className="button-primary" onClick={create}>Crear horario</button>
      {uncertain&&<div role="alert">
        <p>Hay una creación sin confirmar. El formulario se conserva.</p>
        <button disabled={busy||mutating} className="button-secondary" onClick={reconcileCreation}>
          Comprobar creación pendiente
        </button>
      </div>}
      <p>Selecciona un servicio publicable y un profesional asociado. Si la respuesta tarda o falla, consulta los horarios antes de repetir la creación.</p>
    </section>
  </div>;
}
