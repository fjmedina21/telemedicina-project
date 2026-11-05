class Patient {
  final String id;
  final String identificationCard; // Cédula dominicana (ej. 001-xxxxxxx-x)
  final String firstName;
  final String lastName;
  final String email;
  final String phone;
  final DateTime dateOfBirth;
  final String gender;
  final String insuranceProvider;
  final String insurancePolicyNumber;
  final List<String> allergies;
  final List<String> chronicConditions;
  final String emergencyContactName;
  final String emergencyContactPhone;

  String get fullName => '$firstName $lastName';

  const Patient({
    required this.id,
    required this.identificationCard,
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.phone,
    required this.dateOfBirth,
    required this.gender,
    required this.insuranceProvider,
    required this.insurancePolicyNumber,
    this.allergies = const [],
    this.chronicConditions = const [],
    required this.emergencyContactName,
    required this.emergencyContactPhone,
  });

  factory Patient.fromJson(Map<String, dynamic> json) {
    return Patient(
      id: json['id'] as String? ?? '',
      identificationCard: json['identificationCard'] as String? ?? '',
      firstName: json['firstName'] as String? ?? '',
      lastName: json['lastName'] as String? ?? '',
      email: json['email'] as String? ?? '',
      phone: json['phone'] as String? ?? '',
      dateOfBirth:
          DateTime.tryParse(json['dateOfBirth'] as String? ?? '') ??
          DateTime(1990, 1, 1),
      gender: json['gender'] as String? ?? 'O',
      insuranceProvider: json['insuranceProvider'] as String? ?? 'Particular',
      insurancePolicyNumber: json['insurancePolicyNumber'] as String? ?? '',
      allergies:
          (json['allergies'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          [],
      chronicConditions:
          (json['chronicConditions'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          [],
      emergencyContactName: json['emergencyContactName'] as String? ?? '',
      emergencyContactPhone: json['emergencyContactPhone'] as String? ?? '',
    );
  }

  Map<String, dynamic> toJson() => {
    'id': id,
    'identificationCard': identificationCard,
    'firstName': firstName,
    'lastName': lastName,
    'email': email,
    'phone': phone,
    'dateOfBirth': dateOfBirth.toIso8601String(),
    'gender': gender,
    'insuranceProvider': insuranceProvider,
    'insurancePolicyNumber': insurancePolicyNumber,
    'allergies': allergies,
    'chronicConditions': chronicConditions,
    'emergencyContactName': emergencyContactName,
    'emergencyContactPhone': emergencyContactPhone,
  };
}
