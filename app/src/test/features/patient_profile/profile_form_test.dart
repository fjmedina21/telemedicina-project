import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:telemedicina_app/core/models/json_map.dart';
import 'package:telemedicina_app/features/patient_profile/presentation/patient_profile_form.dart';

JsonMap fixture() => {
  'firstName': 'Paciente',
  'lastName': 'Sintético',
  'birthDate': '1990-01-01',
  'phone': '+18095550100',
  'address': 'Dirección sintética',
  'emergencyName': 'Contacto sintético',
  'emergencyPhone': '+18095550101',
  'allergies': <String>[],
  'medications': <String>[],
  'conditions': <String>[],
};
Widget page(Widget child) => MaterialApp(
  home: Scaffold(body: SingleChildScrollView(child: child)),
);

void main() {
  testWidgets(
    'Individual declarations preserve commas and show unknown dates',
    (tester) async {
      JsonMap? saved;
      await tester.pumpWidget(
        page(
          PatientProfileForm(
            profile: fixture(),
            version: 1,
            busy: false,
            onSave: (body) async {
              saved = body;
              return false;
            },
          ),
        ),
      );
      expect(
        find.text('Autodeclarado · Fecha no disponible'),
        findsNWidgets(3),
      );
      expect(find.text('Sin datos registrados'), findsNWidgets(3));
      await tester.ensureVisible(find.text('Añadir alergias'));
      await tester.tap(find.text('Añadir alergias'));
      await tester.pumpAndSettle();
      final allergy = find.widgetWithText(
        TextFormField,
        'Elemento de alergias',
      );
      await tester.ensureVisible(allergy);
      await tester.enterText(allergy, 'Un elemento, con coma');
      await tester.ensureVisible(find.text('Guardar perfil'));
      await tester.tap(find.text('Guardar perfil'));
      await tester.pumpAndSettle();
      expect(saved?['allergies'], ['Un elemento, con coma']);
      expect(saved?['medications'], isEmpty);
    },
  );

  testWidgets('External rebuild keeps draft; confirmed reload is explicit', (
    tester,
  ) async {
    var reloaded = 0;
    Widget form(JsonMap data, {int revision = 1}) => page(
      PatientProfileForm(
        profile: data,
        version: revision,
        busy: false,
        onSave: (_) async => false,
        onReload: () async {
          reloaded++;
          return true;
        },
      ),
    );
    await tester.pumpWidget(form(fixture()));
    await tester.enterText(find.byKey(const ValueKey('firstName')), 'Borrador');
    await tester.pumpWidget(
      form({...fixture(), 'firstName': 'Cambio externo'}),
    );
    expect(find.text('Borrador'), findsOneWidget);
    await tester.ensureVisible(find.text('Recargar perfil'));
    await tester.tap(find.text('Recargar perfil'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Conservar borrador'));
    await tester.pumpAndSettle();
    expect(reloaded, 0);
    expect(find.text('Borrador'), findsOneWidget);
    await tester.tap(find.text('Recargar perfil'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Descartar y recargar'));
    await tester.pumpAndSettle();
    expect(reloaded, 1);
    await tester.pumpWidget(
      form({...fixture(), 'firstName': 'Perfil recargado'}, revision: 2),
    );
    await tester.pumpAndSettle();
    expect(find.text('Perfil recargado'), findsOneWidget);
    expect(find.text('Borrador'), findsNothing);
  });

  testWidgets(
    'Required fields block submission and profile fits scaled narrow display',
    (tester) async {
      var calls = 0;
      await tester.binding.setSurfaceSize(const Size(390, 844));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.pumpWidget(
        MaterialApp(
          home: MediaQuery(
            data: const MediaQueryData(textScaler: TextScaler.linear(1.5)),
            child: Scaffold(
              body: SingleChildScrollView(
                child: PatientProfileForm(
                  profile: {...fixture(), 'firstName': ''},
                  version: 1,
                  busy: false,
                  onSave: (_) async {
                    calls++;
                    return true;
                  },
                ),
              ),
            ),
          ),
        ),
      );
      await tester.ensureVisible(find.text('Guardar perfil'));
      await tester.tap(find.text('Guardar perfil'));
      await tester.pumpAndSettle();
      expect(calls, 0);
      expect(find.text('Completa este campo.'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
}
