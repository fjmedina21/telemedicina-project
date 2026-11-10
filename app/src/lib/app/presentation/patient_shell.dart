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
import '../../features/video_visit/presentation/video_visit_screen.dart';

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
        profile: c.profileDraft,
        version: c.profileDraftRevision,
        metadata: c.declarationMetadata,
        completeness: c.profileCompleteness,
        errors: c.profileErrors,
        conflict: c.profileConflict || c.profileChangedElsewhere,
        onChanged: c.updateProfileDraft,
        onReload: () => c.execute(c.reloadProfile),
        busy: c.busy,
        onSave: (body) => c.execute(() => c.saveProfile(body)),
      ),
      1 => BookingForm(
        services: c.filteredServices,
        slots: c.slots,
        specialties: c.specialties,
        practitioners: c.practitioners,
        selectedSpecialty: c.selectedSpecialty,
        selectedPractitionerId: c.selectedPractitionerId,
        fromDate: c.catalogFromDate,
        toDate: c.catalogToDate,
        insurerDraft: c.bookingInsurerDraft,
        memberDraft: c.bookingMemberDraft,
        availabilityState: c.availabilityState,
        availabilityError: c.availabilityError,
        catalogError: c.catalogError,
        practitionersLoading: c.practitionersLoading,
        onSpecialtyChanged: (value) => c.execute(() => c.selectSpecialty(value)),
        onPractitionerChanged: c.selectPractitioner,
        onRangeChanged: c.updateAvailabilityRange,
        onDraftChanged: c.updateBookingDraft,
        onReloadCatalog: () => c.execute(c.refreshCatalog),
        selectedServiceId: c.selectedServiceId,
        busy: c.busy,
        hasProfile: c.canRequestAppointment,
        pendingBooking: c.pendingBooking,
        onAbandonPending: c.abandonBooking,
        onProfile: () => setState(() => tab = 0),
        sessionChanges: c,
        sessionGeneration: c.api.sessionGeneration,
        isSessionCurrent: (generation) => c.authenticated && c.api.sessionGeneration == generation,
        onServiceChanged: (value) => c.execute(() => c.selectService(value)),
        onAvailability: () => c.execute(c.availability),
        onBook: (slot, insurer, member) async {
          final saved = await c.execute(() => c.book(slot, insurer, member));
          if (mounted) {
            if (saved) { setState(() => tab = 2); }
            else if (c.bookingNeedsProfile) { setState(() => tab = 0); }
          }
          return saved;
        },
      ),
      2 => AppointmentsView(
        appointments: c.appointments,
        filters: c.appointmentFilters,
        loading: c.appointmentsLoading,
        error: c.appointmentsError,
        detail: c.appointmentDetail,
        checkInRequirements: c.appointmentCheckInRequirements,
        history: c.appointmentHistory,
        detailLoading: c.appointmentDetailLoading,
        detailError: c.appointmentDetailError,
        onOpen: c.openAppointment,
        onCheckIn: c.submitCheckIn,
        onVideo: (appointment) async {
          await Navigator.of(context).push(MaterialPageRoute(builder: (_) => VideoVisitScreen(
            api: c.api, appointmentId: appointment['id'] as String,
            practitionerName: appointment['practitionerName'] as String? ?? 'Profesional')));
        },
        onFilter: (filters) => c.refreshAppointments(filters: filters),
        sessionChanges: c,
        sessionGeneration: c.api.sessionGeneration,
        isSessionCurrent: (generation) => c.authenticated && c.api.sessionGeneration == generation,
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
