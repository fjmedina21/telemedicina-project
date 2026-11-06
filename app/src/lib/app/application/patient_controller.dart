import 'package:flutter/foundation.dart';

import '../../core/network/api_client.dart';
import '../../features/auth/data/auth_repository.dart';
import '../../features/appointments/data/appointment_repository.dart';
import '../../features/appointments/domain/reschedule_options.dart';
import '../../features/patient_profile/data/profile_repository.dart';
import '../../features/catalog/data/catalog_repository.dart';

class PatientController extends ChangeNotifier {
  final ApiClient api;
  late final AuthRepository auth = AuthRepository(api);
  bool busy = false;
  bool _disposed = false;
  int _privateGeneration = 0;
  String message = '';
  JsonMap? identity;
  JsonMap? profile;
  int profileVersion = 0;
  late final ProfileRepository profileRepository = ProfileRepository(api);
  JsonMap profileDraft = ProfileRepository.draft(null);
  int profileDraftVersion = 0;
  int profileDraftRevision = 0;
  bool profileDirty = false;
  bool profileConflict = false;
  Map<String, String> profileErrors = {};
  Map<String, DeclarationMetadata> declarationMetadata = {};
  ProfileCompleteness profileCompleteness = const ProfileCompleteness(
    'Incomplete',
    [],
  );
  bool get profileChangedElsewhere =>
      profileDirty && profileVersion != profileDraftVersion;
  int preferencesVersion = 0;
  bool emailEnabled = false;
  String? selectedServiceId;
  String? selectedSpecialty, selectedPractitionerId;
  late final CatalogRepository catalogRepository = CatalogRepository(api);
  List<String> specialties = [];
  List<JsonMap> practitioners = [];
  late String catalogFromDate, catalogToDate;
  String bookingInsurerDraft = '', bookingMemberDraft = '';
  String catalogError = '', availabilityError = '';
  bool practitionersLoading = false;
  CatalogAvailabilityState availabilityState = CatalogAvailabilityState.notQueried;
  int _catalogLoadRevision = 0, _practitionerRevision = 0, _availabilityRevision = 0;
  List<JsonMap> get filteredServices => services.where((service) =>
    selectedSpecialty == null || service['specialty'] == selectedSpecialty).toList();
  List<JsonMap> services = [],
      slots = [],
      appointments = [],
      notifications = [];
  late final AppointmentRepository appointmentRepository = AppointmentRepository(api);
  PendingBookingIntent? pendingBooking;
  bool bookingNeedsProfile = false;
  AppointmentFilters appointmentFilters = const AppointmentFilters();
  JsonMap? appointmentDetail;
  CheckInRequirements? appointmentCheckInRequirements;
  List<JsonMap> appointmentHistory = [];
  bool appointmentsLoading = false, appointmentDetailLoading = false;
  String appointmentsError = '', appointmentDetailError = '';
  int _appointmentsRevision = 0, _detailRevision = 0;
  bool get canRequestAppointment => profileCompleteness.status == 'Complete';
  void abandonBooking() {
    if (busy) return;
    pendingBooking = null;
    _notify();
  }
  PatientController(this.api) {
    _privateGeneration = api.sessionGeneration;
    _defaultCatalogDates();
  }

  void _defaultCatalogDates() {
    final now = DateTime.now().toUtc();
    catalogFromDate = CatalogDateRange.civilDate(now);
    catalogToDate = CatalogDateRange.civilDate(now.add(const Duration(days: 14)));
  }

  void _invalidateAvailability() {
    _availabilityRevision++;
    slots = [];
    availabilityState = CatalogAvailabilityState.notQueried;
    availabilityError = '';
  }

  bool _currentSession(int generation) => !_disposed && authenticated && generation == api.sessionGeneration;

  bool get authenticated => api.authenticated;
  void setMessage(String value) {
    message = value;
    _notify();
  }

  List<JsonMap> _records(dynamic value) => (value as List)
      .map((item) => Map<String, dynamic>.from(item as Map))
      .toList();
  void _syncSession() {
    if (_privateGeneration != api.sessionGeneration) _clearPrivateState();
  }
  void _notify() {
    _syncSession();
    if (!_disposed) {
      notifyListeners();
    }
  }

