import 'package:flutter/material.dart';

import '../../../core/models/json_map.dart';
import '../data/profile_repository.dart';

class PatientProfileForm extends StatefulWidget {
  final JsonMap? profile;
  // This revision changes only after accepting a reload/save, never during a draft.
  final int version;
  final bool busy;
  final Future<bool> Function(JsonMap) onSave;
  final void Function(JsonMap)? onChanged;
  final Future<bool> Function()? onReload;
  final Map<String, DeclarationMetadata> metadata;
  final ProfileCompleteness? completeness;
  final Map<String, String> errors;
  final bool conflict;
  const PatientProfileForm({
    super.key,
    required this.profile,
    required this.version,
    required this.busy,
    required this.onSave,
    this.onChanged,
    this.onReload,
    this.metadata = const {},
    this.completeness,
    this.errors = const {},
    this.conflict = false,
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
    'allergies': 'Alergias',
    'medications': 'Medicamentos habituales',
    'conditions': 'Antecedentes',
  };
  static const limits = {
    'firstName': 80,
    'lastName': 80,
    'phone': 30,
    'address': 250,
    'emergencyName': 100,
    'emergencyPhone': 30,
  };
  final _form = GlobalKey<FormState>();
  late final fields = {
    for (final key in ProfileRepository.fields) key: TextEditingController(),
  };
  final lists = <String, List<TextEditingController>>{};
  bool _reading = false;
  bool _submitting = false;
  final _edited = <String>{};

  void readProfile() {
    _reading = true;
    _edited.clear();
    for (final entry in fields.entries) {
      entry.value.text = (widget.profile?[entry.key] ?? '').toString();
    }
    for (final controllers in lists.values) {
      for (final controller in controllers) {
        controller.dispose();
      }
    }
    lists.clear();
    for (final category in ProfileRepository.categories) {
      final values = widget.profile?[category];
      lists[category] = [
        if (values is List)
          for (final value in values.whereType<String>())
            TextEditingController(text: value),
      ];
    }
    _reading = false;
  }

  JsonMap body() => {
    for (final entry in fields.entries) entry.key: entry.value.text.trim(),
    for (final entry in lists.entries)
      entry.key: entry.value.map((c) => c.text.trim()).toList(),
  };

  void changed(String key) {
    if (_reading) return;
    setState(() => _edited.add(key));
    widget.onChanged?.call(body());
  }

  @override
  void initState() {
    super.initState();
    readProfile();
  }

  @override
  void didUpdateWidget(covariant PatientProfileForm oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.version != widget.version) {
      readProfile();
    }
  }

  @override
  void dispose() {
    for (final controller in fields.values) {
      controller.dispose();
    }
    for (final controllers in lists.values) {
      for (final controller in controllers) {
        controller.dispose();
      }
    }
    super.dispose();
  }

  String? validate(String key, String? value) {
    if (!_edited.contains(key) && widget.errors[key] != null)
      return widget.errors[key];
    final text = value?.trim() ?? '';
    if (text.isEmpty) return 'Completa este campo.';
    if (text.length > (limits[key] ?? 200))
      return 'El texto supera el límite permitido.';
    if (key == 'birthDate') {
      final date = DateTime.tryParse(text);
      final today = DateTime.now().toUtc();
      if (!RegExp(r'^\d{4}-\d{2}-\d{2}$').hasMatch(text) ||
          date == null ||
          date.year < 1900 ||
          date.month.toString().padLeft(2, '0') != text.substring(5, 7) ||
          date.day.toString().padLeft(2, '0') != text.substring(8, 10) ||
          !DateTime.utc(
            date.year,
            date.month,
            date.day,
          ).isBefore(DateTime.utc(today.year, today.month, today.day))) {
        return 'Usa una fecha válida anterior a hoy (AAAA-MM-DD).';
      }
    }
    return null;
  }

  String dateLabel(String category) {
    final date = widget.metadata[category]?.updatedAt;
    if (date == null) return 'Autodeclarado · Fecha no disponible';
    final local = date.toUtc().subtract(const Duration(hours: 4));
    String two(int n) => n.toString().padLeft(2, '0');
    return 'Autodeclarado · ${local.year}-${two(local.month)}-${two(local.day)} ${two(local.hour)}:${two(local.minute)} (America/Santo_Domingo)';
  }

