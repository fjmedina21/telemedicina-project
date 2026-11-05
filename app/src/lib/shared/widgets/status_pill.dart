import 'package:flutter/material.dart';

class StatusPill extends StatelessWidget {
  final String value;
  const StatusPill(this.value, {super.key});
  @override
  Widget build(BuildContext context) {
    final (label, foreground, background) = switch (value) {
      'Confirmed' => (
        'Confirmada',
        const Color(0xFF205440),
        const Color(0xFFE5F1E8),
      ),
      'Approved' => (
        'Aprobada',
        const Color(0xFF205440),
        const Color(0xFFE5F1E8),
      ),
      'PendingValidation' => (
        'Pendiente',
        const Color(0xFF785316),
        const Color(0xFFF9EFDA),
      ),
      'Pending' => (
        'Pendiente',
        const Color(0xFF785316),
        const Color(0xFFF9EFDA),
      ),
      'ManualReview' => (
        'En revisión',
        const Color(0xFF785316),
        const Color(0xFFF9EFDA),
      ),
      'Denied' => (
        'Denegada',
        const Color(0xFF963F32),
        const Color(0xFFFAE8E3),
      ),
      'Rejected' => (
        'Rechazada',
        const Color(0xFF963F32),
        const Color(0xFFFAE8E3),
      ),
      'Cancelled' => (
        'Cancelada',
        const Color(0xFF58655F),
        const Color(0xFFEDF1ED),
      ),
      'Expired' => (
        'Vencida',
        const Color(0xFF58655F),
        const Color(0xFFEDF1ED),
      ),
      _ => (value, const Color(0xFF58655F), const Color(0xFFEDF1ED)),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(6),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: foreground,
          fontSize: 12,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
