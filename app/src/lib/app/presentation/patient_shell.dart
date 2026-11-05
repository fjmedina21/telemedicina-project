import 'package:flutter/material.dart';

import '../../core/network/api_client.dart';
import '../theme/brand_theme.dart';
import '../../shared/widgets/brand_identity.dart';
import '../application/patient_controller.dart';
import '../../features/auth/presentation/auth_form.dart';
import '../../features/patient_profile/presentation/patient_profile_form.dart';
import '../../features/catalog/presentation/booking_form.dart';
import '../../features/appointments/presentation/appointments_view.dart';
import '../../features/notifications/presentation/notifications_view.dart';

class PatientShell extends StatefulWidget {
  final ApiClient? api;
  const PatientShell({super.key, this.api});
  @override
  State<PatientShell> createState() => _PatientShellState();
}

class _PatientShellState extends State<PatientShell> {
  late final controller = PatientController(widget.api ?? ApiClient());
  int tab = 0;
  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  Widget content() {
    final c = controller;
    if (!c.authenticated) {
      return AuthForm(
        auth: c.auth,
        run: c.execute,
        onLogin: c.login,
        onMessage: c.setMessage,
        busy: c.busy,
      );
    }
    return switch (tab) {
      0 => PatientProfileForm(
        profile: c.profile,
        version: c.profileVersion,
        busy: c.busy,
        onSave: (body) => c.execute(() => c.saveProfile(body)),
      ),
      1 => BookingForm(
        services: c.services,
        slots: c.slots,
        selectedServiceId: c.selectedServiceId,
        busy: c.busy,
        hasProfile: c.profileVersion > 0,
        onServiceChanged: c.selectService,
        onAvailability: () => c.execute(c.availability),
        onBook: (slot, insurer, member) async {
          final saved = await c.execute(() => c.book(slot, insurer, member));
          if (saved && mounted) {
            setState(() => tab = 2);
          }
          return saved;
        },
      ),
      2 => AppointmentsView(
        appointments: c.appointments,
        busy: c.busy,
        run: c.execute,
        onCancel: c.cancel,
        onLoadOptions: c.rescheduleOptions,
        onReschedule: c.reschedule,
      ),
      _ => NotificationsView(
        notifications: c.notifications,
        busy: c.busy,
        emailEnabled: c.emailEnabled,
        onEmailChanged: (value) => c.execute(() => c.setEmailEnabled(value)),
        onRead: (notification) => c.execute(() => c.markRead(notification)),
      ),
    };
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) {
      final c = controller;
      return Scaffold(
        appBar: AppBar(
          title: const BrandIdentity(),
          actions: [
            if (c.authenticated)
              IconButton(
                tooltip: 'Actualizar',
                onPressed: c.busy ? null : () => c.execute(c.load),
                icon: const Icon(Icons.refresh),
              ),
            if (c.authenticated)
              IconButton(
                tooltip: 'Salir',
                onPressed: c.busy ? null : () => c.execute(c.logout),
                icon: const Icon(Icons.logout_rounded),
              ),
          ],
        ),
        body: Column(
          children: [
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
              decoration: const BoxDecoration(color: BrandTheme.soft),
              child: const Text(
                'Demostración · Datos sintéticos · Sin atención clínica',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 11, color: BrandTheme.primary),
              ),
            ),
            if (c.busy) const LinearProgressIndicator(),
            if (c.message.isNotEmpty)
              Padding(
                padding: const EdgeInsets.all(12),
                child: Semantics(liveRegion: true, child: Text(c.message)),
              ),
            Expanded(
              child: SingleChildScrollView(
                padding: const EdgeInsets.symmetric(
                  horizontal: 20,
                  vertical: 28,
                ),
                child: Center(
                  child: ConstrainedBox(
                    constraints: BoxConstraints(
                      maxWidth: c.authenticated ? 760 : 460,
                    ),
                    child: Material(
                      color: Colors.white,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(20),
                        side: const BorderSide(color: BrandTheme.border),
                      ),
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: content(),
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
        bottomNavigationBar: !c.authenticated
            ? null
            : NavigationBar(
                selectedIndex: tab,
                onDestinationSelected: c.busy
                    ? null
                    : (value) => setState(() => tab = value),
                destinations: const [
                  NavigationDestination(
                    icon: Icon(Icons.person_outline_rounded),
                    label: 'Mi perfil',
                  ),
                  NavigationDestination(
                    icon: Icon(Icons.add_circle_outline_rounded),
                    label: 'Solicitar cita',
                  ),
                  NavigationDestination(
                    icon: Icon(Icons.calendar_today_outlined),
                    label: 'Mis citas',
                  ),
                  NavigationDestination(
                    icon: Icon(Icons.notifications_none_rounded),
                    label: 'Avisos',
                  ),
                ],
              ),
      );
    },
  );
}