  Future<void> reload() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('¿Descartar el borrador?'),
        content: const Text(
          'La recarga reemplazará tus cambios por el perfil guardado.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Conservar borrador'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Descartar y recargar'),
          ),
        ],
      ),
    );
    if (confirmed == true && mounted) await widget.onReload?.call();
  }

  Future<void> save() async {
    if (_submitting || widget.busy || !_form.currentState!.validate()) return;
    setState(() => _submitting = true);
    try {
      final success = await widget.onSave(body());
      if (!success && mounted) {
        // Backend errors arrive on the next parent rebuild; the draft stays intact.
        setState(() => _edited.clear());
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final disabled = widget.busy || _submitting;
    return Form(
      key: _form,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Mi perfil',
            style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
          ),
          const Text(
            'Alergias, medicamentos y antecedentes son datos autodeclarados, no diagnósticos confirmados.',
          ),
          const SizedBox(height: 12),
          if (widget.completeness != null)
            Semantics(
              liveRegion: true,
              child: Text(
                widget.completeness!.status == 'Complete'
                    ? 'Perfil completo'
                    : 'Información pendiente: ${widget.completeness!.missingFields.map((f) => labels[f] ?? f).join(', ')}',
              ),
            ),
          if (widget.conflict)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: Text(
                'Existe otra versión del perfil. Tu borrador se conserva; recarga antes de volver a guardar.',
              ),
            ),
          if (widget.onReload != null)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton(
                onPressed: disabled ? null : reload,
                child: const Text('Recargar perfil'),
              ),
            ),
          for (final key in ProfileRepository.fields) ...[
            if (key == 'firstName' || key == 'emergencyName') ...[
              const Divider(),
              Text(
                key == 'firstName'
                    ? 'Datos personales'
                    : 'Contacto de emergencia',
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
            ],
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 8),
              child: TextFormField(
                key: ValueKey(key),
                controller: fields[key],
                enabled: !disabled,
                maxLength: limits[key] ?? 10,
                keyboardType: key.toLowerCase().contains('phone')
                    ? TextInputType.phone
                    : null,
                onChanged: (_) => changed(key),
                validator: (value) => validate(key, value),
                autovalidateMode: AutovalidateMode.onUserInteraction,
                decoration: InputDecoration(
                  labelText: labels[key],
                  errorText: _edited.contains(key) ? null : widget.errors[key],
                ),
              ),
            ),
          ],
          const Divider(),
          const Text(
            'Información autodeclarada',
            style: TextStyle(fontWeight: FontWeight.w600),
          ),
          for (final category in ProfileRepository.categories) ...[
            const SizedBox(height: 12),
            Text(
              labels[category]!,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            Text(dateLabel(category)),
            if (lists[category]!.isEmpty) const Text('Sin datos registrados'),
            if (!_edited.contains(category) && widget.errors[category] != null)
              Semantics(
                liveRegion: true,
                child: Text(
                  widget.errors[category]!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
            for (final controller in lists[category]!)
              Padding(
                key: ObjectKey(controller),
                padding: const EdgeInsets.symmetric(vertical: 8),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: TextFormField(
                        controller: controller,
                        enabled: !disabled,
                        maxLength: 200,
                        decoration: InputDecoration(
                          labelText:
                              'Elemento de ${labels[category]!.toLowerCase()}',
                        ),
                        onChanged: (_) => changed(category),
                        validator: (value) => validate(category, value),
                        autovalidateMode: AutovalidateMode.onUserInteraction,
                      ),
                    ),
                    IconButton(
                      tooltip:
                          'Eliminar elemento de ${labels[category]!.toLowerCase()}',
                      onPressed: disabled
                          ? null
                          : () {
                              setState(
                                () => lists[category]!.remove(controller),
                              );
                              // Removed text fields unmount on the next frame.
                              WidgetsBinding.instance.addPostFrameCallback(
                                (_) => controller.dispose(),
                              );
                              changed(category);
                            },
                      icon: const Icon(Icons.delete_outline),
                    ),
                  ],
                ),
              ),
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: disabled || lists[category]!.length >= 20
                    ? null
                    : () {
                        setState(
                          () => lists[category]!.add(TextEditingController()),
                        );
                        changed(category);
                      },
                icon: const Icon(Icons.add),
                label: Text('Añadir ${labels[category]!.toLowerCase()}'),
              ),
            ),
          ],
          const SizedBox(height: 16),
          FilledButton(
            onPressed: disabled || widget.conflict ? null : save,
            child: const Text('Guardar perfil'),
          ),
        ],
      ),
    );
  }
}
