import 'package:flutter_test/flutter_test.dart';
import 'package:telemedicina_app/main.dart';

void main() {
  testWidgets('El arranque muestra acceso de paciente y no el contador', (
    tester,
  ) async {
    await tester.pumpWidget(const MyApp());
    expect(find.text('Entrar'), findsOneWidget);
    expect(find.text('Crear cuenta'), findsNothing);
    expect(find.text('Flutter Demo Home Page'), findsNothing);
    await tester.ensureVisible(find.text('Registrarme'));
    await tester.tap(find.text('Registrarme'));
    await tester.pump();
    expect(find.text('Crear cuenta'), findsOneWidget);
  });
}
