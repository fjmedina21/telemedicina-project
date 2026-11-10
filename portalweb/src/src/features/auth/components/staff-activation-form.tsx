"use client";
import {useState} from "react";
import Link from "next/link";
import {api} from "@/shared/api/client";

export default function StaffActivationForm(){
  const [token,setToken]=useState(""),[password,setPassword]=useState("");
  const [busy,setBusy]=useState(false),[message,setMessage]=useState(""),[done,setDone]=useState(false);
  return <section className="login-card" aria-busy={busy}>
    <h1>Activar cuenta de personal</h1>
    <p>Utiliza el código de invitación recibido por correo. Caduca en 15 minutos y se utiliza una sola vez.</p>
    {!done&&<form className="space-y-4" autoComplete="off" onSubmit={async event=>{
      event.preventDefault();setBusy(true);setMessage("");
      try{
        await api("/auth/staff/activate","POST",{token:token.trim(),password});
        setDone(true);setMessage("Cuenta activada. Ya puedes iniciar sesión.");
      }catch(error){setMessage(error instanceof Error?error.message:"No se pudo activar la cuenta.");}
      finally{setToken("");setPassword("");setBusy(false);}
    }}>
      <label className="block">Invitación<input className="block w-full rounded border p-2" type="password"
        autoComplete="off" required pattern="[A-Fa-f0-9]{64}" maxLength={64} value={token}
        disabled={busy} onChange={event=>setToken(event.target.value)}/></label>
      <label className="block">Nueva contraseña<input className="block w-full rounded border p-2" type="password"
        autoComplete="new-password" required minLength={12} maxLength={128} value={password}
        disabled={busy} onChange={event=>setPassword(event.target.value)}/></label>
      <button className="button-primary" disabled={busy}>{busy?"Activando…":"Activar cuenta"}</button>
    </form>}
    <p role="status" aria-live="polite">{message}</p><Link href="/login">Ir al acceso</Link>
  </section>;
}
