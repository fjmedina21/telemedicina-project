import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:telemedicina_app/core/network/api_client.dart';
import 'package:telemedicina_app/app/presentation/patient_shell.dart';

void main() {
  testWidgets('El paciente cambia preferencias y marca un aviso leído', (
    tester,
  ) async {
    var enabled = false;
    var version = 0;
    var read = false;
    final client = MockClient((request) async {
      final path = request.url.path;
      dynamic data;
      if (path.endsWith('/auth/login')) {
        data = {
          'role': 'Patient',
          'accessToken': 'synthetic',
          'refreshToken': 'synthetic-refresh',
        };
      } else if (path.endsWith('/auth/me')) {
        data = {
          'userId': 'synthetic-patient',
          'email': 'synthetic@example.invalid',
          'role': 'Patient',
          'permissions': ['identity.self', 'profile.self', 'notifications.self'],
          'accountVersion': 1,
        };
      } else if (path.endsWith('/notifications/preferences')) {
        if (request.method == 'PATCH') {
          enabled = jsonDecode(request.body)['emailEnabled'] as bool;
          version++;
        }
        data = {'emailEnabled': enabled, 'version': version};
      } else if (path.endsWith('/notifications/test/read')) {
        read = true;
        data = {'read': true};
      } else if (path.endsWith('/notifications/me')) {
        data = [
          {
            'id': 'test',
            'body': 'Tu solicitud fue registrada.',
            'createdAt': '2026-11-05T12:00:00Z',
            'readAt': read ? '2026-11-05T12:05:00Z' : null,
          },
        ];
      } else if (path.endsWith('/patients/me')) {
        data = null;
      } else {
        data = [];
      }
      return http.Response(jsonEncode({'data': data}), 200);
    });
    await tester.pumpWidget(
      MaterialApp(
        home: PatientShell(api: ApiClient(client: client)),
      ),
    );
    final inputs = find.byType(TextField);
    await tester.enterText(inputs.at(0), 'synthetic@example.invalid');
    await tester.enterText(inputs.at(1), 'synthetic-password');
    await tester.ensureVisible(find.text('Entrar'));
    await tester.tap(find.text('Entrar'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Avisos'));
    await tester.pumpAndSettle();
    expect(find.text('Tu solicitud fue registrada.'), findsOneWidget);
    await tester.tap(find.byType(SwitchListTile));
    await tester.pumpAndSettle();
    expect(enabled, isTrue);
    await tester.tap(find.text('Marcar leído'));
    await tester.pumpAndSettle();
    expect(read, isTrue);
    expect(find.text('Marcar leído'), findsNothing);
  });
}
