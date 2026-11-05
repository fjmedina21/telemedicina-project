import 'package:flutter/material.dart';

import 'app/presentation/patient_shell.dart';
import 'app/theme/brand_theme.dart';

void main() => runApp(const MyApp());

class MyApp extends StatelessWidget {
  const MyApp({super.key});
  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'Telemedicina',
    debugShowCheckedModeBanner: false,
    theme: BrandTheme.light,
    home: const PatientShell(),
  );
}
