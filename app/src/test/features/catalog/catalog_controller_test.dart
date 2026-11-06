import 'dart:async';
import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:telemedicina_app/app/application/patient_controller.dart';
import 'package:telemedicina_app/core/network/api_client.dart';
import 'package:telemedicina_app/features/catalog/data/catalog_repository.dart';
http.Response envelope(dynamic data) => http.Response(jsonEncode({'data': data}), 200);
Map<String, dynamic> slot(String id) => {'id': id, 'serviceId': 'service-one',
  'practitionerId': 'person-one', 'practitionerName': 'Profesional sintético',
  'serviceName': 'Servicio sintético', 'specialty': 'Especialidad uno',
  'startAt': '2026-11-02T13:00:00Z', 'endAt': '2026-11-02T13:30:00Z'};
class Fixture {
  Future<http.Response> Function(http.Request)? onAvailability;
  int bookingStatus = 400;
  bool bookingOffline = false;
  List<String> bookingErrors = [];
  final bookingRequests = <http.Request>[];
  late final api = ApiClient(client: MockClient(handle));
  late final controller = PatientController(api);
  Future<http.Response> handle(http.Request request) async {
    final path = request.url.path;
    if (path.endsWith('/auth/login')) {
      return envelope({'role': 'Patient', 'accessToken': 'synthetic-token',
        'refreshToken': 'synthetic-refresh'});
    }
    if (path.endsWith('/auth/logout')) return envelope(null);
    if (path.endsWith('/auth/me')) return envelope({'role': 'Patient'});
    if (path.endsWith('/patients/me')) {
      return envelope({'profile': {'firstName': 'Paciente sintético', 'lastName': 'Prueba'},
        'version': 1, 'declarationMetadata': {},
        'completeness': {'status': 'Complete', 'missingFields': <String>[]}});
    }
    if (path.endsWith('/specialties')) {
      return envelope([{'name': 'Especialidad uno'}, {'name': 'Especialidad dos'}]);
    }
    if (path.endsWith('/services')) {
      return envelope([
        {'id': 'service-one', 'name': 'Servicio uno', 'specialty': 'Especialidad uno', 'durationMinutes': 30},
        {'id': 'service-two', 'name': 'Servicio dos', 'specialty': 'Especialidad dos', 'durationMinutes': 20},
      ]);
    }
    if (path.endsWith('/practitioners')) {
      return envelope([{'id': 'person-one', 'name': 'Profesional sintético'}]);
    }
    if (path.endsWith('/availability')) {
      final handler = onAvailability;
      return handler == null ? envelope(<dynamic>[]) : handler(request);
    }
    if (path.endsWith('/notifications/preferences')) {
      return envelope({'emailEnabled': false, 'version': 1});
    }
    if (path.endsWith('/appointments') && request.method == 'POST') {
      bookingRequests.add(request);
      if (bookingOffline) throw http.ClientException('synthetic offline');
      return http.Response(jsonEncode({'detail': 'Solicitud rechazada',
        'code': bookingStatus == 409 ? 'SLOT_UNAVAILABLE' : 'VALIDATION_FAILED',
        'errors': bookingErrors}), bookingStatus);
    }
    if (path.endsWith('/appointments/me') || path.endsWith('/notifications/me')) {
      return envelope(<dynamic>[]);
    }
    throw StateError('Unexpected synthetic fixture route: $path');
  }
  Future<void> login() async {
    await controller.login('synthetic@example.invalid', 'synthetic-password');
    controller.updateAvailabilityRange('2026-11-01', '2026-11-03');
  }
  void dispose() => controller.dispose();
}
void main() {
  test('Changing filters invalidates displayed availability', () async {
    final fixture = Fixture();
    addTearDown(fixture.dispose);
    await fixture.login();
    final c = fixture.controller;
    fixture.onAvailability = (r) async => envelope([slot('slot-one')]);
    await c.availability();
    expect(c.availabilityState, CatalogAvailabilityState.results);
    c.selectPractitioner('person-one');
    expect(c.slots, isEmpty);
    expect(c.availabilityState, CatalogAvailabilityState.notQueried);
    await c.availability();
    c.updateAvailabilityRange('2026-11-02', '2026-11-04');
    expect(c.slots, isEmpty);
    await c.availability();
    await c.selectSpecialty('Especialidad dos');
    expect(c.filteredServices.map((item) => item['id']), ['service-two']);
    expect(c.selectedServiceId, 'service-two');
    expect(c.selectedPractitionerId, isNull);
    expect(c.slots, isEmpty);
    await c.selectSpecialty(null);
    await c.selectService('service-one');
    expect(c.selectedServiceId, 'service-one');
    expect(c.availabilityState, CatalogAvailabilityState.notQueried);
  });
  test('Late response cannot replace newer filter result', () async {
    final fixture = Fixture();
    addTearDown(fixture.dispose);
    await fixture.login();
    final c = fixture.controller;
    final started = Completer<void>();
    final pending = Completer<http.Response>();
    var calls = 0;
    fixture.onAvailability = (r) async {
      calls++;
      if (calls == 1) { started.complete(); return pending.future; }
      return envelope([slot('new-slot')]);
    };
    final first = c.availability();
    await started.future;
    c.updateAvailabilityRange('2026-11-02', '2026-11-04');
    await c.availability();
    expect(c.slots.single['id'], 'new-slot');
    pending.complete(envelope([slot('old-slot')]));
    await first;
    expect(c.slots.single['id'], 'new-slot');
    expect(c.availabilityState, CatalogAvailabilityState.results);
  });
  test('Logout clears draft and discards pending availability', () async {
    final fixture = Fixture();
    addTearDown(fixture.dispose);
    await fixture.login();
    final c = fixture.controller;
    c.updateBookingDraft('Aseguradora sintética', '1234');
    final started = Completer<void>();
    final pending = Completer<http.Response>();
    fixture.onAvailability = (r) async { started.complete(); return pending.future; };
    final request = c.availability();
    await started.future;
    await c.logout();
    pending.complete(envelope([slot('old-session-slot')]));
    await request;
    expect(c.authenticated, isFalse);
    expect(c.slots, isEmpty);
    expect(c.services, isEmpty);
    expect(c.practitioners, isEmpty);
    expect(c.selectedServiceId, isNull);
    expect(c.bookingInsurerDraft, '');
    expect(c.bookingMemberDraft, '');
    expect(c.availabilityState, CatalogAvailabilityState.notQueried);
  });
  test('400, 409 and network failure preserve booking draft and retry key', () async {
    final fixture = Fixture();
    addTearDown(fixture.dispose);
    await fixture.login();
    final c = fixture.controller;
    c.updateBookingDraft('Aseguradora sintética', '1234');
    final from = c.catalogFromDate, to = c.catalogToDate;
    for (final status in [400, 409, 503]) {
      fixture.bookingStatus = status;
      fixture.bookingOffline = status == 503;
      fixture.bookingErrors = status == 400 ? ['request.memberLast4:INVALID'] : [];
      expect(await c.execute(() => c.book(slot('slot-one'),
        c.bookingInsurerDraft, c.bookingMemberDraft)), isFalse);
      expect(c.bookingInsurerDraft, 'Aseguradora sintética');
      expect(c.bookingMemberDraft, '1234');
      expect(c.catalogFromDate, from);
      expect(c.catalogToDate, to);
      expect(c.busy, isFalse);
      if (status == 400) {
        expect(c.message, 'Revisa: últimos cuatro dígitos de afiliación.');
      }
    }
    expect(fixture.bookingRequests, hasLength(3));
    final keys = fixture.bookingRequests.map((r) => r.headers['Idempotency-Key']).toSet();
    expect(keys, hasLength(1));
    expect(keys.single, isNotNull);
    expect(fixture.bookingRequests.map((r) => r.body).toSet(), hasLength(1));
  });
}
