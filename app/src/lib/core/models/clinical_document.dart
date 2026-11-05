enum ClinicalDocumentType {
  prescription,
  labOrder,
  imagingOrder,
  referral,
  dischargeSummary,
}

class ClinicalDocument {
  final String id;
  final String appointmentId;
  final ClinicalDocumentType type;
  final String title;
  final String doctorName;
  final String doctorLicense;
  final DateTime issuedAt;
  final DateTime? expiresAt;
  final String summary;
  final String downloadUrl;

  const ClinicalDocument({
    required this.id,
    required this.appointmentId,
    required this.type,
    required this.title,
    required this.doctorName,
    required this.doctorLicense,
    required this.issuedAt,
    this.expiresAt,
    required this.summary,
    required this.downloadUrl,
  });

  String get typeDisplay {
    switch (type) {
      case ClinicalDocumentType.prescription:
        return 'Receta Médica';
      case ClinicalDocumentType.labOrder:
        return 'Orden de Laboratorio';
      case ClinicalDocumentType.imagingOrder:
        return 'Orden de Imágenes';
      case ClinicalDocumentType.referral:
        return 'Referimiento';
      case ClinicalDocumentType.dischargeSummary:
        return 'Resumen Clínico';
    }
  }
}
