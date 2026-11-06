import 'dart:async';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:telemedicina_app/core/network/api_client.dart';
import 'package:telemedicina_app/features/patient_profile/data/profile_repository.dart';

http.Response response(dynamic data) =>
    http.Response(jsonEncode({'data': data}), 200);

void main() {
  test(
    'DTO uses outer version, declaration timestamps and completeness',
    () async {
      final api = ApiClient(
        client: MockClient(
          (r) async => response({
            'profile': {'firstName': 'Sintético', 'version': 1},
            'version': 7,
            'declarationMetadata': {
              'allergies': {
                'source': 'SelfDeclared',
                'updatedAt': '2026-11-05T14:30:00Z',
              },
            },
            'completeness': {
              'status': 'Incomplete',
              'missingFields': ['address'],
            },
          }),
        ),
      );
      addTearDown(api.close);
      final snapshot = await ProfileRepository(api).load();
      expect(snapshot.version, 7);
      expect(
        snapshot.metadata['allergies']!.updatedAt,
        DateTime.utc(2026, 11, 5, 14, 30),
      );
      expect(snapshot.metadata['conditions']!.updatedAt, isNull);
      expect(snapshot.completeness.missingFields, ['address']);
    },
  );

  test('No profile loads completeness from its protected endpoint', () async {
    final paths = <String>[];
    final api = ApiClient(
      client: MockClient((r) async {
        paths.add(r.url.path);
        return response(
          r.url.path.endsWith('/completeness')
              ? {
                  'status': 'Incomplete',
                  'missingFields': ProfileRepository.fields,
                }
              : null,
        );
      }),
    );
    addTearDown(api.close);
    final snapshot = await ProfileRepository(api).load();
    expect(snapshot.profile, isNull);
    expect(snapshot.version, 0);
    expect(paths, ['/api/v1/patients/me', '/api/v1/patients/me/completeness']);
  });

  test(
    'PATCH preserves commas, strips protected fields and never retries 409',
    () async {
      var calls = 0;
      final api = ApiClient(
        client: MockClient((r) async {
          calls++;
          final body = jsonDecode(r.body) as Map;
          expect(body['version'], 4);
          expect(body['allergies'], ['Sintético, elemento único']);
          expect(body.containsKey('patientId'), isFalse);
          expect(body.containsKey('declarationMetadata'), isFalse);
          return http.Response(
            jsonEncode({
              'code': 'VERSION_CONFLICT',
              'detail': 'Conflicto',
              'errors': <String>[],
            }),
            409,
          );
        }),
      );
      addTearDown(api.close);
      await expectLater(
        ProfileRepository(api).save({
          'patientId': 'protected',
          'declarationMetadata': {},
          'allergies': ['Sintético, elemento único'],
        }, 4),
        throwsA(
          isA<ApiFailure>().having((e) => e.code, 'code', 'VERSION_CONFLICT'),
        ),
      );
      expect(calls, 1);
    },
  );

  test('Field error mapping accepts only known names and safe codes', () {
    expect(
      ProfileRepository.fieldErrors([
        'phone:INVALID',
        'allergies:MAX_ITEMS',
        'address:REQUIRED',
        'unexpected:REQUIRED',
        'phone:sensitive text',
        'birthDate:INVALID:extra',
      ]),
      {
        'phone': 'Revisa el formato de este campo.',
        'allergies': 'Se permiten hasta 20 elementos.',
        'address': 'Completa este campo.',
      },
    );
  });

  test(
    'A late response from an earlier session cannot clear a newer session',
    () async {
      final pending = Completer<http.Response>();
      final api = ApiClient(
        client: MockClient((r) async {
          if (r.url.path.endsWith('/auth/login'))
            return response({
              'role': 'Patient',
              'accessToken': 'synthetic',
              'refreshToken': 'synthetic-refresh',
            });
          return pending.future;
        }),
      );
      addTearDown(api.close);
      await api.login('synthetic@example.invalid', 'synthetic-password');
      final late = api.request('GET', '/patients/me');
      await Future<void>.delayed(Duration.zero);
      api.clearSession();
      await api.login('another@example.invalid', 'synthetic-password');
      final rejected = expectLater(
        late,
        throwsA(isA<ApiFailure>().having((e) => e.status, 'status', 401)),
      );
      pending.complete(http.Response('{}', 401));
      await rejected;
      expect(api.authenticated, isTrue);
    },
  );
}
