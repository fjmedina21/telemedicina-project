"use client";
import {useEffect,useRef,useState} from "react";
import {Room,RoomEvent,Track} from "livekit-client";
import {api,ApiError,Item} from "@/shared/api/client";

export default function PractitionerVideoPanel() {
  const [appointmentId,setAppointmentId]=useState(""),[status,setStatus]=useState<Item|null>(null);
  const [error,setError]=useState(""),[busy,setBusy]=useState(false),[joined,setJoined]=useState(false);
  const room=useRef<Room|null>(null),remoteVideo=useRef<HTMLVideoElement>(null),localVideo=useRef<HTMLVideoElement>(null);
  useEffect(()=>()=>{room.current?.disconnect();},[]);
  async function loadStatus() {
    setBusy(true);setError("");
    try{setStatus(await api<Item>(`/appointments/${encodeURIComponent(appointmentId)}/video-status`));}
    catch(e){setError(e instanceof ApiError?e.message:"No se pudo consultar la sesión.");}
    finally{setBusy(false);}
  }
  async function admit() {
    if(!status?.sessionId)return;
    setBusy(true);setError("");
    try{await api(`/video-sessions/${status.sessionId}/admit`,"POST",{});await loadStatus();}
    catch(e){setError(e instanceof ApiError?e.message:"No se pudo admitir la sesión.");}
    finally{setBusy(false);}
  }
  async function join() {
    setBusy(true);setError("");
    try{
      const token=await api<{sessionId:string;token:string;url:string}>(
        `/appointments/${encodeURIComponent(appointmentId)}/video-token`,"POST",{},
        true,{"Idempotency-Key":crypto.randomUUID()});
      const next=new Room();
      room.current=next;
      next.on(RoomEvent.TrackSubscribed,(track)=>{if(track.kind===Track.Kind.Video&&remoteVideo.current)
        track.attach(remoteVideo.current);});
      next.on(RoomEvent.TrackUnsubscribed,(track)=>track.detach());
      await next.connect(token.url,token.token);
      try {
        await next.localParticipant.setCameraEnabled(true);
      } catch (e) {
        const detail=e instanceof Error?`: ${e.message}`:"";
        throw new Error(`La cámara no pudo iniciarse${detail}`);
      }
      try {
        await next.localParticipant.setMicrophoneEnabled(true);
      } catch (e) {
        const detail=e instanceof Error?`: ${e.message}`:"";
        throw new Error(`El micrófono no pudo iniciarse${detail}`);
      }
      if(localVideo.current){
        const publication=next.localParticipant.getTrackPublication(Track.Source.Camera);
        if(publication?.track)publication.track.attach(localVideo.current);
      }
      setJoined(true);
    }catch(e){
      const detail=e instanceof ApiError?e.message:e instanceof Error?e.message:String(e);
      setError(detail?`No se pudo conectar a la sala: ${detail}`:"No se pudo conectar a la sala.");
    }
    finally{setBusy(false);}
  }
  async function end() {
    const id=status?.sessionId;
    if(id)await api(`/video-sessions/${id}/end`,"POST",{});
    room.current?.disconnect();room.current=null;setJoined(false);await loadStatus();
  }
  return <section className="space-y-5">
    <div><span className="eyebrow">VIDEOCONSULTA H07</span><h1>Atender una sala</h1>
      <p className="page-description">Solo usa citas asignadas al profesional autenticado. No se graba la llamada.</p></div>
    <div className="form-stack">
      <label htmlFor="appointment-id">ID de cita</label>
      <input id="appointment-id" value={appointmentId} onChange={e=>setAppointmentId(e.target.value)}
        placeholder="UUID de la cita" disabled={busy||joined}/>
      <div className="button-row">
        <button className="button-secondary" disabled={busy||!appointmentId||joined} onClick={loadStatus}>Consultar sala</button>
        {status?.status==="Waiting"&&<button className="button-primary" disabled={busy} onClick={admit}>Admitir paciente</button>}
        {status?.status==="Active"&&!joined&&<button className="button-primary" disabled={busy} onClick={join}>Entrar a la sala</button>}
        {joined&&<button className="button-danger" onClick={end}>Finalizar sesión</button>}
      </div>
    </div>
    {error&&<p role="alert" className="notice error-notice">{error}</p>}
    {status&&<p role="status">Estado: <strong>{String(status.status)}</strong> · Participantes: {String(status.connectedParticipants??0)}</p>}
    {joined&&<div className="grid grid-cols-2 gap-3">
      <video ref={localVideo} autoPlay muted playsInline className="video-tile"/>
      <video ref={remoteVideo} autoPlay playsInline className="video-tile" aria-label="Video del paciente"/>
    </div>}
  </section>;
}
