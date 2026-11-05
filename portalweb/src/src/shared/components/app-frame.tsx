"use client";
import Link from "next/link";
import {useEffect,useState} from "react";
import {api} from "@/shared/api/client";
import type {Identity} from "@/shared/api/identity";
import {usePathname} from "next/navigation";
import {CalendarDays,ClipboardList,Layers3,Mail,ShieldCheck,ArrowUpRight} from "lucide-react";
import Brand from "./brand";
const navigation=[
  {permission:"admission.manage",href:"/admision",label:"Admisión",icon:ClipboardList},
  {permission:"schedule.manage",href:"/agenda",label:"Horarios",icon:CalendarDays},
  {permission:"catalog.manage",href:"/catalogo",label:"Catálogo",icon:Layers3},
  {permission:"delivery.read",href:"/entregas",label:"Entregas de correo",icon:Mail},
  {permission:"audit.read",href:"/admin",label:"Auditoría",icon:ShieldCheck},
  {permission:"staff.read",href:"/personal",label:"Cuentas de personal",icon:ShieldCheck},
  {permission:"identity.self",href:"/cuenta",label:"Mi cuenta",icon:ShieldCheck},
];
export default function AppFrame({children}:{children:React.ReactNode}) {
  const path=usePathname();
  const login=path==="/login" || path==="/activar-personal";
  const [identity,setIdentity]=useState<Identity|null>(null);
  useEffect(()=>{
    let current=true;
    const clear=()=>setIdentity(null);
    window.addEventListener("tm-session-invalidated",clear);
    setIdentity(null);
    if(!login) api<Identity>("/auth/me").then(me=>{if(current)setIdentity(me);}).catch(()=>{});
    return ()=>{current=false;window.removeEventListener("tm-session-invalidated",clear);};
  },[path,login]);
  return <div className={login?"auth-frame":"portal-frame"}>
    <a className="skip-link" href="#contenido">Ir al contenido</a>
    {login?<header className="auth-header"><Link href="/login" aria-label="Telemedicina · Acceso"><Brand/></Link><span className="environment-tag">Entorno de demostración</span></header>:
      <aside className="sidebar">
        <Link href="/" className="brand-link" aria-label="Telemedicina · Inicio"><Brand/></Link>
        <p className="nav-caption">ESPACIO DE TRABAJO</p>
        <nav aria-label="Navegación principal">{navigation.filter(item=>identity?.permissions?.includes(item.permission)).map(({href,label,icon:Icon})=>{
          const active=path===href||(href==="/admision"&&path==="/");
          return <Link key={href} href={href} className={active?"nav-link active":"nav-link"} aria-current={active?"page":undefined}>
            <Icon size={19} aria-hidden="true"/><span>{label}</span></Link>;
        })}</nav>
        <div className="sidebar-bottom"><span className="environment-tag">Datos sintéticos</span>
          <p>Un espacio para organizar cada solicitud.</p><Link href="/login" className="access-link">Acceso al portal <ArrowUpRight size={16} aria-hidden="true"/></Link></div>
      </aside>}
    <div className="workspace">
      {!login&&<header className="workspace-bar"><span>Portal de telemedicina</span><span className="timezone">Hora de Santo Domingo · UTC−4</span></header>}
      <main id="contenido" tabIndex={-1}>{children}</main>
      <footer className="workspace-footer">Entorno de demostración · Sin atención clínica, video ni documentos habilitados.</footer>
    </div>
  </div>;
}
