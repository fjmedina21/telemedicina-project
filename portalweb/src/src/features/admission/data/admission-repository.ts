import {api,Item} from "@/shared/api/client";
export type QueueFilters={from:string;to:string;status:string;coverageStatus:string};
export type Appointment=Item&{id:string;version:number;startAt:string;endAt:string;status:string;coverageStatus:string;coverageMethod:"Manual";coverageRequiresReview:boolean;coverageValidityReason:string|null};
export type HistoryEntry={sequence:number;oldStatus:string|null;newStatus:string;oldCoverage:string|null;newCoverage:string;version:number;occurredAt:string};
export const emptyFilters:QueueFilters={from:"",to:"",status:"",coverageStatus:""};
export function queuePath(filters:QueueFilters){
  const query=new URLSearchParams();
  if(Boolean(filters.from)!==Boolean(filters.to))throw new Error("Indica ambas fechas para consultar el intervalo.");
  if(filters.from&&filters.to){
    const from=new Date(filters.from+"-04:00"),to=new Date(filters.to+"-04:00");
    if(!Number.isFinite(from.getTime())||!Number.isFinite(to.getTime())||to<=from||to.getTime()-from.getTime()>366*86400000)
      throw new Error("El intervalo debe ser positivo y de hasta 366 días.");
    query.set("from",from.toISOString());query.set("to",to.toISOString());
  }
  if(filters.status)query.set("status",filters.status);
  if(filters.coverageStatus)query.set("coverageStatus",filters.coverageStatus);
  return "/admin/work-queue"+(query.size?"?"+query:"");
}
export const admissionRepository={
  list:(filters:QueueFilters)=>api<Appointment[]>(queuePath(filters)),
  detail:(id:string)=>api<Appointment>("/appointments/"+encodeURIComponent(id)),
  history:(id:string)=>api<HistoryEntry[]>("/appointments/"+encodeURIComponent(id)+"/history"),
  review:(id:string,body:{status:string;evidence:string;version:number;validUntil:string|null})=>
    api("/admin/appointments/"+encodeURIComponent(id)+"/validation","PATCH",body),
};
const coverageLabels:Record<string,string>={Approved:"Revisión manual favorable",Denied:"Revisión manual desfavorable",Expired:"Revisión manual vencida",ManualReview:"En revisión manual",Pending:"Revisión pendiente"};
export const coverageLabel=(value:unknown)=>coverageLabels[String(value)]??String(value??"Sin revisión");
const validityLabels:Record<string,string>={MissingValidity:"Falta vigencia de la revisión.",Expired:"La revisión manual venció; requiere nueva revisión.",OutsideAppointment:"La vigencia no cubre el final de la cita."};
export const validityLabel=(reason:string|null)=>validityLabels[reason??""]??"";
