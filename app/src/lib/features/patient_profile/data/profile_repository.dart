import '../../../core/network/api_client.dart';

class ProfileCompleteness {
  final String status;
  final List<String> missingFields;
  const ProfileCompleteness(this.status, this.missingFields);
  factory ProfileCompleteness.fromJson(Map data) => ProfileCompleteness(
    data['status'] as String,
    List<String>.unmodifiable((data['missingFields'] as List).cast<String>()),
  );
}

class DeclarationMetadata {
  final String source;
  final DateTime? updatedAt;
  const DeclarationMetadata(this.source, this.updatedAt);
  factory DeclarationMetadata.fromJson(Map data) => DeclarationMetadata(
    data['source'] as String,
    data['updatedAt'] == null
        ? null
        : DateTime.parse(data['updatedAt'] as String).toUtc(),
  );
}

class ProfileSnapshot {
  final JsonMap? profile;
  final int version;
  final Map<String, DeclarationMetadata> metadata;
  final ProfileCompleteness completeness;
  const ProfileSnapshot(
    this.profile,
    this.version,
    this.metadata,
    this.completeness,
  );
}

class ProfileRepository {
  final ApiClient api;
  ProfileRepository(this.api);
  static const fields = [
    'firstName',
    'lastName',
    'birthDate',
    'phone',
    'address',
    'emergencyName',
    'emergencyPhone',
  ];
  static const categories = ['allergies', 'medications', 'conditions'];

  // Copy only editable fields. Identity and server metadata are never submitted.
  static JsonMap draft(JsonMap? profile) => {
    for (final key in fields) key: profile?[key] is String ? profile![key] : '',
    for (final key in categories)
      key: profile?[key] is List
          ? List<String>.from((profile![key] as List).whereType<String>())
          : <String>[],
  };

  Future<ProfileSnapshot> load() async {
    final data = await api.request('GET', '/patients/me') as Map?;
    final completeness = data == null
        ? await api.request('GET', '/patients/me/completeness') as Map
        : data['completeness'] as Map;
    return ProfileSnapshot(
      data == null ? null : Map<String, dynamic>.from(data['profile'] as Map),
      data?['version'] as int? ?? 0,
      {
        for (final key in categories)
          key:
              data != null &&
                  data['declarationMetadata'] is Map &&
                  (data['declarationMetadata'] as Map)[key] is Map
              ? DeclarationMetadata.fromJson(
                  (data['declarationMetadata'] as Map)[key] as Map,
                )
              : const DeclarationMetadata('SelfDeclared', null),
      },
      ProfileCompleteness.fromJson(completeness),
    );
  }

  Future<void> save(JsonMap body, int version) async {
    await api.request(
      'PATCH',
      '/patients/me',
      body: {...draft(body), 'version': version},
    );
  }

  static Map<String, String> fieldErrors(List<String> errors) {
    const messages = {
      'REQUIRED': 'Completa este campo.',
      'INVALID': 'Revisa el formato de este campo.',
      'MAX_LENGTH': 'El texto supera el límite permitido.',
      'MAX_ITEMS': 'Se permiten hasta 20 elementos.',
    };
    final result = <String, String>{};
    for (final error in errors) {
      final parts = error.split(':');
      if (parts.length == 2 &&
          [...fields, ...categories, 'version'].contains(parts[0]) &&
          messages.containsKey(parts[1])) {
        result[parts[0]] = messages[parts[1]]!;
      }
    }
    return result;
  }
}
