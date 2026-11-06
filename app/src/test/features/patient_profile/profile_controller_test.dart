import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:telemedicina_app/app/application/patient_controller.dart';
import 'package:telemedicina_app/core/network/api_client.dart';

void main() {
  test('Draft survives external refresh, 400, 409 and clears only after explicit reload/logout', () async {
    var version = 1;
    var patchStatus = 400;
    final sentVersions = <int>[];
    final api = ApiClient(
      client: MockClient((r) async {
        dynamic data;
        final path = r.url.path;
        if (path.endsWith('/auth/login')) {
          data = {
            'role': 'Patient',
            'accessToken': 'synthetic',
            'refreshToken': 'synthetic-refresh',
          };
        } else if (path.endsWith('/auth/me')) {
          data = {'role': 'Patient'};
        } else if (path.endsWith('/patients/me') && r.method == 'PATCH') {
          sentVersions.add((jsonDecode(r.body) as Map)['version'] as int);
          return http.Response(
            jsonEncode({
              'code': patchStatus == 409
                  ? 'VERSION_CONFLICT'
                  : 'INVALID_PROFILE',
              'detail': 'Solicitud rechazada',
              'errors': patchStatus == 400 ? ['phone:INVALID'] : <String>[],
            }),
            patchStatus,
          );
        } else if (path.endsWith('/patients/me')) {
          data = {
            'profile': {
              'firstName': 'Versión $version',
              'phone': '+18095550100',
            },
            'version': version,
            'declarationMetadata': {},
            'completeness': {
              'status': 'Incomplete',
              'missingFields': ['address'],
            },
          };
        } else if (path.endsWith('/notifications/preferences')) {
          data = {'emailEnabled': false, 'version': 0};
        } else {
          data = <dynamic>[];
        }
        return http.Response(jsonEncode({'data': data}), 200);
      }),
    );
    final c = PatientController(api);
    addTearDown(c.dispose);
    await c.login('synthetic@example.invalid', 'synthetic-password');
    c.updateProfileDraft({
      ...c.profileDraft,
      'firstName': 'Borrador sintético',
      'allergies': ['Uno, con coma'],
    });
    final revision = c.profileDraftRevision;
    version = 2;
    await c.load();
    expect(c.profileDraft['firstName'], 'Borrador sintético');
    expect(c.profileDraftVersion, 1);
    expect(c.profileVersion, 2);
    expect(c.profileDraftRevision, revision);
    expect(c.profileChangedElsewhere, isTrue);
    expect(await c.execute(() => c.saveProfile(c.profileDraft)), isFalse);
    expect(c.profileErrors['phone'], 'Revisa el formato de este campo.');
    expect(c.profileDraft['allergies'], ['Uno, con coma']);
    patchStatus = 409;
    expect(await c.execute(() => c.saveProfile(c.profileDraft)), isFalse);
    expect(c.profileConflict, isTrue);
    expect(sentVersions, [1, 1]);
    await c.reloadProfile();
    expect(c.profileDraft['firstName'], 'Versión 2');
    expect(c.profileDraftVersion, 2);
    expect(c.profileConflict, isFalse);
    expect(c.profileDirty, isFalse);
    await c.logout();
    expect(c.profile, isNull);
    expect(c.profileDraft['firstName'], '');
    expect(c.profileDraft['allergies'], isEmpty);
    expect(c.declarationMetadata, isEmpty);
    expect(c.authenticated, isFalse);
  });

  test('Network failure preserves draft and original version', () async {
    final api = ApiClient(
      client: MockClient((r) async {
        if (r.url.path.endsWith('/auth/login')) {
          return http.Response(
            jsonEncode({
              'data': {
                'role': 'Patient',
                'accessToken': 'synthetic',
                'refreshToken': 'synthetic-refresh',
              },
            }),
            200,
          );
        }
        throw http.ClientException('offline');
      }),
    );
    await api.login('synthetic@example.invalid', 'synthetic-password');
    final c = PatientController(api);
    addTearDown(c.dispose);
    c.profileDraftVersion = 3;
    final saved = await c.execute(
      () => c.saveProfile({
        'phone': 'synthetic-phone',
        'allergies': ['Uno, dos'],
      }),
    );
    expect(saved, isFalse);
    expect(c.profileDraft['phone'], 'synthetic-phone');
    expect(c.profileDraft['allergies'], ['Uno, dos']);
    expect(c.profileDraftVersion, 3);
    expect(c.profileDirty, isTrue);
  });
}
