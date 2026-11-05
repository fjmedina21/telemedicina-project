"use client";
import {useEffect,useState} from "react";
import {api,Item} from "@/shared/api/client";
import {localTime} from "@/shared/formatting/date-time";
import type {OperationRunner} from "@/shared/api/operation";

type Props={services:Item[];practitioners:Item[];busy:boolean;run:OperationRunner;onMessage:(message:string)=>void};
export default function SchedulePanel({services,practitioners,busy,run,onMessage:setError}:Props){
  const [service,setService]=useState(""),[practitioner,setPractitioner]=useState(""),[start,setStart]=useState("");
  const [slots,setSlots]=useState<Item[]>([]);
  useEffect(()=>{
    setService(current=>current||String(services[0]?.id??""));
    setPractitioner(current=>current||String(practitioners[0]?.id??""));
  },[services,practitioners]);
  return (<div className="space-y-3 rounded-xl border bg-white p-5">
      <label className="block">Servicio<select className="ml-2 rounded border p-2" value={service}
        onChange={e=>{setService(e.target.value);setSlots([]);}}>
        {services.map(s=><option key={String(s.id)} value={String(s.id)}>{String(s.name)}</option>)}</select></label>
      <label className="block">Profesional<select className="ml-2 rounded border p-2" value={practitioner}
        onChange={e=>{setPractitioner(e.target.value);setSlots([]);}}>
        {practitioners.map(p=><option key={String(p.id)} value={String(p.id)}>{String(p.name)}</option>)}</select></label>
      <label className="block">Inicio (hora de Santo Domingo)<input className="ml-2 rounded border p-2"
        type="datetime-local" value={start} onChange={e=>setStart(e.target.value)}/></label>
      <button disabled={busy||!start||!service||!practitioner} className="rounded border p-2"
        onClick={()=>run(async()=>{
          await api("/admin/slots","POST",{serviceId:service,practitionerId:practitioner,
            startAt:new Date(start+"-04:00").toISOString()}); setStart("");setError("Horario creado.");
        })}>Crear horario</button>
      <button disabled={busy||!service||!practitioner} className="ml-2 rounded border p-2"
        onClick={()=>run(async()=>setSlots(await api<Item[]>(
          "/availability?serviceId="+service+"&practitionerId="+practitioner)))}>Consultar horarios libres</button>
      {slots.map(s=><div key={String(s.id)} className="flex justify-between rounded border p-2">
        {localTime(s.startAt)}
        <button disabled={busy} className="underline" onClick={()=>run(async()=>{
          await api("/admin/slots/"+s.id+"/block","PATCH",{blocked:true});
          setSlots(current=>current.filter(slot=>slot.id!==s.id));
        })}>Bloquear</button>
      </div>)}
    </div>);
}
