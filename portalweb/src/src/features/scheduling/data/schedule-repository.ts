import {api,ApiError} from "@/shared/api/client";

export type ScheduleSlot={
  id:string;serviceId:string;serviceName:string;practitionerId:string;practitionerName:string;
  startAt:string;endAt:string;blocked:boolean;reserved:boolean;version:number;
  serviceActive:boolean;practitionerActive:boolean;assignmentActive:boolean;servicePublishable:boolean;
};
export type SchedulePage={items:ScheduleSlot[];totalCount:number;offset:number;limit:number;hasMore:boolean};
export type BooleanFilter=""|"true"|"false";
export type ScheduleFilters={serviceId:string;practitionerId:string;from:string;to:string;
  blocked:BooleanFilter;reserved:BooleanFilter;limit:number};
export function santoDomingoInput(value:Date){
  return new Date(value.getTime()-4*3600000).toISOString().slice(0,16);
}
export function scheduleInstant(value:string){
  if(!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value))
    throw new ApiError(400,"Indique fecha y hora válidas de Santo Domingo.");
  const date=new Date(value+"-04:00");
  if(!Number.isFinite(date.getTime())||santoDomingoInput(date)!==value)
    throw new ApiError(400,"Indique fecha y hora válidas de Santo Domingo.");
  return date.toISOString();
}
export function scheduleQuery(filters:ScheduleFilters,offset:number){
  const from=scheduleInstant(filters.from),to=scheduleInstant(filters.to);
  const span=Date.parse(to)-Date.parse(from);
  if(span<=0||span>31*24*3600000)
    throw new ApiError(400,"El rango debe ser positivo y no superar 31 días.");
  const query=new URLSearchParams({from,to,offset:String(offset),limit:String(filters.limit)});
  if(filters.serviceId)query.set("serviceId",filters.serviceId);
  if(filters.practitionerId)query.set("practitionerId",filters.practitionerId);
  if(filters.blocked!=="")query.set("blocked",filters.blocked);
  if(filters.reserved!=="")query.set("reserved",filters.reserved);
  return query;
}
export const scheduleRepository={
  list:(filters:ScheduleFilters,offset=0)=>api<SchedulePage>("/admin/slots?"+scheduleQuery(filters,offset)),
  create:(serviceId:string,practitionerId:string,start:string)=>api<{id:string;version:number}>(
    "/admin/slots","POST",{serviceId,practitionerId,startAt:scheduleInstant(start)}),
  block:(slot:ScheduleSlot)=>api<{blocked:boolean;version:number}>(
    "/admin/slots/"+slot.id+"/block","PATCH",{blocked:!slot.blocked,version:slot.version}),
};
