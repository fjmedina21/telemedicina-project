import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:telemedicina_app/core/network/api_client.dart';
import 'package:telemedicina_app/features/auth/data/auth_repository.dart';

void main() {
  test('Verifica el código normalizado junto al correo del paciente', () async {
    final api = ApiClient(client: MockClient((request) async {
      expect(request.url.path, '/api/v1/auth/verify');
      expect(jsonDecode(request.body), {
        'email': 'synthetic@example.invalid',
        'token': 'AB12CD',
      });
      return http.Response(jsonEncode({
        'success': true, 'status': 200, 'data': {'verified': true},
      }), 200);
    }));
    addTearDown(api.close);
    await AuthRepository(api).verify('synthetic@example.invalid', ' ab12cd ');
  });
}
