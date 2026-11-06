import '../../../core/network/api_client.dart';

class CatalogService {
  final String id, name, specialty;
  final int durationMinutes;
  const CatalogService(this.id, this.name, this.specialty, this.durationMinutes);
  factory CatalogService.fromJson(JsonMap data) => CatalogService(
    data['id'] as String, data['name'] as String, data['specialty'] as String,
    data['durationMinutes'] as int,
  );
  JsonMap toJson() => {'id': id, 'name': name, 'specialty': specialty, 'durationMinutes': durationMinutes};
}

class CatalogPractitioner {
  final String id, name;
  const CatalogPractitioner(this.id, this.name);
  factory CatalogPractitioner.fromJson(JsonMap data) => CatalogPractitioner(data['id'] as String, data['name'] as String);
  JsonMap toJson() => {'id': id, 'name': name};
}

class CatalogSlot {
  final String id, serviceId, practitionerId, practitionerName, serviceName, specialty;
  final DateTime startAt, endAt;
  const CatalogSlot(this.id, this.serviceId, this.practitionerId, this.practitionerName,
    this.serviceName, this.specialty, this.startAt, this.endAt);
  factory CatalogSlot.fromJson(JsonMap data) => CatalogSlot(
    data['id'] as String, data['serviceId'] as String, data['practitionerId'] as String,
    data['practitionerName'] as String, data['serviceName'] as String, data['specialty'] as String,
    DateTime.parse(data['startAt'] as String).toUtc(), DateTime.parse(data['endAt'] as String).toUtc(),
  );
  JsonMap toJson() => {'id': id, 'serviceId': serviceId, 'practitionerId': practitionerId,
    'practitionerName': practitionerName, 'serviceName': serviceName, 'specialty': specialty,
    'startAt': startAt.toIso8601String(), 'endAt': endAt.toIso8601String()};
}

class CatalogOptions {
  final List<String> specialties;
  final List<CatalogService> services;
  const CatalogOptions(this.specialties, this.services);
}

enum CatalogAvailabilityState { notQueried, loading, empty, results, error }

/// Civil dates are interpreted in America/Santo_Domingo, independently of the device zone.
/// The end date is exclusive. The server remains authoritative for slot availability.
class CatalogDateRange {
  final DateTime from, to;
  const CatalogDateRange(this.from, this.to);
  factory CatalogDateRange.fromCivilDates(String from, String to) {
    DateTime civil(String value) {
      final match = RegExp(r'^(\d{4})-(\d{2})-(\d{2})$').firstMatch(value);
      if (match == null) {
        throw ApiFailure(400, 'Usa fechas válidas con formato AAAA-MM-DD.');
      }
      final year = int.parse(match[1]!), month = int.parse(match[2]!), day = int.parse(match[3]!);
      final date = DateTime.utc(year, month, day);
      if (date.year != year || date.month != month || date.day != day) {
        throw ApiFailure(400, 'Usa fechas válidas con formato AAAA-MM-DD.');
      }
      return date.add(const Duration(hours: 4));
    }
    final start = civil(from), end = civil(to);
    if (!end.isAfter(start) || end.difference(start) > const Duration(days: 31)) {
      throw ApiFailure(400, 'El rango debe ser positivo y de hasta 31 días.');
    }
    return CatalogDateRange(start, end);
  }
  static String civilDate(DateTime utc) {
    final local = utc.toUtc().subtract(const Duration(hours: 4));
    String pad(int value) => value.toString().padLeft(2, '0');
    return '${local.year.toString().padLeft(4, '0')}-${pad(local.month)}-${pad(local.day)}';
  }
}

class CatalogRepository {
  final ApiClient api;
  CatalogRepository(this.api);

  Future<List<T>> _list<T>(String path, T Function(JsonMap) parse) async {
    final data = await api.request('GET', path);
    try {
      return List<T>.unmodifiable((data as List).map((item) => parse(Map<String, dynamic>.from(item as Map))));
    } on TypeError {
      throw ApiFailure(502, 'Respuesta de catálogo no válida.');
    } on FormatException {
      throw ApiFailure(502, 'Respuesta de catálogo no válida.');
    }
  }

  Future<CatalogOptions> catalog() async {
    final results = await Future.wait([
      _list<String>('/specialties', (item) => item['name'] as String),
      _list<CatalogService>('/services', CatalogService.fromJson),
    ]);
    return CatalogOptions(results[0] as List<String>, results[1] as List<CatalogService>);
  }

  Future<List<CatalogPractitioner>> practitioners(String serviceId) => _list(
    '/practitioners?${Uri(queryParameters: {'serviceId': serviceId}).query}', CatalogPractitioner.fromJson,
  );

  Future<List<CatalogSlot>> availability({required String serviceId, String? practitionerId,
    required String fromDate, required String toDate}) {
    final range = CatalogDateRange.fromCivilDates(fromDate, toDate);
    final query = Uri(queryParameters: {
      'serviceId': serviceId,
      if (practitionerId != null) 'practitionerId': practitionerId,
      'from': range.from.toIso8601String(), 'to': range.to.toIso8601String(),
    }).query;
    return _list('/availability?$query', CatalogSlot.fromJson);
  }
}
