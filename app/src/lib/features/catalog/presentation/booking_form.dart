import 'package:flutter/material.dart';

import '../../../core/models/json_map.dart';
import '../../../core/errors/api_failure.dart';
import '../../../shared/formatting/date_time.dart';
import '../data/catalog_repository.dart';
import '../../appointments/data/appointment_repository.dart';
import '../../appointments/presentation/appointment_session_guard.dart';

class BookingForm extends StatefulWidget {
  final List<JsonMap> services, slots;
  final List<String> specialties;
  final List<JsonMap> practitioners;
  final String? selectedServiceId, selectedSpecialty, selectedPractitionerId;
  final String fromDate, toDate, insurerDraft, memberDraft, availabilityError, catalogError;
  final bool busy, hasProfile, practitionersLoading;
  final CatalogAvailabilityState availabilityState;
  final ValueChanged<String?> onServiceChanged;
  final ValueChanged<String?>? onSpecialtyChanged, onPractitionerChanged;
  final void Function(String, String)? onRangeChanged, onDraftChanged;
  final VoidCallback onAvailability;
  final VoidCallback? onReloadCatalog;
  final Listenable? sessionChanges;
  final bool Function(int)? isSessionCurrent;
  final int sessionGeneration;
  final PendingBookingIntent? pendingBooking;
  final VoidCallback? onAbandonPending, onProfile;
  final Future<bool> Function(JsonMap, String, String) onBook;
  const BookingForm({
    super.key, required this.services, required this.slots,
    required this.selectedServiceId, required this.busy, required this.hasProfile,
    required this.onServiceChanged, required this.onAvailability, required this.onBook,
    this.specialties = const [], this.practitioners = const [], this.selectedSpecialty,
    this.selectedPractitionerId, this.fromDate = '', this.toDate = '', this.insurerDraft = '',
    this.memberDraft = '', this.availabilityError = '', this.catalogError = '',
    this.practitionersLoading = false, this.availabilityState = CatalogAvailabilityState.notQueried,
    this.onSpecialtyChanged, this.onPractitionerChanged, this.onRangeChanged,
    this.onDraftChanged, this.onReloadCatalog, this.pendingBooking, this.onAbandonPending, this.onProfile, this.sessionChanges, this.isSessionCurrent, this.sessionGeneration = 0,
  });
  @override
  State<BookingForm> createState() => _BookingFormState();
}

class _BookingFormState extends State<BookingForm> {
  late final insurer = TextEditingController(text: widget.insurerDraft);
  late final member = TextEditingController(text: widget.memberDraft);
  late final from = TextEditingController(text: widget.fromDate);
  late final to = TextEditingController(text: widget.toDate);
  String? rangeError;

  @override
  void didUpdateWidget(covariant BookingForm oldWidget) {
    super.didUpdateWidget(oldWidget);
    void sync(TextEditingController controller, String previous, String next) {
      if (previous != next && controller.text != next) controller.text = next;
    }
    sync(insurer, oldWidget.insurerDraft, widget.insurerDraft);
    sync(member, oldWidget.memberDraft, widget.memberDraft);
    sync(from, oldWidget.fromDate, widget.fromDate);
    sync(to, oldWidget.toDate, widget.toDate);
  }

  @override
  void dispose() {
    insurer.dispose(); member.dispose(); from.dispose(); to.dispose();
    super.dispose();
  }

  void rangeChanged(String _) {
    setState(() => rangeError = null);
    widget.onRangeChanged?.call(from.text, to.text);
  }

  void consult() {
    try {
      CatalogDateRange.fromCivilDates(from.text, to.text);
      setState(() => rangeError = null);
      widget.onAvailability();
    } on ApiFailure catch (error) {
      setState(() => rangeError = error.message);
    }
  }

