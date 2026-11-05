import 'package:flutter/material.dart';

import '../../../core/models/json_map.dart';
import '../../../shared/widgets/labeled_text_field.dart';

class PatientProfileForm extends StatefulWidget {
  final JsonMap? profile;
  final int version;
  final bool busy;
  final Future<bool> Function(JsonMap) onSave;
  const PatientProfileForm({
    super.key,
    required this.profile,
    required this.version,
    required this.busy,
    required this.onSave,
  });
  @override
  State<PatientProfileForm> createState() => _PatientProfileFormState();
}

class _PatientProfileFormState extends State<PatientProfileForm> {
  static const labels = {
    'firstName': 'Nombres',
    'lastName': 'Apellidos',
    'birthDate': 'Nacimiento (AAAA-MM-DD)',
    'phone': 'Teléfono',
    'address': 'Dirección',
    'emergencyName': 'Contacto de emergencia',
    'emergencyPhone': 'Teléfono de emergencia',
    'allergies': 'Alergias (separadas por comas)',
    'medications': 'Medicamentos habituales (separados por comas)',
    'conditions': 'Antecedentes (separados por comas)',
  };
  late final fields = {
    for (final key in labels.keys) key: TextEditingController(),
  };
  void readProfile() {
    for (final entry in fields.entries) {
      final value = widget.profile?[entry.key];
      entry.value.text = value is List
          ? value.join(', ')
          : (value ?? '').toString();
    }
  }

  @override
  void initState() {
    super.initState();
    readProfile();
  }

  @override
  void didUpdateWidget(covariant PatientProfileForm oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.version != widget.version ||
        oldWidget.profile != widget.profile) {
      readProfile();
    }
  }

  @override
  void dispose() {
    for (final field in fields.values) {
      field.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const Text(
        'Mi perfil',
        style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
      ),
      const Text(
        'Alergias, medicamentos y antecedentes son datos autodeclarados, no diagnósticos confirmados.',
      ),
      const SizedBox(height: 20),
      for (final entry in labels.entries) ...[
        if ([
          'firstName',
          'emergencyName',
          'allergies',
        ].contains(entry.key)) ...[
          if (entry.key != 'firstName') const Divider(),
          Text(
            {
              'firstName': 'Datos personales',
              'emergencyName': 'Contacto de emergencia',
              'allergies': 'Información autodeclarada',
            }[entry.key]!,
            style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 8),
        ],
        LabeledTextField(
          controller: fields[entry.key]!,
          label: entry.value,
          enabled: !widget.busy,
          keyboardType: entry.key.toLowerCase().contains('phone')
              ? TextInputType.phone
              : null,
        ),
      ],
      const SizedBox(height: 16),
      FilledButton(
        onPressed: widget.busy
            ? null
            : () {
                final body = <String, dynamic>{};
                for (final entry in fields.entries) {
                  body[entry.key] =
                      [
                        'allergies',
                        'medications',
                        'conditions',
                      ].contains(entry.key)
                      ? entry.value.text
                            .split(',')
                            .map((value) => value.trim())
                            .where((value) => value.isNotEmpty)
                            .toList()
                      : entry.value.text.trim();
                }
                widget.onSave(body);
              },
        child: const Text('Guardar perfil'),
      ),
    ],
  );
}
