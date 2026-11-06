import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:telemedicina_app/features/appointments/data/appointment_repository.dart';
import 'package:telemedicina_app/features/appointments/domain/reschedule_options.dart';
import 'package:telemedicina_app/features/appointments/presentation/appointments_view.dart';

void main() {
  testWidgets('NotConfigured shows notice without a continue action', (tester) async {
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
        checkInRequirements: const CheckInRequirements('NotConfigured', false, null),
        busy: false,
        run: (action) async { await action(); return true; },
        onCancel: (_) async {},
        onLoadOptions: (appointment) async => RescheduleOptions(appointment, const []),
        onReschedule: (_, __) async {},
      ))),
    ));
    expect(find.text('La admisión virtual aún no está disponible.'), findsOneWidget);
    expect(find.text('Continuar'), findsNothing);
    expect(find.text('Iniciar check-in'), findsNothing);
  });
}
