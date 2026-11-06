import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:telemedicina_app/core/network/api_client.dart';
import 'package:telemedicina_app/features/appointments/data/appointment_repository.dart';

void main() {
  test('Filters send Santo Domingo offsets and exclude omitted values', () {
    final query = const AppointmentFilters(fromDate: '2026-11-01', toDate: '2026-12-01', status: 'Confirmed', coverageStatus: 'Expired').query();
    expect(query, {'from': '2026-11-01T00:00:00-04:00', 'to': '2026-12-01T00:00:00-04:00', 'status': 'Confirmed', 'coverageStatus': 'Expired'});
    expect(const AppointmentFilters().query(), isEmpty);
    for (final filter in [
      const AppointmentFilters(fromDate: '2026-11-01'),
      const AppointmentFilters(fromDate: '2026-02-30', toDate: '2026-03-01'),
      const AppointmentFilters(fromDate: '2026-11-02', toDate: '2026-11-01'),
      const AppointmentFilters(fromDate: '2026-01-01', toDate: '2027-01-03'),
      const AppointmentFilters(status: ''),
    ]) { expect(filter.query, throwsA(isA<ApiFailure>())); }
  });
  test('Pending intent freezes body and key independently of mutable slot', () async {
    final original = <String, dynamic>{'id': 'slot-one', 'serviceName': 'Servicio sintético'};
    final intent = PendingBookingIntent(original, ' Aseguradora sintética ', '1234', key: 'synthetic-key');
    original['id'] = 'different-slot';
    expect(intent.body['slotId'], 'slot-one');
    expect(intent.slot['id'], 'slot-one');
    expect(() => intent.body['insurer'] = 'changed', throwsUnsupportedError);
    final requests = <http.Request>[];
    final api = ApiClient(client: MockClient((request) async {
      requests.add(request); return http.Response('{"data":{}}', 201);
    }));
    addTearDown(api.close);
    final repository = AppointmentRepository(api);
    await repository.book(intent); await repository.book(intent);
    expect(requests.map((r) => r.body).toSet(), hasLength(1));
    expect(jsonDecode(requests.first.body)['insurer'], ' Aseguradora sintética ');
    expect(requests.map((r) => r.headers['Idempotency-Key']).toSet(), {'synthetic-key'});
  });
  test('List repository preserves additive manual coverage DTO and encoded filters', () async {
    late Uri url;
    final api = ApiClient(client: MockClient((request) async {
      url = request.url;
      return http.Response(jsonEncode({'data': [{'id': 'appointment-one', 'version': 3, 'coverageStatus': 'Expired', 'coverageMethod': 'Manual', 'coverageRequiresReview': true, 'coverageValidityReason': 'Expired', 'practitionerId': 'person-one'}]}), 200);
    }));
    addTearDown(api.close);
    final values = await AppointmentRepository(api).list(const AppointmentFilters(coverageStatus: 'Expired'));
    expect(url.queryParameters['coverageStatus'], 'Expired');
    expect(values.single.toJson()['coverageRequiresReview'], true);
    expect(values.single.coverageStatus, 'Expired');
    expect(manualCoverageLabel('Approved'), 'Revisión manual favorable');
  });
  test('Snapshot decodes disabled check-in requirements with separate DTO responses', () async {
    final api = ApiClient(client: MockClient((request) async {
      final path = request.url.path;
      expect(request.method, 'GET');
      if (path.endsWith('/appointments/appointment-one/history')) {
        return http.Response(jsonEncode({'data': <dynamic>[]}), 200);
      }
      if (path.endsWith('/appointments/appointment-one/check-in-requirements')) {
        return http.Response(jsonEncode({'data': {
          'status': 'NotConfigured',
          'canProceed': false,
          'configurationVersion': null,
        }}), 200);
      }
      if (path.endsWith('/appointments/appointment-one')) {
        return http.Response(jsonEncode({'data': {
          'id': 'appointment-one',
          'version': 1,
          'coverageStatus': 'ManualReview',
        }}), 200);
      }
      throw StateError('Unexpected route: ${request.url.path}');
    }));
    addTearDown(api.close);
    final snapshot = await AppointmentRepository(api).snapshot('appointment-one');
    expect(snapshot.checkInRequirements.disabled, isTrue);
    expect(snapshot.checkInRequirements.canProceed, isFalse);
  });
}
