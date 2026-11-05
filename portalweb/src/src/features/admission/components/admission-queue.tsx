"use client";
import {useEffect,useRef,useState} from "react";
import {ClipboardList} from "lucide-react";
import StatusBadge from "@/shared/components/status-badge";
import {api,Item} from "@/shared/api/client";
import {localTime} from "@/shared/formatting/date-time";
import type {OperationRunner} from "@/shared/api/operation";

type Props={items:Item[];busy:boolean;run:OperationRunner;onRefresh:()=>Promise<void>};
export default function AdmissionQueue({items,busy,run,onRefresh:load}:Props){
  const [selectedAppointment,setSelected]=useState<Item|null>(null);
  const selected=selectedAppointment;
  const [evidence,setEvidence]=useState(""),[until,setUntil]=useState("");
  const [query,setQuery]=useState(""),[statusFilter,setStatusFilter]=useState("");
  const evidenceRef=useRef<HTMLInputElement>(null);
  useEffect(()=>{if(selected) evidenceRef.current?.focus();},[selected]);
  const visible=items.filter(item=>(!statusFilter||item.status===statusFilter)&&
    [item.patientEmail,item.serviceName,item.practitionerName].some(value=>String(value??"").toLocaleLowerCase("es").includes(query.toLocaleLowerCase("es"))));
  return <><section className="surface" aria-label="Solicitudes de cita">
    <div className="panel-toolbar"><div><h2>Solicitudes de cita</h2><p>{visible.length} de {items.length} citas cargadas</p></div>
      <div className="table-filters"><label className="sr-only" htmlFor="queue-search">Buscar citas</label><input id="queue-search" type="search" placeholder="Paciente, servicio o profesional" value={query} onChange={e=>setQuery(e.target.value)}/>
        <select aria-label="Filtrar por estado" value={statusFilter} onChange={e=>setStatusFilter(e.target.value)}><option value="">Todos los estados</option><option value="PendingValidation">Pendientes</option><option value="Confirmed">Confirmadas</option><option value="Cancelled">Canceladas</option><option value="Rejected">Rechazadas</option></select></div>
    </div>
    {visible.length===0?<div className="empty-state"><ClipboardList size={28} aria-hidden="true"/><strong>{items.length===0?"No hay citas registradas.":"No hay coincidencias."}</strong><p>{items.length===0?"Las nuevas solicitudes aparecerán aquí.":"Prueba con otro filtro o término de búsqueda."}</p></div>:
      <><div className="admission-desktop overflow-x-auto"><table className="data-table">
        <thead><tr>{["Paciente","Servicio / profesional","Fecha","Cita","Cobertura","Acción"].map(t=><th key={t} className="p-2">{t}</th>)}</tr></thead>
        <tbody>{visible.map(a=><tr key={String(a.id)} className="border-t">
          <td className="max-w-xs break-words p-2 align-top">{String(a.patientEmail)}</td><td className="p-2 align-top"><span className="cell-primary">{String(a.serviceName)}</span><span className="cell-secondary">{String(a.practitionerName)}</span></td>
          <td className="p-2 align-top">{localTime(a.startAt)}</td><td className="p-2 align-top"><StatusBadge value={a.status}/></td><td className="p-2 align-top"><StatusBadge value={a.coverageStatus}/></td>
          <td className="p-2 align-top"><button className="button-secondary" disabled={busy || !["PendingValidation","Confirmed"].includes(String(a.status))}
            onClick={()=>{setSelected(a);setEvidence("");setUntil("");}}>Revisar</button></td>
        </tr>)}</tbody>
      </table></div><div className="admission-mobile">{visible.map(a=><article key={String(a.id)} className="appointment-mobile-card">
        <span className="cell-secondary">Paciente</span><p className="cell-primary">{String(a.patientEmail)}</p>
        <h3>{String(a.serviceName)}</h3><p className="cell-secondary">{String(a.practitionerName)}</p>
        <p className="appointment-date">{localTime(a.startAt)}</p>
        <div className="mobile-statuses"><div><span className="cell-secondary">Cita</span><StatusBadge value={a.status}/></div><div><span className="cell-secondary">Cobertura</span><StatusBadge value={a.coverageStatus}/></div></div>
        <button className="button-secondary" disabled={busy||!["PendingValidation","Confirmed"].includes(String(a.status))}
          onClick={()=>{setSelected(a);setEvidence("");setUntil("");}}>Revisar</button>
      </article>)}</div></>}</section>
      {selected && <form className="space-y-3 rounded-xl border bg-white p-5" onSubmit={e=>e.preventDefault()}>
        <h2 className="font-bold">Revisión manual · {String(selected.patientEmail)}</h2>
        <p>Aseguradora: {String(selected.insurer)} · Afiliación: ****{String(selected.memberLast4)}</p>
        <label className="block">Referencia de evidencia (sin texto clínico)
          <input ref={evidenceRef} className="block w-full rounded border p-2" required maxLength={250} value={evidence}
            onChange={e=>setEvidence(e.target.value)}/></label>
        <label className="block">Vigencia hasta (hora de Santo Domingo)
          <input className="block rounded border p-2" type="datetime-local" value={until}
            onChange={e=>setUntil(e.target.value)}/></label>
        <p>Una aprobación requiere evidencia y vigencia que cubra la cita. Una denegación no autoriza atención particular.</p>
        <div className="flex flex-wrap gap-3">{[
          ["ManualReview","Mantener en revisión"],["Approved","Aprobar cobertura y confirmar"],["Denied","Denegar cobertura"]
        ].map(([status,label])=><button key={status} disabled={busy||!evidence.trim()||(status==="Approved"&&!until)}
          className={status==="Approved"?"button-primary":status==="Denied"?"button-danger":"button-secondary"}
          onClick={()=>run(async()=>{
            await api("/admin/appointments/"+selected.id+"/validation","PATCH",{
              status,evidence,version:selected.version,validUntil:until?new Date(until+"-04:00").toISOString():null
            });
            setSelected(null); await load();
          })}>{label}</button>)}</div>
        <button onClick={()=>setSelected(null)} className="underline">Cerrar</button>
      </form>}</>;
}
