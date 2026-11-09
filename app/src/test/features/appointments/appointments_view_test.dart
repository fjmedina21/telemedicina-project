import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:telemedicina_app/features/appointments/data/appointment_repository.dart';
import 'package:telemedicina_app/features/appointments/domain/reschedule_options.dart';
import 'package:telemedicina_app/features/appointments/presentation/appointments_view.dart';

void main() {
  testWidgets('Check-in saves partial non-clinical progress', (tester) async {
    Map<String,dynamic>? submitted;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(body: SingleChildScrollView(child: AppointmentsView(
        appointments: const [],
        detail: {
          'id': 'appointment-one',
          'serviceName': 'Servicio sintético',
          'practitionerName': 'Profesional sintético',
          'startAt': '2026-11-10T14:00:00Z',
          'status': 'PendingValidation',
          'coverageStatus': 'ManualReview',
          'coverageRequiresReview': false,
          'memberLast4': '1234',
          'evidence': null,
          'validUntil': null,
        },
        checkInRequirements: const CheckInRequirements(status:'NotStarted',canProceed:true,
          configurationVersion:1,version:0,windowOpenAt:'2026-11-10T13:30:00Z',
          windowCloseAt:'2026-11-10T14:15:00Z',prepared:false,privacyConfirmed:false,
          connectivityConfirmed:false,technicalPreflightCompleted:false,
          consent:{'version':1,'hash':'synthetic','text':'Demostración académica: no constituye atención médica real.','accepted':false},
          blockers:['ADMIN_CHECKLIST_INCOMPLETE']),
        onCheckIn: (value) async { submitted=value; },
        busy: false,
        run: (action) async { await action(); return true; },
        onCancel: (_) async {},
        onLoadOptions: (appointment) async => RescheduleOptions(appointment, const []),
        onReschedule: (_, __) async {},
      ))),
    ));
    await tester.tap(find.text('Completar check-in')); await tester.pumpAndSettle();
    expect(find.text('Cuestionario administrativo no clínico.'),findsOneWidget);
    await tester.tap(find.text('Estoy preparado para la demostración'));
    await tester.tap(find.text('Guardar progreso')); await tester.pumpAndSettle();
    expect(submitted?['prepared'],isTrue);
    expect(submitted?['consentAccepted'],isFalse);
  });
  testWidgets('Logout/session change removes an open check-in dialog', (tester) async {
    final changes=ChangeNotifier(); var current=true;
    addTearDown(changes.dispose);
    await tester.pumpWidget(MaterialApp(home:Scaffold(body:SingleChildScrollView(child:
      AppointmentsView(appointments:const [],detail:{'id':'appointment-one','serviceName':'Synthetic',
        'practitionerName':'Synthetic','startAt':'2040-01-01T15:00:00Z','status':'Confirmed',
        'coverageStatus':'Approved','coverageRequiresReview':false,'memberLast4':'0000',
        'evidence':null,'validUntil':null},
        checkInRequirements:const CheckInRequirements(status:'NotStarted',canProceed:true,
          configurationVersion:1,version:0,windowOpenAt:'2040-01-01T14:30:00Z',
          windowCloseAt:'2040-01-01T15:15:00Z',prepared:false,privacyConfirmed:false,
          connectivityConfirmed:false,technicalPreflightCompleted:false,
          consent:{'version':1,'hash':'synthetic','text':'demo','accepted':false},blockers:[]),
        busy:false,run:(action) async {await action();return true;},onCancel:(_) async {},
        onLoadOptions:(a) async=>RescheduleOptions(a,const []),onReschedule:(_,__) async {},
        onCheckIn:(_) async {},sessionChanges:changes,sessionGeneration:7,
        isSessionCurrent:(_)=>current)))));
    await tester.tap(find.text('Completar check-in'));await tester.pumpAndSettle();
    expect(find.text('Cuestionario administrativo no clínico.'),findsOneWidget);
    current=false;changes.notifyListeners();await tester.pumpAndSettle();
    expect(find.text('Cuestionario administrativo no clínico.'),findsNothing);
  });
}
