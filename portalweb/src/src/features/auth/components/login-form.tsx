"use client";
import {useState} from "react";
import {useRouter} from "next/navigation";
import {api} from "@/shared/api/client";

export default function LoginForm() {
  const router=useRouter();
  const [email,setEmail]=useState(""),[password,setPassword]=useState("");
  const [busy,setBusy]=useState(false),[error,setError]=useState("");
  return <div className="login-grid"><aside className="login-story">
    <span className="eyebrow">CADA SOLICITUD, EN SU LUGAR</span>
    <h2>Más claridad.<br/>Mejor organización.</h2>
    <p>Solicitudes, horarios y cobertura en un espacio pensado para tu equipo.</p>
    <div className="login-note">La confirmación de citas y la cobertura requieren revisión de admisión.</div>
  </aside><section className="login-card"><span className="eyebrow">BIENVENIDO AL PORTAL</span>
    <h1 >Acceso de admisión</h1>
    <p>Ingresa con tu cuenta de admisión.</p>
    <form className="space-y-4" onSubmit={async event=>{
      event.preventDefault(); setBusy(true); setError("");
      try {
        const user=await api<{role:string}>("/auth/login","POST",{email,password});
        if(user.role!=="Admission") {
          await api("/auth/logout","POST",{});
          throw new Error("Esta cuenta no tiene acceso al portal de admisión.");
        }
        setPassword(""); router.replace("/admision");
      } catch(e) {setError(e instanceof Error ? e.message : "Error de acceso");}
      finally {setBusy(false);}
    }}>
      <label className="block">Correo<input className="block w-full rounded border p-2"
        type="email" autoComplete="username" required disabled={busy} value={email} onChange={e=>setEmail(e.target.value)}/></label>
      <label className="block">Contraseña<input className="block w-full rounded border p-2"
        type="password" autoComplete="current-password" required disabled={busy} value={password} onChange={e=>setPassword(e.target.value)}/></label>
      <button disabled={busy} className="button-primary">
        {busy?"Verificando…":"Entrar"}</button>
      <p role="alert" aria-live="polite">{error}</p>
    </form>
  </section></div>;
}
