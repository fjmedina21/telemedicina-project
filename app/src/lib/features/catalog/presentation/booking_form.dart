import 'package:flutter/material.dart';

import '../../../core/models/json_map.dart';
import '../../../shared/formatting/date_time.dart';
import '../../../shared/widgets/labeled_text_field.dart';

class BookingForm extends StatefulWidget {
  final List<JsonMap> services, slots;
  final String? selectedServiceId;
  final bool busy, hasProfile;
  final ValueChanged<String?> onServiceChanged;
  final VoidCallback onAvailability;
  final Future<bool> Function(JsonMap, String, String) onBook;
  const BookingForm({
    super.key,
    required this.services,
    required this.slots,
    required this.selectedServiceId,
    required this.busy,
    required this.hasProfile,
    required this.onServiceChanged,
    required this.onAvailability,
    required this.onBook,
  });
  @override
  State<BookingForm> createState() => _BookingFormState();
}

class _BookingFormState extends State<BookingForm> {
  final insurer = TextEditingController(), member = TextEditingController();
  @override
  void dispose() {
    insurer.dispose();
    member.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const Text(
        'Solicitar cita',
        style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
      ),
      const SizedBox(height: 16),
      if (!widget.hasProfile) const Text('Complete primero su perfil.'),
      if (widget.services.isEmpty)
        const Text('No hay servicios disponibles. Puede actualizar más tarde.'),
      if (widget.services.isNotEmpty)
        InputDecorator(
          decoration: const InputDecoration(labelText: 'Servicio'),
          child: DropdownButtonHideUnderline(
            child: DropdownButton<String>(
              value: widget.selectedServiceId,
              isExpanded: true,
              items: widget.services
                  .map(
                    (service) => DropdownMenuItem(
                      value: service['id'] as String,
                      child: Text(
                        '${service['name']} · ${service['durationMinutes']} min',
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  )
                  .toList(),
              onChanged: widget.busy ? null : widget.onServiceChanged,
            ),
          ),
        ),
      const SizedBox(height: 16),
      FilledButton(
        onPressed: widget.busy || widget.services.isEmpty
            ? null
            : widget.onAvailability,
        child: const Text('Consultar horarios'),
      ),
      LabeledTextField(
        controller: insurer,
        label: 'Aseguradora para revisión manual',
        enabled: !widget.busy,
      ),
      LabeledTextField(
        controller: member,
        label: 'Últimos 4 dígitos de afiliación',
        enabled: !widget.busy,
      ),
      const Text(
        'La disponibilidad no garantiza reserva. No se confirma cobertura ni copago sin revisión.',
      ),
      const SizedBox(height: 20),
      for (final slot in widget.slots)
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  localTime(slot['startAt'] as String),
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: 4),
                Text(slot['practitionerName'] as String),
                const SizedBox(height: 12),
                FilledButton(
                  onPressed: widget.busy || !widget.hasProfile
                      ? null
                      : () => widget.onBook(
                          slot,
                          insurer.text.trim(),
                          member.text.trim(),
                        ),
                  child: const Text('Solicitar'),
                ),
              ],
            ),
          ),
        ),
    ],
  );
}
