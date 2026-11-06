import 'package:flutter/material.dart';
import '../../../core/models/json_map.dart';
import '../../../shared/widgets/brand_identity.dart';
import '../../../shared/widgets/status_pill.dart';
import '../../../shared/formatting/date_time.dart';
import '../../../shared/presentation/actions.dart';
import '../domain/reschedule_options.dart';
import '../data/appointment_repository.dart';
import 'appointment_session_guard.dart';

class AppointmentsView extends StatefulWidget {
  final List<JsonMap> appointments, history;
  final JsonMap? detail;
  final CheckInRequirements? checkInRequirements;
  final bool busy, loading, detailLoading;
  final String error, detailError;
  final AppointmentFilters filters;
  final AsyncActionRunner run;
  final Future<void> Function(JsonMap) onCancel;
  final Future<RescheduleOptions> Function(JsonMap) onLoadOptions;
  final Future<void> Function(JsonMap, String) onReschedule;
  final Future<void> Function(JsonMap)? onOpen;
  final Future<void> Function(AppointmentFilters)? onFilter;
  final Listenable? sessionChanges;
  final bool Function(int)? isSessionCurrent;
  final int sessionGeneration;
  const AppointmentsView({
    super.key, required this.appointments, required this.busy, required this.run,
    required this.onCancel, required this.onLoadOptions, required this.onReschedule,
    this.detail, this.checkInRequirements, this.history = const [],
    this.loading = false, this.detailLoading = false,
    this.error = '', this.detailError = '', this.filters = const AppointmentFilters(),
    this.onOpen, this.onFilter, this.sessionChanges, this.isSessionCurrent, this.sessionGeneration = 0,
  });
  @override
  State<AppointmentsView> createState() => _AppointmentsViewState();
}
class _AppointmentsViewState extends State<AppointmentsView> {
  late final from = TextEditingController(text: widget.filters.fromDate);
  late final to = TextEditingController(text: widget.filters.toDate);
  late String? status = widget.filters.status, coverage = widget.filters.coverageStatus;
  @override
  void dispose() { from.dispose(); to.dispose(); super.dispose(); }
  Widget guard(Widget child, int generation) => AppointmentSessionGuard(
    changes: widget.sessionChanges,
    isCurrent: () => mounted && generation == widget.sessionGeneration && (widget.isSessionCurrent?.call(generation) ?? true),
    child: child,
  );
  Future<void> mutation(JsonMap appointment, {required bool reschedule}) async {
    final generation = widget.sessionGeneration;
    RescheduleOptions? options;
    if (reschedule) {
      final loaded = await widget.run(() async { options = await widget.onLoadOptions(appointment); });
      if (!loaded || !mounted || generation != widget.sessionGeneration || !(widget.isSessionCurrent?.call(generation) ?? true)) return;
    }
    if (!mounted) return;
    await showDialog<void>(context: context, builder: (context) => guard(
      _AppointmentMutationDialog(
        appointment: options?.appointment ?? appointment,
        slots: options?.slots ?? const [],
        reschedule: reschedule,
        run: widget.run, onCancel: widget.onCancel, onReschedule: widget.onReschedule,
        onReload: widget.onLoadOptions,
        guard: (child) => guard(child, generation),
        active: () => mounted && generation == widget.sessionGeneration && (widget.isSessionCurrent?.call(generation) ?? true),
      ), generation));
  }
  Widget coverageText(JsonMap appointment) => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
    const Text('Cobertura · revisión manual'),
    Text(manualCoverageLabel(appointment['coverageStatus'] as String)),
    if (appointment['coverageRequiresReview'] == true && appointment['status'] == 'Confirmed')
      const Text('La cita figura confirmada y su cobertura requiere nueva revisión. Esto no cancela la cita ni habilita acceso a video.'),
    const Text('No constituye autorización recibida de ARS ni determina copago.'),
  ]);
  Widget detailPanel() {
    final value = widget.detail;
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      if (widget.detailLoading) const Text('Cargando detalle e historial…'),
      if (widget.detailError.isNotEmpty) Semantics(liveRegion: true, child: Text(widget.detailError)),
      if (value != null) Card(child: Padding(padding: const EdgeInsets.all(16), child: Column(
        crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Detalle de cita', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
          Text('${value['serviceName']} · ${value['practitionerName']}'),
          Text(localTime(value['startAt'] as String)),
          Text('Cita: ${value['status']}'),
          coverageText(value),
          if (widget.checkInRequirements?.disabled == true)
            Semantics(
              liveRegion: true,
              child: Text('La admisión virtual aún no está disponible.'),
            ),
          Text('Afiliación: ••••${value['memberLast4']}'),
          if (value['evidence'] != null) Text('Evidencia de revisión: ${value['evidence']}'),
          if (value['validUntil'] != null) Text('Vigencia: ${localTime(value['validUntil'] as String)}'),
          TextButton(onPressed: widget.busy || widget.onOpen == null ? null : () => widget.run(() => widget.onOpen!(value)),
            child: const Text('Recargar detalle e historial')),
          const Text('Historial de cambios', style: TextStyle(fontWeight: FontWeight.bold)),
          if (widget.history.isEmpty) const Text('Sin eventos disponibles.'),
          for (final event in widget.history) Padding(padding: const EdgeInsets.symmetric(vertical: 8), child: Text(
            '${localTime(event['occurredAt'] as String)} · Cita: ${event['newStatus']} · Cobertura: ${manualCoverageLabel(event['newCoverage'] as String)} · Versión ${event['version']}')),
        ],
      ))),
    ]);
  }
  Widget appointmentCard(JsonMap appointment) {
    final future = DateTime.parse(appointment['startAt'] as String).isAfter(DateTime.now());
    final mutable = future && ['PendingValidation', 'Confirmed'].contains(appointment['status']);
    return Card(child: Padding(padding: const EdgeInsets.all(20), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text('${appointment['serviceName']} · ${appointment['practitionerName']}'),
      Text(localTime(appointment['startAt'] as String)),
      const Text('Cita'), StatusPill(appointment['status'] as String),
      const SizedBox(height: 8), coverageText(appointment),
      Wrap(spacing: 8, children: [
        TextButton(onPressed: widget.busy || widget.onOpen == null ? null : () => widget.run(() => widget.onOpen!(appointment)),
          child: const Text('Ver detalle e historial')),
        if (mutable) ...[
          TextButton(onPressed: widget.busy ? null : () => mutation(appointment, reschedule: false), child: const Text('Cancelar')),
          TextButton(onPressed: widget.busy ? null : () => mutation(appointment, reschedule: true), child: const Text('Reprogramar')),
        ],
      ]),
    ])));
  }
  Widget selector(String label, String? value, List<String> values, ValueChanged<String?> onChanged, {bool manual = false}) =>
    Padding(padding: const EdgeInsets.symmetric(vertical: 8), child: DropdownButtonFormField<String>(
      initialValue: value ?? '', isExpanded: true, decoration: InputDecoration(labelText: label),
      items: [const DropdownMenuItem(value: '', child: Text('Todos')),
        for (final item in values) DropdownMenuItem(value: item, child: Text(manual ? manualCoverageLabel(item) : item, overflow: TextOverflow.ellipsis))],
      onChanged: widget.busy ? null : (value) => onChanged(value == '' ? null : value),
    ));
  @override
  Widget build(BuildContext context) {
    final now = DateTime.now();
    final upcoming = widget.appointments.where((a) => DateTime.parse(a['startAt'] as String).isAfter(now)).toList();
    final historic = widget.appointments.where((a) => !DateTime.parse(a['startAt'] as String).isAfter(now)).toList();
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      const Text('Mis citas', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold)),
      const Text('Se muestran hasta 100 citas. Acota fechas y estados para consultar el histórico. Las citas históricas se agrupan por fecha, sin inferir cierre clínico.'),
      TextField(controller: from, enabled: !widget.busy, decoration: const InputDecoration(labelText: 'Citas desde (AAAA-MM-DD, Santo Domingo)')),
      TextField(controller: to, enabled: !widget.busy, decoration: const InputDecoration(labelText: 'Citas hasta (excluyente, AAAA-MM-DD, Santo Domingo)')),
      selector('Estado de cita', status, AppointmentFilters.statuses, (value) => setState(() => status = value)),
      selector('Revisión de cobertura', coverage, AppointmentFilters.coverages, (value) => setState(() => coverage = value), manual: true),
      FilledButton(onPressed: widget.busy || widget.onFilter == null ? null : () => widget.run(() => widget.onFilter!(
        AppointmentFilters(fromDate: from.text, toDate: to.text, status: status, coverageStatus: coverage))),
        child: const Text('Consultar citas')),
      if (widget.loading) const Text('Cargando citas…'),
      if (widget.error.isNotEmpty) Semantics(liveRegion: true, child: Text(widget.error)),
      if (!widget.loading && widget.appointments.isEmpty) const EmptyState(
        icon: Icons.calendar_today_outlined, title: 'Sin citas para esta consulta', description: 'Prueba con otros filtros o solicita una cita.'),
      if (upcoming.isNotEmpty) const Text('Próximas', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
      for (final appointment in upcoming) appointmentCard(appointment),
      if (historic.isNotEmpty) const Text('Históricas', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
      for (final appointment in historic) appointmentCard(appointment),
      detailPanel(),
    ]);
  }
}
class _AppointmentMutationDialog extends StatefulWidget {
  final JsonMap appointment;
  final List<JsonMap> slots;
  final bool reschedule;
  final AsyncActionRunner run;
  final Future<void> Function(JsonMap) onCancel;
  final Future<void> Function(JsonMap, String) onReschedule;
  final Future<RescheduleOptions> Function(JsonMap) onReload;
  final Widget Function(Widget) guard;
  final bool Function() active;
  const _AppointmentMutationDialog({required this.appointment, required this.slots,
    required this.reschedule, required this.run, required this.onCancel, required this.onReschedule,
    required this.onReload, required this.guard, required this.active});
  @override
  State<_AppointmentMutationDialog> createState() => _AppointmentMutationDialogState();
}
class _AppointmentMutationDialogState extends State<_AppointmentMutationDialog> {
  late JsonMap base = Map<String, dynamic>.from(widget.appointment);
  late List<JsonMap> slots = widget.slots;
  String? chosen;
  bool sending = false, failed = false;
  Future<void> send() async {
    if (!widget.active() || sending || widget.reschedule && chosen == null) return;
    setState(() => sending = true);
    final saved = await widget.run(() => widget.reschedule ? widget.onReschedule(base, chosen!) : widget.onCancel(base));
    if (!mounted) return;
    if (saved) { Navigator.pop(context); return; }
    setState(() { sending = false; failed = true; });
  }
  Future<void> reload() async {
    final discard = await showDialog<bool>(context: context, builder: (context) => widget.guard(AlertDialog(
      title: const Text('¿Recargar y descartar la selección actual?'),
      content: const Text('Revisa la cita vigente y elige de nuevo antes de enviar. No se aplicará un cambio automáticamente.'),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Conservar selección')),
        FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Recargar')),
      ],
    )));
    if (discard != true || !mounted || !widget.active()) return;
    setState(() => sending = true);
    RescheduleOptions? options;
    final loaded = await widget.run(() async { options = await widget.onReload(base); });
    if (!mounted) return;
    setState(() {
      sending = false;
      if (loaded) { base = options!.appointment; slots = options!.slots; chosen = null; failed = false; }
    });
  }
  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(widget.reschedule ? 'Confirmar nuevo horario' : '¿Cancelar esta cita?'),
    content: SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text('${base['serviceName']} · ${base['practitionerName']}'),
      Text(localTime(base['startAt'] as String)),
      if (widget.reschedule) ...[
        const Text('El nuevo horario volverá a revisión manual. Solo se ofrecen horarios del mismo servicio.'),
        if (slots.isEmpty) const Text('Sin horarios disponibles.'),
        for (final slot in slots) RadioListTile<String>(
          value: slot['id'] as String, groupValue: chosen,
          onChanged: sending ? null : (value) => setState(() => chosen = value),
          title: Text('${localTime(slot['startAt'] as String)} · ${slot['practitionerName']}'),
        ),
      ],
      if (failed) const Text('No se pudo confirmar el cambio. Conservamos la selección y la versión original. Ante conflicto o resultado incierto, recarga explícitamente para comprobar el estado.'),
    ])),
    actions: [
      TextButton(onPressed: sending ? null : () => Navigator.pop(context), child: const Text('Volver')),
      TextButton(onPressed: sending ? null : reload, child: const Text('Recargar cita y horarios')),
      FilledButton(onPressed: sending || widget.reschedule && chosen == null ? null : send,
        child: Text(widget.reschedule ? 'Confirmar reprogramación' : 'Confirmar cancelación')),
    ],
  );
}
