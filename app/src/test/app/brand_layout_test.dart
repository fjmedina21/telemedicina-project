import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:telemedicina_app/main.dart';

void main() {
  for (final scale in [1.0, 1.5]) {
    testWidgets('Acceso y registro a 390px con texto $scale', (tester) async {
      await tester.binding.setSurfaceSize(const Size(390, 844));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.pumpWidget(
        MediaQuery(
          data: MediaQueryData(textScaler: TextScaler.linear(scale)),
          child: const MyApp(),
        ),
      );
      expect(tester.takeException(), isNull);
      await tester.ensureVisible(find.text('Registrarme'));
      await tester.tap(find.text('Registrarme'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Crear cuenta'));
      expect(find.text('Crear cuenta'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }
}
