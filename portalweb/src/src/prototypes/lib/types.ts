export type UserRole = 'Doctor' | 'Admission' | 'FollowUp' | 'Billing' | 'Admin';

export interface UserSession {
  id: string;
  email: string;
  name: string;
  role: UserRole;
  medicalCenter: string;
  specialty?: string;
  licenseNumber?: string;
}

export type AppointmentStatus =
  | 'Requested'
  | 'PendingValidation'
  | 'Confirmed'
  | 'WaitingRoom'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled'
  | 'NoShow';

export type CoverageStatus =
  | 'NotRequired'
  | 'Pending'
  | 'Approved'
  | 'Denied'
  | 'ManualReview'
  | 'Expired';

export interface PatientSummary {
  id: string;
  identificationId: string; // Cédula o pasaporte
  fullName: string;
  dateOfBirth: string;
  gender: 'M' | 'F' | 'O';
  bloodType?: string;
  allergies: string[];
  chronicConditions: string[];
  insuranceProvider: string;
  policyNumber: string;
  phone: string;
}

export interface Appointment {
  id: string;
  patientId: string;
  patientName: string;
  patientIdCard: string;
  doctorName: string;
  specialty: string;
  scheduledAt: string;
  durationMinutes: number;
  status: AppointmentStatus;
  coverageStatus: CoverageStatus;
  reason: string;
  isVirtual: boolean;
  videoRoomUrl?: string;
}

export interface ClinicalNote {
  id: string;
  encounterId: string;
  appointmentId: string;
  patientId: string;
  doctorId: string;
  subjective: string;   // Síntomas y motivo según el paciente
  objective: string;    // Observaciones clínicas, signos vitales
  assessment: string;   // Impresión diagnóstica (CIE-10 sugerido)
  plan: string;         // Indicaciones, tratamiento y seguimiento
  createdAt: string;
  signedAt?: string;
  status: 'Draft' | 'Signed' | 'Closed';
}

export interface PrescriptionItem {
  medication: string;
  dosage: string;
  frequency: string;
  duration: string;
  instructions: string;
}

export interface Prescription {
  id: string;
  appointmentId: string;
  patientId: string;
  patientName: string;
  doctorId: string;
  doctorName: string;
  doctorLicense: string;
  items: PrescriptionItem[];
  issuedAt: string;
  expiresAt: string;
  status: 'Active' | 'Dispensed' | 'Expired';
}

export interface ApiResponse<T> {
  success: boolean;
  status: number;
  detail: string;
  data?: T;
}

export interface ErrorApiResponse {
  success: boolean;
  status: number;
  title: string;
  type: string;
  instance: string;
  detail?: string;
  errors?: string[];
}

