import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:telemedicina_app/core/network/api_client.dart';

void main() {
  test(
    'Conserva 409 para que la interfaz permita resolver un conflicto',
    () async {
      final api = ApiClient(
        client: MockClient(
          (r) async =>
              http.Response(jsonEncode({'title': 'Horario ocupado'}), 409),
        ),
      );
      await expectLater(
        api.request('POST', '/appointments', body: {}),
        throwsA(isA<ApiFailure>().having((e) => e.status, 'status', 409)),
      );
      api.close();
    },
  );
  test('Renueva sesión y conserva Idempotency-Key al reintentar', () async {
    var bookingCalls = 0;
    final key = ApiClient.uuid();
    final api = ApiClient(
      client: MockClient((r) async {
        if (r.url.path.endsWith('/auth/login')) {
          return http.Response(
            jsonEncode({
              'data': {
                'role': 'Patient',
                'accessToken': 'old',
                'refreshToken': 'refresh',
              },
            }),
            200,
          );
        }
        if (r.url.path.endsWith('/auth/refresh')) {
          return http.Response(
            jsonEncode({
              'data': {'accessToken': 'new', 'refreshToken': 'rotated'},
            }),
            200,
          );
        }
        expect(r.headers['Idempotency-Key'], key);
        bookingCalls++;
        return bookingCalls == 1
            ? http.Response('{}', 401)
            : http.Response(
                jsonEncode({
                  'data': {'id': 'one'},
                }),
                201,
              );
      }),
    );
    await api.login('synthetic@example.invalid', 'synthetic-password');
    final result = await api.request(
      'POST',
      '/appointments',
      body: {'slotId': 'slot'},
      extraHeaders: {'Idempotency-Key': key},
    );
    expect(result['id'], 'one');
    expect(bookingCalls, 2);
    api.close();
  });
  test('Cerrar sesión borra tokens incluso si falla la red', () async {
    final api = ApiClient(
      client: MockClient((r) async {
        if (r.url.path.endsWith('/auth/login')) {
          return http.Response(
            jsonEncode({
              'data': {
                'role': 'Patient',
                'accessToken': 'old',
                'refreshToken': 'refresh',
              },
            }),
            200,
          );
        }
        throw http.ClientException('offline');
      }),
    );
    await api.login('synthetic@example.invalid', 'synthetic-password');
    await expectLater(api.logout(), throwsA(isA<ApiFailure>()));
    expect(api.authenticated, isFalse);
    api.close();
  });
}
