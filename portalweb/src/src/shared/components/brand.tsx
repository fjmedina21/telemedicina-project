export default function Brand({subtitle="Portal de admisión"}:{subtitle?:string}) {
  return <span className="brand">
    <span className="brand-mark" aria-hidden="true"><svg viewBox="0 0 32 32" fill="none"><path d="M16 8v16M8 16h16" stroke="currentColor" strokeWidth="5" strokeLinecap="round"/></svg></span>
    <span><strong>telemedicina</strong><small>{subtitle}</small></span>
  </span>;
}
