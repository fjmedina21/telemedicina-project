import 'dart:async';
import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:telemedicina_app/core/network/api_client.dart';
import 'package:telemedicina_app/features/patient_profile/data/profile_repository.dart';
import '../catalog/catalog_controller_test.dart' as catalog;

class Fixture extends catalog.Fixture {
  String? profileCode;
  Future<http.Response> Function(http.Request)? appointmentResponse;
  static Map<String,dynamic> checkIn({String status='NotStarted'})=>{
    'status':status,'canProceed':true,'configurationVersion':1,'version':0,
    'windowOpenAt':'2040-01-01T14:30:00Z','windowCloseAt':'2040-01-01T15:15:00Z',
    'prepared':false,'privacyConfirmed':false,'connectivityConfirmed':false,
    'technicalPreflightCompleted':false,
    'consent':{'version':1,'hash':'synthetic','text':'demo','accepted':false},
    'blockers':['ADMIN_CHECKLIST_INCOMPLETE']};
  @override
  Future<http.Response> handle(http.Request request) async {
    if (request.url.path.endsWith('/appointments') && profileCode != null) {
      bookingRequests.add(request);
      return http.Response(jsonEncode({'code': profileCode, 'detail': 'Completa el perfil', 'errors': ['firstName:REQUIRED']}), 400);
    }
    if (request.url.path.endsWith('/appointments/appointment-one')) {
      final response = appointmentResponse;
      if (response != null) return response(request);
      return catalog.envelope({'id': 'appointment-one', 'version': 1, 'coverageStatus': 'ManualReview'});
    }
    if (request.url.path.endsWith('/appointments/appointment-one/check-in-requirements')) {
      return catalog.envelope(checkIn());
    }
    if (request.url.path.endsWith('/appointments/appointment-one/history')) {
      return catalog.envelope([{'sequence': 1, 'newStatus': 'PendingValidation', 'newCoverage': 'ManualReview'}]);
    }
    return super.handle(request);
  }
}
void main() {
  test('Book retry freezes body/key across refresh and retires key after successful commit', () async {
    final f = Fixture(); addTearDown(f.dispose); await f.login();
    f.bookingOffline = true;
    await expectLater(f.controller.book(catalog.slot('slot-one'), 'Aseguradora sintética', '1234'), throwsA(isA<ApiFailure>()));
    final key = f.controller.pendingBooking!.key;
    await f.controller.load();
    expect(f.controller.pendingBooking!.key, key);
    await expectLater(f.controller.book(catalog.slot('slot-two'), 'Aseguradora sintética', '1234'), throwsA(isA<ApiFailure>()));
    expect(f.bookingRequests, hasLength(1)); // changed intention never sent automatically
    f.bookingOffline = false; f.bookingStatus = 201;
    await f.controller.book(catalog.slot('slot-one'), 'Aseguradora sintética', '1234');
    expect(f.controller.pendingBooking, isNull);
    await f.controller.book(catalog.slot('slot-one'), 'Aseguradora sintética', '1234');
    expect(f.bookingRequests[0].headers['Idempotency-Key'], f.bookingRequests[1].headers['Idempotency-Key']);
    expect(f.bookingRequests[2].headers['Idempotency-Key'], isNot(key));
  });
  test('PROFILE_INCOMPLETE maps H03 errors and preserves selected slot, draft and pending intention', () async {
    final f = Fixture(); addTearDown(f.dispose); await f.login();
    f.controller.updateBookingDraft('Aseguradora sintética', '1234');
    final service = f.controller.selectedServiceId;
    f.profileCode = 'PROFILE_INCOMPLETE';
    expect(await f.controller.execute(() => f.controller.book(catalog.slot('slot-one'), 'Aseguradora sintética', '1234')), false);
    expect(f.controller.bookingNeedsProfile, true);
    expect(f.controller.profileCompleteness.status, 'Incomplete');
    expect(f.controller.profileErrors, contains('firstName'));
    expect(f.controller.pendingBooking!.body['slotId'], 'slot-one');
    expect(f.controller.bookingInsurerDraft, 'Aseguradora sintética');
    expect(f.controller.selectedServiceId, service);
    final key = f.controller.pendingBooking!.key;
    f.profileCode = null; f.bookingStatus = 201;
    // Even after a profile changes, a pending intent is replayed with the same key.
    await f.controller.book(catalog.slot('slot-one'), 'Aseguradora sintética', '1234');
    expect(f.bookingRequests.last.headers['Idempotency-Key'], key);
  });
  test('Administrative completeness blocks a new intent, not just profile version', () async {
    final f = Fixture(); addTearDown(f.dispose); await f.login();
    f.controller.profileCompleteness = const ProfileCompleteness('Incomplete', ['firstName']);
    expect(f.controller.profileVersion, greaterThan(0));
    await expectLater(f.controller.book(catalog.slot('slot-one'), 'Aseguradora sintética', '1234'), throwsA(isA<ApiFailure>()));
    expect(f.bookingRequests, isEmpty);
    expect(f.controller.pendingBooking, isNull);
  });
  test('Late detail response after logout cannot reinstall private state', () async {
    final f = Fixture(); addTearDown(f.dispose); await f.login();
    final started = Completer<void>(), response = Completer<http.Response>();
    f.appointmentResponse = (_) { started.complete(); return response.future; };
    final pending = f.controller.openAppointment({'id': 'appointment-one'});
    final assertion = expectLater(pending, throwsA(isA<ApiFailure>()));
    await started.future;
    await f.controller.logout();
    response.complete(catalog.envelope({'id': 'appointment-one', 'version': 1, 'coverageStatus': 'Approved'}));
    await assertion;
    expect(f.controller.appointmentDetail, isNull);
    expect(f.controller.appointmentHistory, isEmpty);
    expect(f.controller.appointmentCheckInRequirements, isNull);
    expect(f.controller.pendingBooking, isNull);
  });
  test('Appointment detail loads check-in requirements for the current session', () async {
    final f = Fixture(); addTearDown(f.dispose); await f.login();
    await f.controller.openAppointment({'id': 'appointment-one'});
    expect(f.controller.appointmentCheckInRequirements?.status, 'NotStarted');
    expect(f.controller.appointmentCheckInRequirements?.canProceed, isTrue);
  });
}
