import { Appointment, PatientSummary, UserSession, ClinicalNote, Prescription } from './types';

export const MOCK_USERS: Record<string, UserSession> = {
  doctor: {
    id: "usr-doc-001",
    email: "dra.mendez@hospital-ejemplo.do",
    name: "Dra. Carmen Méndez",
    role: "Doctor",
    medicalCenter: "Centro Médico Metropolitano",
    specialty: "Medicina Interna",
    licenseNumber: "EXQ-2024-8841"
  },
  admission: {
    id: "usr-adm-002",
    email: "admision@hospital-ejemplo.do",
    name: "Lic. Roberto Santana",
    role: "Admission",
    medicalCenter: "Centro Médico Metropolitano"
  },
  admin: {
    id: "usr-adm-003",
    email: "admin@hospital-ejemplo.do",
    name: "Ing. Laura Fernández",
    role: "Admin",
    medicalCenter: "Centro Médico Metropolitano"
  }
};

export const MOCK_PATIENTS: PatientSummary[] = [
  {
    id: "pat-001",
    identificationId: "001-1827364-5",
    fullName: "Juan Carlos Pérez Santos",
    dateOfBirth: "1988-06-15",
    gender: "M",
    bloodType: "O+",
    allergies: ["Penicilina", "AINES"],
    chronicConditions: ["Hipertensión arterial Grado 1"],
    insuranceProvider: "ARS Humano",
    policyNumber: "POL-7782910-A",
    phone: "(809) 555-0142"
  },
  {
    id: "pat-002",
    identificationId: "402-2938471-2",
    fullName: "María Elena Gómez Durán",
    dateOfBirth: "1995-11-23",
    gender: "F",
    bloodType: "A+",
    allergies: ["Ninguna conocida"],
    chronicConditions: ["Asma bronquial intermitente"],
    insuranceProvider: "ARS Palic",
    policyNumber: "PAL-8839201-B",
    phone: "(829) 555-0199"
  },
  {
    id: "pat-003",
    identificationId: "001-0987654-3",
    fullName: "José Ramón Alcántara",
    dateOfBirth: "1972-03-04",
    gender: "M",
    bloodType: "B+",
    allergies: ["Sulfas"],
    chronicConditions: ["Diabetes Mellitus Tipo 2"],
    insuranceProvider: "Seguro Particular",
    policyNumber: "N/A",
    phone: "(849) 555-0112"
  }
];

export const MOCK_APPOINTMENTS: Appointment[] = [
  {
    id: "apt-101",
    patientId: "pat-001",
    patientName: "Juan Carlos Pérez Santos",
    patientIdCard: "001-1827364-5",
    doctorName: "Dra. Carmen Méndez",
    specialty: "Medicina Interna",
    scheduledAt: "2026-11-04T20:00:00Z",
    durationMinutes: 30,
    status: "WaitingRoom",
    coverageStatus: "Approved",
    reason: "Control mensual de presión arterial y ajuste de dosis",
    isVirtual: true,
    videoRoomUrl: "https://telemed.local/call/apt-101"
  },
  {
    id: "apt-102",
    patientId: "pat-002",
    patientName: "María Elena Gómez Durán",
    patientIdCard: "402-2938471-2",
    doctorName: "Dra. Carmen Méndez",
    specialty: "Medicina Interna",
    scheduledAt: "2026-11-04T20:45:00Z",
    durationMinutes: 30,
    status: "Confirmed",
    coverageStatus: "ManualReview",
    reason: "Revisión de espirometría y síntomas asmáticos nocturnos",
    isVirtual: true,
    videoRoomUrl: "https://telemed.local/call/apt-102"
  },
  {
    id: "apt-103",
    patientId: "pat-003",
    patientName: "José Ramón Alcántara",
    patientIdCard: "001-0987654-3",
    doctorName: "Dra. Carmen Méndez",
    specialty: "Medicina Interna",
    scheduledAt: "2026-11-04T21:30:00Z",
    durationMinutes: 30,
    status: "PendingValidation",
    coverageStatus: "Pending",
    reason: "Evaluación de glucemia en ayunas elevada",
    isVirtual: true
  }
];

export const MOCK_CLINICAL_NOTES: ClinicalNote[] = [
  {
    id: "not-501",
    encounterId: "enc-901",
    appointmentId: "apt-100",
    patientId: "pat-001",
    doctorId: "usr-doc-001",
    subjective: "Paciente masculino de 38 años refiere cefalea occipital leve ocasional. Reporta buena adherencia al tratamiento antihipertensivo sin efectos adversos.",
    objective: "PA tomada en domicilio: 128/82 mmHg. FC: 72 lpm. Sin datos de alarma vascular ni edema en miembros inferiores.",
    assessment: "Hipertensión arterial esencial controlada (CIE-10: I10).",
    plan: "Continuar Losartán 50mg VO cada 24 horas. Dieta hiposódica, ejercicio aeróbico 150 min/semana. Control en 3 meses con perfil lipídico y creatinina.",
    createdAt: "2026-10-15T14:30:00Z",
    signedAt: "2026-10-15T14:45:00Z",
    status: "Signed"
  }
];

export const MOCK_PRESCRIPTIONS: Prescription[] = [
  {
    id: "rx-301",
    appointmentId: "apt-100",
    patientId: "pat-001",
    patientName: "Juan Carlos Pérez Santos",
    doctorId: "usr-doc-001",
    doctorName: "Dra. Carmen Méndez",
    doctorLicense: "EXQ-2024-8841",
    items: [
      {
        medication: "Losartán Potásico 50 mg",
        dosage: "1 tableta",
        frequency: "Cada 24 horas (en la mañana)",
        duration: "90 días",
        instructions: "Tomar con abundante agua en ayunas. Mantener control de PA matutina."
      }
    ],
    issuedAt: "2026-10-15T14:40:00Z",
    expiresAt: "2027-01-15T14:40:00Z",
    status: "Active"
  }
];