  Widget guardDialog(Widget child, int generation) => AppointmentSessionGuard(
    changes: widget.sessionChanges,
    isCurrent: () => mounted && generation == widget.sessionGeneration && (widget.isSessionCurrent?.call(generation) ?? true),
    child: child,
  );
  Future<void> submit(JsonMap slot, String insurerValue, String memberValue) async {
    final generation = widget.sessionGeneration;
    if (insurerValue.isEmpty || insurerValue.length > 100 || !RegExp(r'^[0-9]{4}$').hasMatch(memberValue)) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Indica aseguradora (máximo 100 caracteres) y cuatro dígitos de afiliación.')));
      return;
    }
    final pending = widget.pendingBooking;
    if (pending != null && !pending.matches(slot, insurerValue, memberValue)) {
      final abandon = await showDialog<bool>(context: context, builder: (dialog) => guardDialog(AlertDialog(
        title: const Text('¿Abandonar la intención pendiente?'),
        content: const Text('La solicitud anterior pudo llegar al servidor. Consulta Mis citas antes de continuar. Abandonar esta intención no cancela una cita ya creada.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(dialog, false), child: const Text('Conservar pendiente')),
          FilledButton(onPressed: () => Navigator.pop(dialog, true), child: const Text('Abandonar intención')),
        ],
      ), generation));
      if (abandon != true || !mounted || widget.onAbandonPending == null) return;
      widget.onAbandonPending!();
    }
    if (!mounted) return;
    final duration = DateTime.parse(slot['endAt'] as String).difference(DateTime.parse(slot['startAt'] as String)).inMinutes;
    final confirmed = await showDialog<bool>(context: context, builder: (dialog) => guardDialog(AlertDialog(
      title: const Text('Revisar solicitud'),
      content: SingleChildScrollView(child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
        Text(slot['serviceName'] as String? ?? 'Servicio seleccionado'),
        Text('Duración: $duration minutos'),
        Text(slot['practitionerName'] as String),
        Text(localTime(slot['startAt'] as String)),
        Text('Aseguradora: $insurerValue'),
        Text('Afiliación: ••••$memberValue'),
        const SizedBox(height: 12),
        const Text('Se enviará una solicitud para revisión manual. Esto no confirma la cita ni constituye autorización recibida de ARS o determinación de copago.'),
      ])),
      actions: [
        TextButton(onPressed: () => Navigator.pop(dialog, false), child: const Text('Volver')),
        FilledButton(onPressed: () => Navigator.pop(dialog, true), child: const Text('Confirmar solicitud para revisión')),
      ],
    ), generation));
    if (confirmed == true && mounted && !widget.busy && generation == widget.sessionGeneration && (widget.isSessionCurrent?.call(generation) ?? true)) await widget.onBook(slot, insurerValue, memberValue);
  }

  Widget selector({required String label, required String? value,
    required List<DropdownMenuItem<String>> items, required ValueChanged<String?>? onChanged}) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 8),
    child: InputDecorator(decoration: InputDecoration(labelText: label),
      child: DropdownButtonHideUnderline(child: DropdownButton<String>(
        value: value, isExpanded: true, items: items,
        onChanged: widget.busy ? null : onChanged,
      )),
    ),
  );

  @override
  Widget build(BuildContext context) {
    final service = widget.services.any((item) => item['id'] == widget.selectedServiceId)
        ? widget.selectedServiceId : null;
    final person = widget.practitioners.any((item) => item['id'] == widget.selectedPractitionerId)
        ? widget.selectedPractitionerId : null;
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      const Text('Solicitar cita', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold)),
      const SizedBox(height: 12),
      const Text('Consulta los servicios habilitados para atención remota. Si necesitas atención presencial, consulta las opciones del centro.'),
      if (!widget.hasProfile) ...[
        const Text('Completa primero tu perfil administrativo.'),
        if (widget.onProfile != null) TextButton(onPressed: widget.busy ? null : widget.onProfile, child: const Text('Ir a Mi perfil')),
      ],
      if (widget.pendingBooking != null) ...[
        const Text('Hay una intención pendiente de comprobar. El reintento conserva exactamente sus datos y su clave, aunque hayas cambiado los filtros.'),
        TextButton(onPressed: widget.busy ? null : () {
          final pending = widget.pendingBooking!;
          submit(pending.slot, pending.body['insurer'] as String, pending.body['memberLast4'] as String);
        }, child: const Text('Reintentar solicitud pendiente')),
      ],
      selector(label: 'Especialidad', value: widget.selectedSpecialty ?? '', items: [
        const DropdownMenuItem(value: '', child: Text('Todas las especialidades')),
        for (final item in widget.specialties) DropdownMenuItem(value: item, child: Text(item, overflow: TextOverflow.ellipsis)),
      ], onChanged: widget.onSpecialtyChanged == null ? null : (value) => widget.onSpecialtyChanged!(value == '' ? null : value)),
      if (widget.services.isEmpty) const Text('No hay servicios disponibles. Puede actualizar más tarde.'),
      if (widget.services.isNotEmpty) selector(label: 'Servicio', value: service, items: [
        for (final item in widget.services) DropdownMenuItem(value: item['id'] as String,
          child: Text('${item['name']} · ${item['durationMinutes']} min', overflow: TextOverflow.ellipsis)),
      ], onChanged: widget.onServiceChanged),
      selector(label: 'Profesional asociado', value: person ?? '', items: [
        const DropdownMenuItem(value: '', child: Text('Cualquier profesional asociado')),
        for (final item in widget.practitioners) DropdownMenuItem(value: item['id'] as String,
          child: Text(item['name'] as String, overflow: TextOverflow.ellipsis)),
      ], onChanged: widget.practitionersLoading || service == null || widget.onPractitionerChanged == null
          ? null : (value) => widget.onPractitionerChanged!(value == '' ? null : value)),
      if (widget.practitionersLoading) Semantics(liveRegion: true, child: const Text('Cargando profesionales asociados…')),
      if (widget.catalogError.isNotEmpty) ...[
        Semantics(liveRegion: true, child: Text(widget.catalogError)),
        if (widget.onReloadCatalog != null) TextButton(onPressed: widget.busy ? null : widget.onReloadCatalog,
          child: const Text('Reintentar catálogo')),
      ],
      const SizedBox(height: 8),
      TextField(key: const ValueKey('catalog-from'), controller: from, enabled: !widget.busy,
        keyboardType: TextInputType.datetime, textInputAction: TextInputAction.next,
        decoration: const InputDecoration(labelText: 'Desde (AAAA-MM-DD, Santo Domingo)'), onChanged: rangeChanged),
      const SizedBox(height: 12),
      TextField(key: const ValueKey('catalog-to'), controller: to, enabled: !widget.busy,
        keyboardType: TextInputType.datetime, textInputAction: TextInputAction.next,
        decoration: const InputDecoration(labelText: 'Hasta (excluyente, AAAA-MM-DD, Santo Domingo)'), onChanged: rangeChanged),
      if (rangeError != null) Semantics(liveRegion: true, child: Text(rangeError!)),
      const SizedBox(height: 12),
      const Text('Consulta un rango de hasta 31 días. Se muestran hasta 300 horarios; reduce el rango o filtra por profesional para acotar resultados.'),
      FilledButton(onPressed: widget.busy || service == null || widget.practitionersLoading ? null : consult,
        child: const Text('Consultar horarios')),
      if (widget.availabilityState == CatalogAvailabilityState.notQueried)
        const Text('Selecciona los filtros y consulta los horarios.'),
      if (widget.availabilityState == CatalogAvailabilityState.loading)
        Semantics(liveRegion: true, child: const Text('Consultando horarios…')),
      if (widget.availabilityState == CatalogAvailabilityState.empty)
        Semantics(liveRegion: true, child: const Text('No hay horarios disponibles para estos filtros. Prueba con otro rango o profesional.')),
      if (widget.availabilityState == CatalogAvailabilityState.error)
        Semantics(liveRegion: true, child: Text(widget.availabilityError.isEmpty
          ? 'No se pudieron consultar los horarios. Conservamos tus filtros para reintentar.' : widget.availabilityError)),
      const SizedBox(height: 12),
      TextField(key: const ValueKey('booking-insurer'), controller: insurer, enabled: !widget.busy,
        decoration: const InputDecoration(labelText: 'Aseguradora para revisión manual'),
        onChanged: (_) => widget.onDraftChanged?.call(insurer.text, member.text)),
      const SizedBox(height: 12),
      TextField(key: const ValueKey('booking-member'), controller: member, enabled: !widget.busy,
        decoration: const InputDecoration(labelText: 'Últimos 4 dígitos de afiliación'),
        onChanged: (_) => widget.onDraftChanged?.call(insurer.text, member.text)),
      const SizedBox(height: 12),
      const Text('La disponibilidad no garantiza reserva. No se confirma cobertura ni copago sin revisión.'),
      const SizedBox(height: 20),
      for (final slot in widget.slots) Card(child: Padding(padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(localTime(slot['startAt'] as String), style: const TextStyle(fontWeight: FontWeight.w600)),
          const SizedBox(height: 4), Text(slot['practitionerName'] as String), const SizedBox(height: 12),
          FilledButton(onPressed: widget.busy || !widget.hasProfile ? null : () => submit(slot, insurer.text.trim(), member.text.trim()),
            child: const Text('Solicitar')),
        ]),
      )),
    ]);
  }
}