  void _clearPrivateState() {
    _privateGeneration = api.sessionGeneration;
    identity = null;
    profile = null;
    profileVersion = 0;
    profileDraft = ProfileRepository.draft(null);
    profileDraftVersion = 0;
    profileDraftRevision++;
    profileDirty = false;
    profileConflict = false;
    profileErrors = {};
    declarationMetadata = {};
    profileCompleteness = const ProfileCompleteness('Incomplete', []);
    preferencesVersion = 0;
    emailEnabled = false;
    services = [];
    slots = [];
    appointments = [];
    notifications = [];
    selectedServiceId = null;
    selectedSpecialty = null;
    selectedPractitionerId = null;
    specialties = [];
    practitioners = [];
    practitionersLoading = false;
    catalogError = '';
    bookingInsurerDraft = '';
    bookingMemberDraft = '';
    _catalogLoadRevision++;
    _practitionerRevision++;
    _defaultCatalogDates();
    _invalidateAvailability();
    pendingBooking = null;
    bookingNeedsProfile = false;
    appointmentFilters = const AppointmentFilters();
    appointmentDetail = null;
    appointmentCheckInRequirements = null;
    appointmentHistory = [];
    appointmentsLoading = false;
    appointmentDetailLoading = false;
    appointmentsError = '';
    appointmentDetailError = '';
    _appointmentsRevision++;
    _detailRevision++;
  }

  Future<bool> execute(Future<void> Function() action) async {
    if (busy || _disposed) return false;
    final generation = api.sessionGeneration;
    final hadSession = authenticated;
    busy = true;
    message = '';
    _notify();
    try {
      await action();
      return true;
    } catch (error) {
      if (_disposed) return false;
      if (hadSession && generation != api.sessionGeneration) {
        // A rejected/expired session is different from a response belonging to
        // a newer login: clear private data and leave an actionable message.
        if (!authenticated) message = 'La sesión ya no está disponible. Inicia sesión nuevamente.';
        return false;
      }
      message = error is ApiFailure && error.code == 'VALIDATION_FAILED' && error.errors.isNotEmpty
          ? _bookingValidationMessage(error.errors)
          : error.toString();
      return false;
    } finally {
      busy = false;
      if (!authenticated) _clearPrivateState();
      _notify();
    }
  }

  String _bookingValidationMessage(List<String> errors) {
    const fields = {
      'slotId': 'horario seleccionado',
      'insurer': 'aseguradora para revisión manual',
      'memberLast4': 'últimos cuatro dígitos de afiliación',
    };
    final invalid = errors.map((error) => error.split(':').first.split('.').last)
        .map((field) => fields[field]).whereType<String>().toSet();
    return invalid.isEmpty
        ? 'Revisa los datos de la solicitud de cita.'
        : 'Revisa: ${invalid.join(', ')}.';
  }

  Future<void> load() async {
    _syncSession();
    final generation = api.sessionGeneration;
    final catalogRevision = ++_catalogLoadRevision;
    final appointmentRevision = ++_appointmentsRevision;
    final me = Map<String, dynamic>.from(
      await api.request('GET', '/auth/me') as Map,
    );
    if (!_currentSession(generation)) return;
    if (me['role'] != 'Patient') {
      try {
        await auth.logout();
      } finally {
        _clearPrivateState();
      }
      throw ApiFailure(403, 'Utiliza una cuenta de paciente en esta app.');
    }
    final results = await Future.wait([
      profileRepository.load(),
      catalogRepository.catalog(),
      appointmentRepository.list(appointmentFilters),
      api.request('GET', '/notifications/me'),
      api.request('GET', '/notifications/preferences'),
    ]);
    if (_disposed || !authenticated || generation != api.sessionGeneration)
      return;
    identity = me;
    _acceptProfile(results[0] as ProfileSnapshot);
    if (catalogRevision == _catalogLoadRevision) {
      _acceptCatalog(results[1] as CatalogOptions);
    }
    if (appointmentRevision == _appointmentsRevision) {
      appointments = (results[2] as List<AppointmentRecord>).map((item) => item.toJson()).toList();
      appointmentsError = '';
    }
    notifications = _records(results[3]);
    final preferences = results[4] as Map;
    emailEnabled = preferences['emailEnabled'] as bool;
    preferencesVersion = preferences['version'] as int;
    if (catalogRevision == _catalogLoadRevision) {
      await refreshPractitioners();
    }
  }

