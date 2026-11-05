import 'package:flutter/material.dart';

import '../../../core/models/json_map.dart';
import '../../../shared/widgets/brand_identity.dart';
import '../../../shared/formatting/date_time.dart';

class NotificationsView extends StatelessWidget {
  final List<JsonMap> notifications;
  final bool busy, emailEnabled;
  final ValueChanged<bool> onEmailChanged;
  final ValueChanged<JsonMap> onRead;
  const NotificationsView({
    super.key,
    required this.notifications,
    required this.busy,
    required this.emailEnabled,
    required this.onEmailChanged,
    required this.onRead,
  });
  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const Text(
        'Avisos',
        style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
      ),
      SwitchListTile(
        title: const Text('Recibir avisos de citas por correo'),
        value: emailEnabled,
        subtitle: const Text(
          'La verificación y recuperación de la cuenta se envían cuando las solicitas.',
        ),
        onChanged: busy ? null : onEmailChanged,
      ),
      const SizedBox(height: 20),
      if (notifications.isEmpty)
        const EmptyState(
          icon: Icons.notifications_none_rounded,
          title: 'Estás al día',
          description: 'Los cambios de tus citas aparecerán aquí.',
        ),
      for (final notification in notifications)
        Card(
          child: Padding(
            padding: const EdgeInsets.all(18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(notification['body'] as String),
                const SizedBox(height: 8),
                Text(
                  localTime(notification['createdAt'] as String),
                  style: TextStyle(
                    fontSize: 12,
                    color: Theme.of(context).colorScheme.onSurfaceVariant,
                  ),
                ),
                const SizedBox(height: 8),
                if (notification['readAt'] == null)
                  TextButton(
                    onPressed: busy ? null : () => onRead(notification),
                    child: const Text('Marcar leído'),
                  )
                else
                  const Row(
                    children: [
                      Icon(Icons.done, size: 16),
                      SizedBox(width: 6),
                      Text('Leído'),
                    ],
                  ),
              ],
            ),
          ),
        ),
    ],
  );
}
