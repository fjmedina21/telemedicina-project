"use client";
import {useEffect,useState} from "react";
import Link from "next/link";
import {useRouter} from "next/navigation";
import {api,ApiError} from "@/shared/api/client";
import {homeFor,type Identity} from "@/shared/api/identity";

export default function AccountPanel() {
  const router=useRouter();
  const [me,setMe]=useState<Identity|null>(null),[error,setError]=useState(""),[busy,setBusy]=useState(true);
  useEffect(()=>{
    let current=true;
    const clear=()=>{setMe(null);router.replace("/login");};
    window.addEventListener("tm-session-invalidated",clear);
    api<Identity>("/auth/me").then(value=>{if(current)setMe(value);}).catch(error=>{
      if(!current)return;
      if(error instanceof ApiError&&error.status===401)clear();
      else setError("No se pudo consultar la identidad.");
    }).finally(()=>{if(current)setBusy(false);});
    return ()=>{current=false;window.removeEventListener("tm-session-invalidated",clear);};
  },[router]);
  if(!me)return <p role="status">{busy?"Verificando sesión…":error||"Se requiere iniciar sesión."}</p>;
  return <section className="space-y-4">
    <h1>Mi cuenta</h1><p>{me.email}</p>
    <p>{["Practitioner","Support"].includes(me.role)?"Sin funciones operativas habilitadas.":"Cuenta de personal autenticada."}</p>
    {homeFor(me.role)!=="/cuenta"&&<Link className="button-primary" href={homeFor(me.role)}>Ir a mi espacio</Link>}
    <button className="button-secondary" disabled={busy} onClick={async()=>{
      setBusy(true);setError("");
      try{await api("/auth/logout","POST",{});setMe(null);router.replace("/login");}
      catch(error){setError(error instanceof Error?error.message:"No se pudo cerrar sesión.");}
      finally{setBusy(false);}
    }}>Salir</button>
    {error&&<p role="alert">{error}</p>}
  </section>;
}
