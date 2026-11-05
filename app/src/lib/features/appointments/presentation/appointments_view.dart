import 'package:flutter/material.dart';

import '../../../core/models/json_map.dart';
import '../../../shared/widgets/brand_identity.dart';
import '../../../shared/widgets/status_pill.dart';
import '../../../shared/formatting/date_time.dart';
import '../../../shared/presentation/actions.dart';
import '../domain/reschedule_options.dart';

class AppointmentsView extends StatelessWidget {
  final List<JsonMap> appointments;
  final bool busy;
  final AsyncActionRunner run;
  final Future<void> Function(JsonMap) onCancel;
  final Future<RescheduleOptions> Function(JsonMap) onLoadOptions;
  final Future<void> Function(JsonMap, String) onReschedule;
  const AppointmentsView({
    super.key,
    required this.appointments,
    required this.busy,
    required this.run,
    required this.onCancel,
    required this.onLoadOptions,
    required this.onReschedule,
  });

  Future<void> cancel(BuildContext context, JsonMap appointment) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialog) => AlertDialog(
        title: const Text('¿Cancelar esta cita?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialog, false),
            child: const Text('Volver'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialog, true),
            child: const Text('Cancelar cita'),
          ),
        ],
      ),
    );
    if (confirmed == true) {
      await onCancel(appointment);
    }
  }

  Future<void> reschedule(BuildContext context, JsonMap appointment) async {
    final options = await onLoadOptions(appointment);
    if (!context.mounted) return;
    final chosen = await showDialog<String>(
      context: context,
      builder: (dialog) => SimpleDialog(
        title: const Text('Nuevo horario · volverá a revisión'),
        children: options.slots.isEmpty
            ? [
                const Padding(
                  padding: EdgeInsets.all(16),
                  child: Text('Sin horarios disponibles.'),
                ),
              ]
            : options.slots
                  .map<Widget>(
                    (slot) => SimpleDialogOption(
                      onPressed: () =>
                          Navigator.pop(dialog, slot['id'] as String),
                      child: Text(
                        '${localTime(slot['startAt'] as String)} · ${slot['practitionerName']}',
                      ),
                    ),
                  )
                  .toList(),
      ),
    );
    if (chosen != null) {
      await onReschedule(options.appointment, chosen);
    }
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const Text(
        'Mis citas',
        style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
      ),
      const Text('Consulta tus solicitudes y el estado de su revisión.'),
      const SizedBox(height: 24),
      if (appointments.isEmpty)
        const EmptyState(
          icon: Icons.calendar_today_outlined,
          title: 'Aún no tienes citas',
          description:
              'Cuando solicites una cita, podrás seguir su estado aquí.',
        ),
      for (final appointment in appointments)
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${appointment['serviceName']} · ${appointment['practitionerName']}',
                ),
                Text(localTime(appointment['startAt'] as String)),
                const SizedBox(height: 16),
                Wrap(
                  spacing: 12,
                  runSpacing: 10,
                  children: [
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Cita', style: TextStyle(fontSize: 11)),
                        const SizedBox(height: 4),
                        StatusPill(appointment['status'] as String),
                      ],
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Cobertura', style: TextStyle(fontSize: 11)),
                        const SizedBox(height: 4),
                        StatusPill(appointment['coverageStatus'] as String),
                      ],
                    ),
                  ],
                ),
                if ([
                      'PendingValidation',
                      'Confirmed',
                    ].contains(appointment['status']) &&
                    DateTime.parse(appointment['startAt'] as String)
                        .isAfter(DateTime.now()))
                  Wrap(
                    spacing: 8,
                    children: [
                      TextButton(
                        onPressed: busy
                            ? null
                            : () => run(() => cancel(context, appointment)),
                        child: const Text('Cancelar'),
                      ),
                      TextButton(
                        onPressed: busy
                            ? null
                            : () => run(() => reschedule(context, appointment)),
                        child: const Text('Reprogramar'),
                      ),
                    ],
                  ),
              ],
            ),
          ),
        ),
    ],
  );
}
