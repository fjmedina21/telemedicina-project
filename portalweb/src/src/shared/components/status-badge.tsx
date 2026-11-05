const states:Record<string,[string,string]>={
  PendingValidation:["Pendiente","warning"],Confirmed:["Confirmada","success"],Cancelled:["Cancelada","neutral"],Rejected:["Rechazada","danger"],
  Pending:["Pendiente","warning"],ManualReview:["En revisión","warning"],Approved:["Aprobada","success"],Denied:["Denegada","danger"],Expired:["Vencida","neutral"],
  Queued:["En cola","neutral"],Sending:["Enviando","info"],Sent:["Enviado","success"],Captured:["Capturado","info"],Failed:["Fallido","danger"],Unknown:["Sin confirmar","warning"],Superseded:["Sustituido","neutral"],
};
export default function StatusBadge({value}:{value:unknown}) {
  const raw=String(value??"");const [label,tone]=states[raw]??[raw||"Sin estado","neutral"];
  return <span className={"status-pill status-"+tone}>{label}</span>;
}
