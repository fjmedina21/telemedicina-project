import 'dart:convert';
import '../../../core/network/api_client.dart';

/// Administrative coverage only; never implies an ARS authorization.
String manualCoverageLabel(String status) => switch (status) {
  'Approved' => 'Revisión manual favorable',
  'Denied' => 'Revisión manual desfavorable',
  'Expired' => 'Revisión manual vencida',
  'Pending' => 'Revisión manual pendiente',
  'ManualReview' => 'En revisión manual',
  _ => status,
};

class AppointmentRecord {
  final JsonMap _data;
  AppointmentRecord.fromJson(Map value) : _data = Map<String, dynamic>.unmodifiable(Map<String, dynamic>.from(value));
  String get id => _data['id'] as String;
  int get version => _data['version'] as int;
  String get coverageStatus => _data['coverageStatus'] as String;
  JsonMap toJson() => Map<String, dynamic>.from(_data);
}
class AppointmentHistoryEntry {
  final JsonMap _data;
  AppointmentHistoryEntry.fromJson(Map value) : _data = Map<String, dynamic>.unmodifiable(Map<String, dynamic>.from(value));
  JsonMap toJson() => Map<String, dynamic>.from(_data);
}
class AppointmentSnapshot {
  final AppointmentRecord appointment;
  final List<AppointmentHistoryEntry> history;
  final CheckInRequirements checkInRequirements;
  const AppointmentSnapshot(this.appointment, this.history, this.checkInRequirements);
}
class CheckInRequirements {
  final String status;
  final bool canProceed;
  final int configurationVersion, version;
  final String windowOpenAt, windowCloseAt;
  final bool prepared, privacyConfirmed, connectivityConfirmed;
  final bool technicalPreflightCompleted;
  final JsonMap consent;
  final List<String> blockers;
  const CheckInRequirements({required this.status, required this.canProceed,
    required this.configurationVersion, required this.version,
    required this.windowOpenAt, required this.windowCloseAt,
    required this.prepared, required this.privacyConfirmed,
    required this.connectivityConfirmed,
    required this.technicalPreflightCompleted, required this.consent,
    required this.blockers});
  factory CheckInRequirements.fromJson(Map value) => CheckInRequirements(
    status: value['status'] as String,
    canProceed: value['canProceed'] as bool,
    configurationVersion: value['configurationVersion'] as int,
    version: value['version'] as int,
    windowOpenAt: value['windowOpenAt'] as String,
    windowCloseAt: value['windowCloseAt'] as String,
    prepared: value['prepared'] as bool,
    privacyConfirmed: value['privacyConfirmed'] as bool,
    connectivityConfirmed: value['connectivityConfirmed'] as bool,
    technicalPreflightCompleted: value['technicalPreflightCompleted'] as bool,
    consent: Map<String, dynamic>.unmodifiable(
      Map<String, dynamic>.from(value['consent'] as Map)),
    blockers: List<String>.unmodifiable(
      (value['blockers'] as List).whereType<String>()),
  );
  bool get ready => status == 'Ready';
}
class AppointmentFilters {
  final String fromDate, toDate;
  final String? status, coverageStatus;
  const AppointmentFilters({this.fromDate = '', this.toDate = '', this.status, this.coverageStatus});
  static const statuses = ['PendingValidation', 'Confirmed', 'Cancelled', 'Rejected'];
  static const coverages = ['Pending', 'ManualReview', 'Approved', 'Denied', 'Expired'];
  Map<String, String> query() {
    if (status != null && !statuses.contains(status) || coverageStatus != null && !coverages.contains(coverageStatus)) {
      throw ApiFailure(400, 'Selecciona estados válidos.');
    }
    final result = <String, String>{if (status != null) 'status': status!, if (coverageStatus != null) 'coverageStatus': coverageStatus!};
    if (fromDate.isNotEmpty || toDate.isNotEmpty) {
      DateTime parse(String value) {
        if (!RegExp(r'^\d{4}-\d{2}-\d{2}$').hasMatch(value)) throw ApiFailure(400, 'Indica ambas fechas como AAAA-MM-DD.');
        final day = DateTime.tryParse('${value}T00:00:00-04:00');
        if (day == null || day.subtract(const Duration(hours: 4)).toIso8601String().substring(0, 10) != value) {
          throw ApiFailure(400, 'Indica fechas válidas.');
        }
        return day;
      }
      final start = parse(fromDate), end = parse(toDate);
      if (!end.isAfter(start) || end.difference(start) > const Duration(days: 366)) {
        throw ApiFailure(400, 'El rango debe ser positivo y de hasta 366 días.');
      }
      result['from'] = '${fromDate}T00:00:00-04:00';
      result['to'] = '${toDate}T00:00:00-04:00';
    }
    return result;
  }
}
class PendingBookingIntent {
  final JsonMap slot;
  final JsonMap body;
  final String key;
  PendingBookingIntent(JsonMap original, String insurer, String member, {String? key})
      : slot = Map<String, dynamic>.unmodifiable(original),
        body = Map<String, dynamic>.unmodifiable({'slotId': original['id'], 'insurer': insurer, 'memberLast4': member}),
        key = key ?? ApiClient.uuid();
  bool matches(JsonMap slot, String insurer, String member) =>
      jsonEncode(body) == jsonEncode({'slotId': slot['id'], 'insurer': insurer, 'memberLast4': member});
}
class AppointmentRepository {
  final ApiClient api;
  AppointmentRepository(this.api);
  Future<List<AppointmentRecord>> list(AppointmentFilters filters) async {
    final query = filters.query();
    final path = Uri(path: '/appointments/me', queryParameters: query.isEmpty ? null : query).toString();
    return (await api.request('GET', path) as List).map((item) => AppointmentRecord.fromJson(item as Map)).toList();
  }
  Future<AppointmentSnapshot> snapshot(String id) async {
    final values = await Future.wait([
      api.request('GET', '/appointments/${Uri.encodeComponent(id)}'),
      api.request('GET', '/appointments/${Uri.encodeComponent(id)}/history'),
      api.request('GET', '/appointments/${Uri.encodeComponent(id)}/check-in-requirements'),
    ]);
    return AppointmentSnapshot(AppointmentRecord.fromJson(values[0] as Map),
      (values[1] as List).map((item) => AppointmentHistoryEntry.fromJson(item as Map)).toList(),
      CheckInRequirements.fromJson(values[2] as Map));
  }
  Future<void> book(PendingBookingIntent intent) async {
    await api.request('POST', '/appointments', body: intent.body, extraHeaders: {'Idempotency-Key': intent.key});
  }
  Future<void> cancel(JsonMap appointment) async {
    await api.request('POST', '/appointments/${Uri.encodeComponent(appointment['id'] as String)}/cancel',
      body: {'version': appointment['version']});
  }
  Future<void> reschedule(JsonMap appointment, String slotId) async {
    await api.request('POST', '/appointments/${Uri.encodeComponent(appointment['id'] as String)}/reschedule',
      body: {'slotId': slotId, 'version': appointment['version']});
  }
  Future<CheckInRequirements> submitCheckIn(
      String id, JsonMap body, String key) async => CheckInRequirements.fromJson(
    await api.request('POST', '/appointments/${Uri.encodeComponent(id)}/check-in',
      body: body, extraHeaders: {'Idempotency-Key': key}) as Map);
}