  Future<void> login(String email, String password) async {
    _clearPrivateState();
    await auth.login(email, password);
    await load();
  }

  Future<void> logout() async {
    try {
      await auth.logout();
    } finally {
      _clearPrivateState();
    }
  }

  void updateProfileDraft(JsonMap body) {
    profileDraft = ProfileRepository.draft(body);
    profileDirty = true;
    profileErrors = {};
  }

  void _acceptProfile(ProfileSnapshot snapshot, {bool discardDraft = false}) {
    profile = snapshot.profile;
    profileVersion = snapshot.version;
    profileCompleteness = snapshot.completeness;
    if (!profileDirty || discardDraft) {
      profileDraft = ProfileRepository.draft(snapshot.profile);
      profileDraftVersion = snapshot.version;
      profileDraftRevision++;
      profileDirty = false;
      profileConflict = false;
      profileErrors = {};
      declarationMetadata = snapshot.metadata;
    }
  }

  Future<void> reloadProfile() async {
    final generation = api.sessionGeneration;
    final snapshot = await profileRepository.load();
    if (_disposed || !authenticated || generation != api.sessionGeneration)
      return;
    _acceptProfile(snapshot, discardDraft: true);
    message = 'Perfil recargado.';
  }

  Future<void> saveProfile(JsonMap body) async {
    updateProfileDraft(body);
    profileErrors = {};
    final generation = api.sessionGeneration;
    try {
      await profileRepository.save(profileDraft, profileDraftVersion);
      final snapshot = await profileRepository.load();
      if (_disposed || !authenticated || generation != api.sessionGeneration)
        return;
      _acceptProfile(snapshot, discardDraft: true);
      message = 'Perfil guardado.';
    } on ApiFailure catch (error) {
      if (!_disposed && authenticated && generation == api.sessionGeneration) {
        profileErrors = ProfileRepository.fieldErrors(error.errors);
        if (error.status == 409) profileConflict = true;
      }
      rethrow;
    }
  }

  void _acceptCatalog(CatalogOptions options) {
    specialties = options.specialties;
    services = options.services.map((item) => item.toJson()).toList();
    if (selectedSpecialty != null && !specialties.contains(selectedSpecialty)) {
      selectedSpecialty = null;
    }
    if (!filteredServices.any((item) => item['id'] == selectedServiceId)) {
      selectedServiceId = filteredServices.firstOrNull?['id'] as String?;
      selectedPractitionerId = null;
      practitioners = [];
    }
    catalogError = '';
    _invalidateAvailability();
  }

  Future<void> refreshCatalog() async {
    final generation = api.sessionGeneration;
    final revision = ++_catalogLoadRevision;
    _invalidateAvailability();
    _notify();
    try {
      final options = await catalogRepository.catalog();
      if (!_currentSession(generation) || revision != _catalogLoadRevision) return;
      _acceptCatalog(options);
      await refreshPractitioners();
    } on ApiFailure catch (error) {
      if (!_currentSession(generation) || revision != _catalogLoadRevision) return;
      catalogError = error.message;
      rethrow;
    } finally {
      if (_currentSession(generation) && revision == _catalogLoadRevision) _notify();
    }
  }

  Future<void> refreshPractitioners() async {
    final service = selectedServiceId;
    final generation = api.sessionGeneration;
    final revision = ++_practitionerRevision;
    practitionersLoading = service != null;
    catalogError = '';
    _notify();
    try {
      final values = service == null
          ? <CatalogPractitioner>[]
          : await catalogRepository.practitioners(service);
      if (!_currentSession(generation) || revision != _practitionerRevision || service != selectedServiceId) return;
      practitioners = values.map((item) => item.toJson()).toList();
      if (selectedPractitionerId != null && !practitioners.any((item) => item['id'] == selectedPractitionerId)) {
        selectedPractitionerId = null;
        _invalidateAvailability();
      }
    } on ApiFailure catch (error) {
      if (!_currentSession(generation) || revision != _practitionerRevision || service != selectedServiceId) return;
      catalogError = error.message;
      rethrow;
    } finally {
      if (_currentSession(generation) && revision == _practitionerRevision) {
        practitionersLoading = false;
        _notify();
      }
    }
  }

