import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:telemedicina_app/features/catalog/presentation/booking_form.dart';
import 'package:telemedicina_app/core/models/json_map.dart';

void main() {
  final slot = <String, dynamic>{
    'id': 'slot-one', 'serviceName': 'Servicio sintético', 'practitionerName': 'Profesional sintético',
    'startAt': '2035-11-01T13:00:00Z', 'endAt': '2035-11-01T13:30:00Z',
  };
  Widget form(Future<bool> Function(JsonMap, String, String) send, {ValueNotifier<bool>? session}) =>
    MaterialApp(home: Scaffold(body: SingleChildScrollView(child: BookingForm(
      services: const [], slots: [slot], selectedServiceId: null, busy: false, hasProfile: true,
      insurerDraft: 'Aseguradora sintética', memberDraft: '1234',
      onServiceChanged: (_) {}, onAvailability: () {}, onBook: send,
      sessionChanges: session, isSessionCurrent: (_) => session?.value ?? true,
    ))));
  testWidgets('Booking shows complete summary and sends only after explicit confirmation', (tester) async {
    var sends = 0;
    await tester.pumpWidget(form((slot, insurer, member) async { sends++; return true; }));
    await tester.ensureVisible(find.text('Solicitar'));
    await tester.tap(find.text('Solicitar')); await tester.pumpAndSettle();
    expect(sends, 0);
    expect(find.text('Revisar solicitud'), findsOneWidget);
    expect(find.text('Duración: 30 minutos'), findsOneWidget);
    expect(find.text('Afiliación: ••••1234'), findsOneWidget);
    expect(find.text('01/11/2035 09:00 (Santo Domingo)'), findsWidgets);
    await tester.tap(find.text('Confirmar solicitud para revisión')); await tester.pumpAndSettle();
    expect(sends, 1);
  });
  testWidgets('Old session overlay is removed without submitting the old intention', (tester) async {
    final session = ValueNotifier(true); addTearDown(session.dispose);
    var sends = 0;
    await tester.pumpWidget(form((slot, insurer, member) async { sends++; return true; }, session: session));
    await tester.ensureVisible(find.text('Solicitar'));
    await tester.tap(find.text('Solicitar')); await tester.pumpAndSettle();
    session.value = false; await tester.pump(); await tester.pumpAndSettle();
    expect(find.text('Revisar solicitud'), findsNothing);
    expect(sends, 0);
  });
}
