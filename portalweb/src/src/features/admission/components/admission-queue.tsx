"use client";
import {useEffect,useRef,useState} from "react";
import StatusBadge from "@/shared/components/status-badge";
import {ApiError,StaleResponse} from "@/shared/api/client";
import {localTime} from "@/shared/formatting/date-time";
import {admissionRepository,coverageLabel,emptyFilters,validityLabel,type Appointment,type HistoryEntry,type QueueFilters} from "../data/admission-repository";

const appointmentStates=[["PendingValidation","Pendiente"],["Confirmed","Confirmada"],["Cancelled","Cancelada"],["Rejected","Rechazada"]];
const coverageStates=["Pending","ManualReview","Approved","Denied","Expired"];
const editable=(a:Appointment)=>["PendingValidation","Confirmed"].includes(a.status)&&new Date(a.startAt).getTime()>Date.now();
const localInput=(value:unknown)=>value?new Date(new Date(String(value)).getTime()-4*3600000).toISOString().slice(0,16):"";

export default function AdmissionQueue(){
  const [items,setItems]=useState<Appointment[]>([]),[filters,setFilters]=useState<QueueFilters>({...emptyFilters});
  const [queried,setQueried]=useState(false),[loading,setLoading]=useState(false),[working,setWorking]=useState(false),[error,setError]=useState("");
  const [selected,setSelected]=useState<Appointment|null>(null),[history,setHistory]=useState<HistoryEntry[]>([]);
  const [evidence,setEvidence]=useState(""),[until,setUntil]=useState(""),[dirty,setDirty]=useState(false),[conflict,setConflict]=useState(false);
  const alive=useRef(true),epoch=useRef(0),listSequence=useRef(0),detailSequence=useRef(0),sending=useRef(false);
  const evidenceRef=useRef<HTMLInputElement>(null);
  const current=(generation:number)=>alive.current&&epoch.current===generation;
  const failure=(reason:unknown)=>{if(!(reason instanceof StaleResponse))setError(reason instanceof Error?reason.message:"Error de conexión.");};
  async function loadQueue(snapshot:QueueFilters){
    const generation=epoch.current,request=++listSequence.current;
    setLoading(true);setError("");
    try{
      const data=await admissionRepository.list(snapshot);
      if(current(generation)&&request===listSequence.current){setItems(data);setQueried(true);}
    }catch(reason){if(current(generation)&&request===listSequence.current)failure(reason);}
    finally{if(current(generation)&&request===listSequence.current)setLoading(false);}
  }
  useEffect(()=>{
    alive.current=true;void loadQueue({...emptyFilters});
    return ()=>{alive.current=false;epoch.current++;listSequence.current++;detailSequence.current++;};
    // The authenticated parent remounts this feature on session changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  },[]);
  useEffect(()=>{if(selected)evidenceRef.current?.focus();},[selected]);
  function changeFilter(key:keyof QueueFilters,value:string){
    listSequence.current++;setLoading(false);setQueried(false);setItems([]);setFilters(old=>({...old,[key]:value}));
  }
  function discard(message="¿Descartar el borrador de revisión para abrir otra cita?"){
    return !dirty||window.confirm(message);
  }
  async function open(id:string,force=false){
    if(sending.current||working)return;
    if(force?!window.confirm("¿Descartar el borrador y cargar la versión actual de esta cita?"):!discard())return;
    const generation=epoch.current,request=++detailSequence.current;
    setWorking(true);setError("");
    try{
      const [detail,events]=await Promise.all([admissionRepository.detail(id),admissionRepository.history(id)]);
      if(!current(generation)||request!==detailSequence.current)return;
      setSelected({...detail});setHistory(events);setEvidence(String(detail.evidence??""));setUntil(localInput(detail.validUntil));
      setDirty(false);setConflict(false);
    }catch(reason){if(current(generation)&&request===detailSequence.current)failure(reason);}
    finally{if(current(generation)&&request===detailSequence.current)setWorking(false);}
  }
  function close(){
    if(working||!discard("¿Descartar el borrador de revisión y cerrar la cita?"))return;
    detailSequence.current++;setSelected(null);setHistory([]);setEvidence("");setUntil("");setDirty(false);setConflict(false);
  }
  async function review(status:string){
    if(!selected||sending.current||working||conflict||!editable(selected))return;
    const generation=epoch.current,base={...selected},draft=evidence;
    sending.current=true;setWorking(true);setError("");
    try{
      const validUntil=status==="Approved"&&until?new Date(until+"-04:00").toISOString():null;
      await admissionRepository.review(base.id,{status,evidence:draft,validUntil,version:base.version});
      if(!current(generation))return;
      setSelected(null);setHistory([]);setEvidence("");setUntil("");setDirty(false);setConflict(false);
      await loadQueue({...filters});
    }catch(reason){
      if(current(generation)){
        failure(reason);
        if(reason instanceof ApiError&&(reason.status===409||reason.status>=500))setConflict(true);
      }
    }finally{sending.current=false;if(current(generation))setWorking(false);}
  }
  return <div className="space-y-6" aria-busy={loading||working}>
    {error&&<p role="alert" className="notice error-notice">{error} Tu borrador se conserva.</p>}
    <section className="surface" aria-label="Solicitudes de cita">
      <h2>Solicitudes de cita</h2><p>Hasta 200 citas por consulta. Acota fechas y estados para consultar más resultados.</p>
      <form className="table-filters" onSubmit={event=>{event.preventDefault();void loadQueue({...filters});}}>
        <label>Desde (hora de Santo Domingo)<input disabled={working} type="datetime-local" value={filters.from} onChange={e=>changeFilter("from",e.target.value)}/></label>
        <label>Hasta (hora de Santo Domingo)<input disabled={working} type="datetime-local" value={filters.to} onChange={e=>changeFilter("to",e.target.value)}/></label>
        <label>Estado de cita<select aria-label="Estado de cita" disabled={working} value={filters.status} onChange={e=>changeFilter("status",e.target.value)}><option value="">Todas las citas</option>{appointmentStates.map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></label>
        <label>Estado de revisión manual<select aria-label="Estado de revisión manual" disabled={working} value={filters.coverageStatus} onChange={e=>changeFilter("coverageStatus",e.target.value)}><option value="">Todas las revisiones</option>{coverageStates.map(value=><option key={value} value={value}>{coverageLabel(value)}</option>)}</select></label>
        <button className="button-secondary" disabled={loading||working}>Consultar citas</button>
      </form>
      {loading?<p role="status">Consultando citas…</p>:!queried?<p>Consulta los filtros seleccionados.</p>:!items.length?<p>No hay citas que coincidan con estos filtros.</p>:
      <div className="overflow-x-auto"><table className="data-table" aria-label="Cola de admisión">
        <thead><tr>{["Paciente","Servicio / profesional","Fecha","Cita","Revisión manual","Acción"].map(label=><th key={label}>{label}</th>)}</tr></thead>
        <tbody>{items.map(a=><tr key={a.id} data-appointment-id={a.id}>
          <td>{String(a.patientEmail??"")}</td><td>{String(a.serviceName??"")}<br/>{String(a.practitionerName??"")}</td>
          <td>{localTime(a.startAt)}</td><td><StatusBadge value={a.status}/></td>
          <td>{coverageLabel(a.coverageStatus)}{a.coverageRequiresReview&&<span className="cell-secondary">Requiere revisión</span>}</td>
          <td><button className="button-secondary" disabled={working} onClick={()=>void open(a.id)}>Ver detalle y revisar</button></td>
        </tr>)}</tbody></table></div>}
    </section>
    {selected&&<section className="surface space-y-3" aria-label="Detalle de cita">
      <h2>Detalle de cita · {String(selected.patientEmail??"")}</h2>
      <p>{String(selected.serviceName??"")} · {String(selected.practitionerName??"")} · {localTime(selected.startAt)}</p>
      <p>Estado de cita: <StatusBadge value={selected.status}/></p>
      <p>Revisión de cobertura: {coverageLabel(selected.coverageStatus)}. Método: manual.</p>
      {validityLabel(selected.coverageValidityReason)&&<p>{validityLabel(selected.coverageValidityReason)}</p>}
      <p>El estado de la cita y la revisión de cobertura son independientes. Una revisión vencida requiere nueva gestión administrativa.</p>
      <p>Aseguradora: {String(selected.insurer??"")} · Afiliación: ****{String(selected.memberLast4??"")}</p>
      {Boolean(selected.reviewedAt)&&<p>Última revisión: {localTime(selected.reviewedAt)}</p>}
      <h3>Historial de cita</h3>
      {!history.length?<p>No hay eventos disponibles.</p>:<ol aria-label="Historial de cita">{history.map(h=><li key={h.sequence}>{localTime(h.occurredAt)} · Cita: <StatusBadge value={h.newStatus}/> · {coverageLabel(h.newCoverage)} · Versión {h.version}</li>)}</ol>}
      {editable(selected)?<form className="space-y-3" aria-label="Revisión manual" onSubmit={event=>event.preventDefault()}>
        <label className="block">Referencia de evidencia (sin texto clínico)<input ref={evidenceRef} className="block w-full rounded border p-2" required maxLength={250} value={evidence} disabled={working} onChange={e=>{setEvidence(e.target.value);setDirty(true);}}/></label>
        <label className="block">Vigencia hasta (hora de Santo Domingo)<input type="datetime-local" value={until} disabled={working} onChange={e=>{setUntil(e.target.value);setDirty(true);}}/></label>
        <p>La revisión manual favorable exige evidencia y vigencia hasta el final de la cita. No constituye una autorización recibida de ARS ni determina copago. La revisión desfavorable no autoriza atención particular.</p>
        {conflict&&<p role="status">Comprueba la versión actual antes de volver a registrar una revisión.</p>}
        <div className="flex flex-wrap gap-3">{[["ManualReview","Mantener en revisión"],["Approved","Registrar revisión favorable y confirmar cita"],["Denied","Registrar revisión desfavorable"]].map(([status,label])=><button key={status} type="button" className="button-secondary" disabled={working||conflict||!evidence.trim()||(status==="Approved"&&!until)} onClick={()=>void review(status)}>{label}</button>)}</div>
      </form>:<p>Esta cita ya no admite cambios de revisión.</p>}
      <div className="flex flex-wrap gap-3"><button className="button-secondary" disabled={working} onClick={()=>void open(selected.id,true)}>Recargar cita y descartar borrador</button><button className="button-secondary" disabled={working} onClick={close}>Cerrar detalle</button></div>
    </section>}
  </div>;
}
