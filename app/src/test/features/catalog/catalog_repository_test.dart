import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:telemedicina_app/core/network/api_client.dart';
import 'package:telemedicina_app/features/catalog/data/catalog_repository.dart';
http.Response envelope(dynamic data) => http.Response(jsonEncode({'data': data}), 200);
void main() {
  test('Civil dates use Santo Domingo and accept positive ranges up to 31 days', () {
    final range = CatalogDateRange.fromCivilDates('2026-11-01', '2026-12-02');
    expect(range.from, DateTime.utc(2026, 11, 1, 4));
    expect(range.to, DateTime.utc(2026, 12, 2, 4));
    expect(range.to.difference(range.from), const Duration(days: 31));
    expect(CatalogDateRange.civilDate(DateTime.parse('2026-11-02T02:00:00Z')), '2026-11-01');
    expect(CatalogDateRange.civilDate(DateTime.parse('2026-11-02T04:00:00Z')), '2026-11-02');
    expect(CatalogDateRange.fromCivilDates('2028-02-29', '2028-03-01').to
        .difference(DateTime.utc(2028, 2, 29, 4)), const Duration(days: 1));
  });
  test('Civil dates reject malformed dates and invalid ranges', () {
    for (final pair in [
      ['2026-11-01', '2026-11-01'], ['2026-11-02', '2026-11-01'],
      ['2026-11-01', '2026-12-03'], ['2026-02-29', '2026-03-01'],
      ['2026-11-31', '2026-12-01'], ['2026-1-01', '2026-11-02'],
      ['2026-11-01T00:00:00Z', '2026-11-02'],
    ]) {
      expect(() => CatalogDateRange.fromCivilDates(pair[0], pair[1]),
          throwsA(isA<ApiFailure>().having((e) => e.status, 'status', 400)),
          reason: pair.toString());
    }
  });
  test('Catalog maps specialties and duration from array envelopes', () async {
    final api = ApiClient(client: MockClient((r) async {
      if (r.url.path.endsWith('/specialties')) {
        return envelope([{'name': 'Especialidad sintética'}]);
      }
      expect(r.url.path, '/api/v1/services');
      return envelope([{'id': 'synthetic-service', 'name': 'Servicio sintético',
        'specialty': 'Especialidad sintética', 'durationMinutes': 30}]);
    }));
    addTearDown(api.close);
    final options = await CatalogRepository(api).catalog();
    expect(options.specialties, ['Especialidad sintética']);
    expect(options.services.single.durationMinutes, 30);
    expect(options.services.single.toJson()['id'], 'synthetic-service');
  });
  test('Repository encodes filters, sends UTC and preserves slot DTO', () async {
    final requests = <Uri>[];
    final api = ApiClient(client: MockClient((r) async {
      requests.add(r.url);
      if (r.url.path.endsWith('/practitioners')) {
        return envelope([{'id': 'synthetic-practitioner', 'name': 'Profesional sintético'}]);
      }
      return envelope([{'id': 'synthetic-slot', 'serviceId': 'service +/&',
        'practitionerId': 'person +/&', 'practitionerName': 'Profesional sintético',
        'serviceName': 'Servicio sintético', 'specialty': 'Especialidad sintética',
        'startAt': '2026-11-02T09:00:00-04:00', 'endAt': '2026-11-02T09:30:00-04:00'}]);
    }));
    addTearDown(api.close);
    final repository = CatalogRepository(api);
    final practitioners = await repository.practitioners('service +/&');
    expect(practitioners.single.name, 'Profesional sintético');
    expect(requests.first.queryParameters, {'serviceId': 'service +/&'});
    final slots = await repository.availability(serviceId: 'service +/&',
      practitionerId: 'person +/&', fromDate: '2026-11-01', toDate: '2026-11-03');
    expect(requests.last.queryParameters, {'serviceId': 'service +/&',
      'practitionerId': 'person +/&', 'from': '2026-11-01T04:00:00.000Z',
      'to': '2026-11-03T04:00:00.000Z'});
    expect(slots.single.startAt, DateTime.utc(2026, 11, 2, 13));
    expect(slots.single.toJson()['endAt'], '2026-11-02T13:30:00.000Z');
    await repository.availability(serviceId: 'service +/&',
      fromDate: '2026-11-01', toDate: '2026-11-03');
    expect(requests.last.queryParameters.containsKey('practitionerId'), isFalse);
  });
  test('Malformed DTO returns safe 502', () async {
    final api = ApiClient(client: MockClient((r) async =>
      envelope([{'id': 42, 'name': 'DO_NOT_ECHO_SYNTHETIC_VALUE'}])));
    addTearDown(api.close);
    await expectLater(CatalogRepository(api).practitioners('synthetic-service'),
      throwsA(isA<ApiFailure>().having((e) => e.status, 'status', 502)
        .having((e) => e.message, 'safe message', 'Respuesta de catálogo no válida.')));
  });
}
