"use client";
import {useState} from "react";
import {Item} from "@/shared/api/client";

type Props={services:Item[];practitioners:Item[];busy:boolean;
  onSave:(path:string,method:string,body:unknown)=>Promise<boolean>};
const blankService={id:"",name:"",specialty:"",durationMinutes:30,active:false,isSynthetic:true,approvalReference:"",version:0};
const blankPractitioner={id:"",name:"",licenseRef:"",active:false,serviceIds:[] as string[],version:0};

export default function CatalogPanel({services,practitioners,busy,onSave}:Props){
  const [service,setService]=useState(blankService);
  const [person,setPerson]=useState(blankPractitioner);
  return <div className="grid gap-6 xl:grid-cols-2">
    <section className="space-y-4 rounded-xl border bg-white p-5">
      <h2 className="text-lg font-bold">Servicios</h2>
      <p>Active solo servicios habilitados por el centro. La marca sintética identifica datos de demostración.</p>
      <form className="space-y-3" onSubmit={async event=>{
        event.preventDefault();
        if(await onSave("/admin/catalog/services"+(service.id?"/"+service.id:""),
          service.id?"PATCH":"POST",service)) setService(blankService);
      }}>
        <label className="block" htmlFor="service-name">Nombre del servicio</label>
        <input id="service-name" className="w-full rounded border p-2" required maxLength={120} value={service.name}
          onChange={e=>setService({...service,name:e.target.value})}/>
        <label className="block" htmlFor="specialty">Especialidad</label>
        <input id="specialty" className="w-full rounded border p-2" required maxLength={100} value={service.specialty}
          onChange={e=>setService({...service,specialty:e.target.value})}/>
        <label className="block" htmlFor="duration">Duración en minutos</label>
        <input id="duration" className="rounded border p-2" type="number" min={5} max={240} required
          value={service.durationMinutes} onChange={e=>setService({...service,durationMinutes:Number(e.target.value)})}/>
        <label className="block"><input type="checkbox" checked={service.isSynthetic}
          onChange={e=>setService({...service,isSynthetic:e.target.checked})}/> Servicio sintético</label>
        <label className="block"><input type="checkbox" checked={service.active}
          onChange={e=>setService({...service,active:e.target.checked})}/> Servicio activo</label>
        <label className="block" htmlFor="approval">Referencia de habilitación del centro</label>
        <input id="approval" className="w-full rounded border p-2" maxLength={250}
          required={service.active&&!service.isSynthetic} value={service.approvalReference}
          onChange={e=>setService({...service,approvalReference:e.target.value})}/>
        <div className="flex gap-3">
          <button disabled={busy} className="rounded bg-teal-700 p-2 text-white">Guardar servicio</button>
          <button disabled={busy} type="button" onClick={()=>setService(blankService)} className="rounded border p-2">Nuevo servicio</button>
        </div>
      </form>
      <ul className="space-y-2">{services.map(item=><li key={String(item.id)} className="flex flex-wrap justify-between gap-2 rounded border p-3">
        <span>{String(item.name)} · {item.active?"Activo":"Inactivo"} · {String(item.durationMinutes)} min</span>
        <button disabled={busy} className="underline" onClick={()=>setService({
          id:String(item.id),name:String(item.name),specialty:String(item.specialty),
          durationMinutes:Number(item.durationMinutes),active:Boolean(item.active),isSynthetic:Boolean(item.isSynthetic),
          approvalReference:String(item.approvalReference??""),version:Number(item.version)
        })}>Editar servicio</button>
      </li>)}</ul>
    </section>
    <section className="space-y-4 rounded-xl border bg-white p-5">
      <h2 className="text-lg font-bold">Profesionales</h2>
      <p>La referencia profesional es un dato administrativo; su registro no constituye validación automática de credenciales.</p>
      <form className="space-y-3" onSubmit={async event=>{
        event.preventDefault();
        if(await onSave("/admin/catalog/practitioners"+(person.id?"/"+person.id:""),
          person.id?"PATCH":"POST",person)) setPerson(blankPractitioner);
      }}>
        <label className="block" htmlFor="person-name">Nombre del profesional</label>
        <input id="person-name" className="w-full rounded border p-2" required maxLength={120} value={person.name}
          onChange={e=>setPerson({...person,name:e.target.value})}/>
        <label className="block" htmlFor="license">Referencia profesional</label>
        <input id="license" className="w-full rounded border p-2" maxLength={100} value={person.licenseRef}
          onChange={e=>setPerson({...person,licenseRef:e.target.value})}/>
        <label className="block"><input type="checkbox" checked={person.active}
          onChange={e=>setPerson({...person,active:e.target.checked})}/> Profesional activo</label>
        <fieldset className="space-y-2 rounded border p-3"><legend>Servicios asignados</legend>
          {services.length===0&&<p>Cree primero un servicio.</p>}
          {services.map(item=><label className="block" key={String(item.id)}>
            <input type="checkbox" checked={person.serviceIds.includes(String(item.id))}
              onChange={e=>setPerson({...person,serviceIds:e.target.checked?
                [...person.serviceIds,String(item.id)]:person.serviceIds.filter(id=>id!==item.id)})}/>
            {" "+String(item.name)}
          </label>)}
        </fieldset>
        <div className="flex gap-3">
          <button disabled={busy} className="rounded bg-teal-700 p-2 text-white">Guardar profesional</button>
          <button disabled={busy} type="button" onClick={()=>setPerson(blankPractitioner)} className="rounded border p-2">Nuevo profesional</button>
        </div>
      </form>
      <ul className="space-y-2">{practitioners.map(item=><li key={String(item.id)}
        className="flex flex-wrap justify-between gap-2 rounded border p-3">
        <span>{String(item.name)} · {item.active?"Activo":"Inactivo"}</span>
        <button disabled={busy} className="underline" onClick={()=>setPerson({
          id:String(item.id),name:String(item.name),licenseRef:String(item.licenseRef??""),
          active:Boolean(item.active),version:Number(item.version),
          serviceIds:Array.isArray(item.serviceIds)?item.serviceIds.map(String):[]
        })}>Editar profesional</button>
      </li>)}</ul>
    </section>
  </div>;
}
