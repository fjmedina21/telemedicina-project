import 'package:flutter/material.dart';

import '../../app/theme/brand_theme.dart';

class BrandIdentity extends StatelessWidget {
  const BrandIdentity({super.key});
  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Container(
        width: 40,
        height: 40,
        decoration: BoxDecoration(
          color: BrandTheme.primary,
          borderRadius: BorderRadius.circular(12),
        ),
        child: const Icon(Icons.add_rounded, color: Colors.white, size: 30),
      ),
      const SizedBox(width: 10),
      const Flexible(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'telemedicina',
              style: TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.w600,
                letterSpacing: -.7,
                color: BrandTheme.ink,
              ),
            ),
            Text(
              'Tu espacio de salud',
              style: TextStyle(fontSize: 10, color: BrandTheme.muted),
            ),
          ],
        ),
      ),
    ],
  );
}

class EmptyState extends StatelessWidget {
  final IconData icon;
  final String title, description;
  const EmptyState({
    super.key,
    required this.icon,
    required this.title,
    required this.description,
  });
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 36, horizontal: 12),
    child: Column(
      children: [
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: BrandTheme.soft,
            borderRadius: BorderRadius.circular(20),
          ),
          child: Icon(icon, color: BrandTheme.primary, size: 28),
        ),
        const SizedBox(height: 18),
        Text(
          title,
          textAlign: TextAlign.center,
          style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
        ),
        const SizedBox(height: 6),
        Text(
          description,
          textAlign: TextAlign.center,
          style: const TextStyle(color: BrandTheme.muted),
        ),
      ],
    ),
  );
}
