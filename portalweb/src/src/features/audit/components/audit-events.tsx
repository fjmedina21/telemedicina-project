"use client";
import {useEffect,useState} from "react";
import {api,Item} from "@/shared/api/client";
import {localTime} from "@/shared/formatting/date-time";
import type {OperationRunner} from "@/shared/api/operation";

type Props={items:Item[];busy:boolean;run:OperationRunner;onItemsChange:(items:Item[])=>void};
export default function AuditEvents({items,busy,run,onItemsChange:setItems}:Props){
  return <><p>Primeros 100 eventos. No se muestran contraseñas, tokens ni contenido clínico.</p>
      <ol className="space-y-2">{items.map(a=><li key={String(a.id)} className="rounded border bg-white p-3">
        {localTime(a.occurredAt)} · {String(a.action)} · Actor {String(a.actorId)} · Recurso {String(a.resourceId)}
      </li>)}</ol>
      {items.length>0 && <button disabled={busy} className="underline" onClick={()=>run(async()=>{
        const next=await api<Item[]>("/admin/audit-events?after="+items[items.length-1].sequence);
        setItems(next);
      })}>Siguiente página</button>}</>;
}