  Future<void> selectSpecialty(String? specialty) async {
    if (specialty != null && !specialties.contains(specialty)) {
      throw ApiFailure(400, 'Selecciona una especialidad disponible.');
    }
    if (specialty == selectedSpecialty) return;
    selectedSpecialty = specialty;
    selectedServiceId = filteredServices.firstOrNull?['id'] as String?;
    selectedPractitionerId = null;
    practitioners = [];
    _invalidateAvailability();
    _notify();
    await refreshPractitioners();
  }

  Future<void> selectService(String? id) async {
    if (id != null && !filteredServices.any((item) => item['id'] == id)) {
      throw ApiFailure(400, 'Selecciona un servicio disponible.');
    }
    if (id == selectedServiceId) return;
    selectedServiceId = id;
    selectedPractitionerId = null;
    practitioners = [];
    _invalidateAvailability();
    _notify();
    await refreshPractitioners();
  }

  void selectPractitioner(String? id) {
    if (id != null && !practitioners.any((item) => item['id'] == id)) {
      throw ApiFailure(400, 'Selecciona un profesional asociado al servicio.');
    }
    if (id == selectedPractitionerId) return;
    selectedPractitionerId = id;
    _invalidateAvailability();
    _notify();
  }

  void updateAvailabilityRange(String from, String to) {
    if (from == catalogFromDate && to == catalogToDate) return;
    catalogFromDate = from;
    catalogToDate = to;
    _invalidateAvailability();
    _notify();
  }

  void updateBookingDraft(String insurer, String member) {
    bookingInsurerDraft = insurer;
    bookingMemberDraft = member;
  }

  Future<void> availability() async {
    final service = selectedServiceId;
    if (service == null) return;
    final generation = api.sessionGeneration;
    final revision = ++_availabilityRevision;
    slots = [];
    availabilityError = '';
    availabilityState = CatalogAvailabilityState.loading;
    _notify();
    try {
      final values = await catalogRepository.availability(serviceId: service,
        practitionerId: selectedPractitionerId, fromDate: catalogFromDate, toDate: catalogToDate);
      if (!_currentSession(generation) || revision != _availabilityRevision) return;
      slots = values.map((item) => item.toJson()).toList();
      availabilityState = slots.isEmpty ? CatalogAvailabilityState.empty : CatalogAvailabilityState.results;
    } on ApiFailure catch (error) {
      if (!_currentSession(generation) || revision != _availabilityRevision) return;
      availabilityError = error.message;
      availabilityState = CatalogAvailabilityState.error;
      rethrow;
    } finally {
      if (_currentSession(generation) && revision == _availabilityRevision) _notify();
    }
  }

  Future<void> refreshAppointments({AppointmentFilters? filters}) async {
    _syncSession();
    final chosen = filters ?? appointmentFilters;
    chosen.query(); // Validate before discarding visible results.
    appointmentFilters = chosen;
    final generation = api.sessionGeneration;
    final revision = ++_appointmentsRevision;
    appointmentsLoading = true;
    appointmentsError = '';
    _notify();
    try {
      final values = await appointmentRepository.list(chosen);
      if (!_currentSession(generation) || revision != _appointmentsRevision) return;
      appointments = values.map((item) => item.toJson()).toList();
    } on ApiFailure catch (error) {
      if (_currentSession(generation) && revision == _appointmentsRevision) appointmentsError = error.message;
      rethrow;
    } finally {
      if (_currentSession(generation) && revision == _appointmentsRevision) {
        appointmentsLoading = false;
        _notify();
      }
    }
  }

  Future<void> openAppointment(JsonMap appointment) async {
    _syncSession();
    final generation = api.sessionGeneration;
    final revision = ++_detailRevision;
    final id = appointment['id'] as String;
    // Clear a different resource immediately. Preserve the same detail on retry.
    if (appointmentDetail?['id'] != id) {
      appointmentDetail = null;
      appointmentCheckInRequirements = null;
      appointmentHistory = [];
    }
    appointmentDetailLoading = true;
    appointmentDetailError = '';
    _notify();
    try {
      final value = await appointmentRepository.snapshot(id);
      if (!_currentSession(generation) || revision != _detailRevision) return;
      appointmentDetail = value.appointment.toJson();
      appointmentCheckInRequirements = value.checkInRequirements;
      appointmentHistory = value.history.map((item) => item.toJson()).toList();
    } on ApiFailure catch (error) {
      if (_currentSession(generation) && revision == _detailRevision) appointmentDetailError = error.message;
      rethrow;
    } finally {
      if (_currentSession(generation) && revision == _detailRevision) {
        appointmentDetailLoading = false;
        _notify();
      }
    }
  }

