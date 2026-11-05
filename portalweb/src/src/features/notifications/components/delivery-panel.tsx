import StatusBadge from "@/shared/components/status-badge";
import type {Item} from "@/shared/api/client";

export default function DeliveryPanel({items}:{items:Item[]}){
  return (<div className="surface overflow-x-auto"><table className="data-table">
      <thead><tr>{["Evento","Estado","Intentos","Último código"].map(name=><th key={name} className="p-2">{name}</th>)}</tr></thead>
      <tbody>{items.map(item=><tr key={String(item.id)} className="border-t">
        <td className="p-2">{String(item.kind)}</td><td className="p-2"><StatusBadge value={item.state}/></td>
        <td className="p-2">{String(item.attempts)}</td><td className="p-2">{String(item.lastErrorCode??"—")}</td>
      </tr>)}</tbody>
    </table><p className="notice">Un mensaje «Sin confirmar» tiene una aceptación de envío incierta y no se reenvía automáticamente.</p></div>);
}
