import type {Metadata} from "next";
import AppFrame from "@/shared/components/app-frame";
import "./globals.css";
export const metadata:Metadata={title:"Telemedicina · Admisión",description:"Gestiona solicitudes, horarios y cobertura en un mismo espacio."};
export default function RootLayout({children}:Readonly<{children:React.ReactNode}>){
  return <html lang="es"><body><AppFrame>{children}</AppFrame></body></html>;
}
