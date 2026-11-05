enum AppointmentStatus {
  requested,
  pendingValidation,
  confirmed,
  waitingRoom,
  inProgress,
  completed,
  cancelled,
  noShow,
}

enum CoverageStatus {
  notRequired,
  pending,
  approved,
  denied,
  manualReview,
  expired,
}

class Appointment {
  final String id;
  final String doctorName;
  final String specialty;
  final String medicalCenter;
  final DateTime scheduledAt;
  final int durationMinutes;
  final AppointmentStatus status;
  final CoverageStatus coverageStatus;
  final String reason;
  final bool isVirtual;
  final String? videoRoomUrl;
  final bool isConsentAccepted;

  const Appointment({
    required this.id,
    required this.doctorName,
    required this.specialty,
    required this.medicalCenter,
    required this.scheduledAt,
    this.durationMinutes = 30,
    required this.status,
    required this.coverageStatus,
    required this.reason,
    this.isVirtual = true,
    this.videoRoomUrl,
    this.isConsentAccepted = false,
  });

  bool get canCheckIn =>
      status == AppointmentStatus.confirmed ||
      status == AppointmentStatus.waitingRoom;

  String get statusDisplay {
    switch (status) {
      case AppointmentStatus.requested:
        return 'Solicitada';
      case AppointmentStatus.pendingValidation:
        return 'Validando seguro';
      case AppointmentStatus.confirmed:
        return 'Confirmada';
      case AppointmentStatus.waitingRoom:
        return 'En sala de espera';
      case AppointmentStatus.inProgress:
        return 'En consulta';
      case AppointmentStatus.completed:
        return 'Completada';
      case AppointmentStatus.cancelled:
        return 'Cancelada';
      case AppointmentStatus.noShow:
        return 'No asistió';
    }
  }

  String get coverageDisplay {
    switch (coverageStatus) {
      case CoverageStatus.approved:
        return 'Cobertura Aprobada';
      case CoverageStatus.manualReview:
        return 'Revisión en Auditoría';
      case CoverageStatus.pending:
        return 'Verificación Pendiente';
      case CoverageStatus.denied:
        return 'No Cubierto (Particular)';
      case CoverageStatus.notRequired:
        return 'Atención Particular';
      case CoverageStatus.expired:
        return 'Autorización Vencida';
    }
  }

  factory Appointment.fromJson(Map<String, dynamic> json) {
    return Appointment(
      id: json['id'] as String? ?? '',
      doctorName: json['doctorName'] as String? ?? '',
      specialty: json['specialty'] as String? ?? '',
      medicalCenter:
          json['medicalCenter'] as String? ?? 'Centro Médico Metropolitano',
      scheduledAt:
          DateTime.tryParse(json['scheduledAt'] as String? ?? '') ??
          DateTime.now(),
      durationMinutes: json['durationMinutes'] as int? ?? 30,
      status: AppointmentStatus.values.firstWhere(
        (e) =>
            e.name.toLowerCase() ==
            (json['status'] as String? ?? '').toLowerCase(),
        orElse: () => AppointmentStatus.confirmed,
      ),
      coverageStatus: CoverageStatus.values.firstWhere(
        (e) =>
            e.name.toLowerCase() ==
            (json['coverageStatus'] as String? ?? '').toLowerCase(),
        orElse: () => CoverageStatus.approved,
      ),
      reason: json['reason'] as String? ?? '',
      isVirtual: json['isVirtual'] as bool? ?? true,
      videoRoomUrl: json['videoRoomUrl'] as String?,
      isConsentAccepted: json['isConsentAccepted'] as bool? ?? false,
    );
  }
}
