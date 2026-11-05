import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:telemedicina_app/core/network/api_client.dart';
import 'package:telemedicina_app/app/application/patient_controller.dart';

http.Response ok(Map<String, dynamic> data) => http.Response(jsonEncode({'data': data}), 200);

void main() {
  test('Sesión revocada limpia tokens, identidad y datos del paciente', () async {
    final api = ApiClient(client: MockClient((request) async {
      if (request.url.path.endsWith('/auth/login')) {
        return ok({'role': 'Patient', 'accessToken': 'synthetic-access', 'refreshToken': 'synthetic-refresh'});
      }
      return http.Response(jsonEncode({'code': 'SESSION_REQUIRED'}), 401);
    }));
    await api.login('synthetic@example.invalid', 'synthetic-password');
    final controller = PatientController(api);
    addTearDown(controller.dispose);
    controller.identity = {'role': 'Patient', 'permissions': ['identity.self'], 'accountVersion': 1};
    controller.profile = {'firstName': 'Paciente sintético'};
    controller.appointments = [{'id': 'synthetic-appointment'}];
    controller.notifications = [{'id': 'synthetic-notification'}];
    expect(await controller.execute(controller.load), isFalse);
    expect(api.authenticated, isFalse);
    expect(controller.identity, isNull);
    expect(controller.profile, isNull);
    expect(controller.appointments, isEmpty);
    expect(controller.notifications, isEmpty);
    expect(controller.message, contains('sesión'));
  });

  test('403 comunica permiso insuficiente sin borrar una sesión válida', () async {
    final api = ApiClient(client: MockClient((request) async {
      if (request.url.path.endsWith('/auth/login')) {
        return ok({'role': 'Patient', 'accessToken': 'synthetic-access', 'refreshToken': 'synthetic-refresh'});
      }
      return http.Response(jsonEncode({'code': 'ACCESS_DENIED'}), 403);
    }));
    addTearDown(api.close);
    await api.login('synthetic@example.invalid', 'synthetic-password');
    await expectLater(api.request('GET', '/admin/staff'),
      throwsA(isA<ApiFailure>().having((error) => error.status, 'status', 403)
        .having((error) => error.message, 'message', contains('permiso'))));
    expect(api.authenticated, isTrue);
  });

  test('La identidad propia renueva acceso y conserva el contrato aditivo', () async {
    var reads = 0;
    final api = ApiClient(client: MockClient((request) async {
      if (request.url.path.endsWith('/auth/login') || request.url.path.endsWith('/auth/refresh')) {
        return ok({'role': 'Patient', 'accessToken': 'synthetic-access', 'refreshToken': 'synthetic-refresh'});
      }
      if (++reads == 1) return http.Response('{}', 401);
      return ok({'userId': 'synthetic-user', 'email': 'synthetic@example.invalid', 'role': 'Patient',
        'permissions': ['identity.self', 'profile.self'], 'accountVersion': 2});
    }));
    addTearDown(api.close);
    await api.login('synthetic@example.invalid', 'synthetic-password');
    final me = await api.request('GET', '/auth/me') as Map;
    expect(me['accountVersion'], 2);
    expect(me['permissions'], contains('profile.self'));
    expect(api.authenticated, isTrue);
  });

  for (final role in ['Administrator', 'Admission', 'Practitioner', 'Support']) {
    test('La app rechaza una cuenta $role y cierra su sesión', () async {
      var loggedOut = false;
      final api = ApiClient(client: MockClient((request) async {
        if (request.url.path.endsWith('/auth/login')) {
          return ok({'role': role, 'accessToken': 'synthetic-access', 'refreshToken': 'synthetic-refresh'});
        }
        loggedOut = request.url.path.endsWith('/auth/logout');
        return ok({'loggedOut': true});
      }));
      addTearDown(api.close);
      await expectLater(api.login('synthetic@example.invalid', 'synthetic-password'), throwsA(isA<ApiFailure>()));
      expect(api.authenticated, isFalse);
      expect(loggedOut, isTrue);
    });
  }
}