  Future<void> _refreshAppointmentChange(int generation, String? id) async {
    try {
      await refreshAppointments();
      if (!_currentSession(generation)) return;
      if (id != null && appointmentDetail?['id'] == id) await openAppointment({'id': id});
    } catch (_) {
      if (_currentSession(generation)) message = 'El cambio quedó guardado. Actualiza para consultar el estado vigente.';
    }
  }

  Future<void> book(JsonMap slot, String insurer, String member) async {
    _syncSession();
    final generation = api.sessionGeneration;
    if (pendingBooking == null && !canRequestAppointment) {
      bookingNeedsProfile = true;
      throw ApiFailure(400, 'Completa Mi perfil antes de solicitar.', code: 'PROFILE_INCOMPLETE');
    }
    final previous = pendingBooking;
    if (previous != null && !previous.matches(slot, insurer, member)) {
      throw ApiFailure(409, 'Hay una solicitud pendiente de comprobar. Reinténtala o confirma que abandonas esa intención antes de cambiar los datos.');
    }
    final intent = pendingBooking ??= PendingBookingIntent(slot, insurer, member);
    try {
      await appointmentRepository.book(intent);
    } on ApiFailure catch (error) {
      if (_currentSession(generation) && ['PROFILE_REQUIRED', 'PROFILE_INCOMPLETE'].contains(error.code)) {
        bookingNeedsProfile = true;
        profileErrors = ProfileRepository.fieldErrors(error.errors);
        profileCompleteness = ProfileCompleteness('Incomplete', profileErrors.keys.toList());
      }
      rethrow;
    }
    if (!_currentSession(generation)) return;
    if (identical(pendingBooking, intent)) pendingBooking = null;
    bookingNeedsProfile = false;
    message = 'Solicitud guardada. Pendiente de validación; cobertura en revisión manual.';
    await _refreshAppointmentChange(generation, null);
    if (_currentSession(generation)) {
      try { await availability(); } catch (_) {
        if (_currentSession(generation)) message = 'Solicitud guardada. Actualiza los horarios antes de otra solicitud.';
      }
    }
  }

  Future<void> cancel(JsonMap appointment) async {
    final generation = api.sessionGeneration;
    await appointmentRepository.cancel(appointment);
    if (!_currentSession(generation)) return;
    message = 'Cancelación guardada.';
    await _refreshAppointmentChange(generation, appointment['id'] as String);
  }

  Future<RescheduleOptions> rescheduleOptions(JsonMap appointment) async {
    final generation = api.sessionGeneration;
    final current = Map<String, dynamic>.from(await api.request('GET', '/appointments/${appointment['id']}') as Map);
    if (!_currentSession(generation)) throw ApiFailure(401, 'La sesión cambió.');
    final available = _records(await api.request('GET', '/availability?serviceId=${Uri.encodeQueryComponent(current['serviceId'] as String)}'));
    if (!_currentSession(generation)) throw ApiFailure(401, 'La sesión cambió.');
    return RescheduleOptions(current, available.where((item) =>
      item['serviceId'] == current['serviceId'] && item['id'] != current['slotId']).toList());
  }

  Future<void> reschedule(JsonMap appointment, String slotId) async {
    final generation = api.sessionGeneration;
    await appointmentRepository.reschedule(appointment, slotId);
    if (!_currentSession(generation)) return;
    message = 'Reprogramación guardada. La cita volvió a revisión manual.';
    await _refreshAppointmentChange(generation, appointment['id'] as String);
  }

  Future<void> setEmailEnabled(bool enabled) async {
    await api.request(
      'PATCH',
      '/notifications/preferences',
      body: {'emailEnabled': enabled, 'version': preferencesVersion},
    );
    await load();
  }

  Future<void> markRead(JsonMap notification) async {
    await api.request(
      'POST',
      '/notifications/${notification['id']}/read',
      body: {},
    );
    await load();
  }

  @override
  void dispose() {
    _disposed = true;
    _clearPrivateState();
    api.close();
    super.dispose();
  }
}
