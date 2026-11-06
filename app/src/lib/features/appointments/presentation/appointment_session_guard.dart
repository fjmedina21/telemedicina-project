import 'package:flutter/material.dart';

/// A response from an old session must not leave private content in an overlay.
class AppointmentSessionGuard extends StatelessWidget {
  final Listenable? changes;
  final bool Function()? isCurrent;
  final Widget child;
  const AppointmentSessionGuard({super.key, this.changes, this.isCurrent, required this.child});
  @override
  Widget build(BuildContext context) {
    Widget content() {
      if (isCurrent?.call() ?? true) return child;
      final route = ModalRoute.of(context);
      final navigator = Navigator.of(context);
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (route != null && route.isActive) navigator.removeRoute(route);
      });
      return const SizedBox.shrink();
    }
    return changes == null ? content() : ListenableBuilder(listenable: changes!, builder: (context, _) => content());
  }
}
