import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../../core/network/api_client.dart';
import '../../features/auth/data/auth_repository.dart';
import '../../features/appointments/domain/reschedule_options.dart';

class PatientController extends ChangeNotifier {
  final ApiClient api;
  late final AuthRepository auth = AuthRepository(api);
  bool busy = false;
  bool _disposed = false;
  String message = '';
  JsonMap? profile;
  int profileVersion = 0;
  int preferencesVersion = 0;
  bool emailEnabled = false;
  String? selectedServiceId;
  List<JsonMap> services = [],
      slots = [],
      appointments = [],
      notifications = [];
  final _bookingKeys = <String, String>{};
  PatientController(this.api);

  bool get authenticated => api.authenticated;
  void setMessage(String value) {
    message = value;
    _notify();
  }

  List<JsonMap> _records(dynamic value) => (value as List)
      .map((item) => Map<String, dynamic>.from(item as Map))
      .toList();
  void _notify() {
    if (!_disposed) {
      notifyListeners();
    }
  }

  void _clearPrivateState() {
    profile = null;
    profileVersion = 0;
    preferencesVersion = 0;
    emailEnabled = false;
    services = [];
    slots = [];
    appointments = [];
    notifications = [];
    selectedServiceId = null;
    _bookingKeys.clear();
  }

  Future<bool> execute(Future<void> Function() action) async {
    if (busy || _disposed) return false;
    busy = true;
    message = '';
    _notify();
    try {
      await action();
      return true;
    } catch (error) {
      message = error.toString();
      return false;
    } finally {
      busy = false;
      if (!authenticated) _clearPrivateState();
      _notify();
    }
  }

  Future<void> load() async {
    final results = await Future.wait([
      api.request('GET', '/patients/me'),
      api.request('GET', '/services'),
      api.request('GET', '/appointments/me'),
      api.request('GET', '/notifications/me'),
      api.request('GET', '/notifications/preferences'),
    ]);
    if (_disposed) return;
    final data = results[0] as Map?;
    profile = data == null
        ? null
        : Map<String, dynamic>.from(data['profile'] as Map);
    profileVersion = data?['version'] as int? ?? 0;
    services = _records(results[1]);
    appointments = _records(results[2]);
    notifications = _records(results[3]);
    final preferences = results[4] as Map;
    emailEnabled = preferences['emailEnabled'] as bool;
    preferencesVersion = preferences['version'] as int;
    if (!services.any((item) => item['id'] == selectedServiceId)) {
      selectedServiceId = services.firstOrNull?['id'] as String?;
    }
  }

  Future<void> login(String email, String password) async {
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

  Future<void> saveProfile(JsonMap body) async {
    await api.request(
      'PATCH',
      '/patients/me',
      body: {...body, 'version': profileVersion},
    );
    await load();
    message = 'Perfil guardado.';
  }

  void selectService(String? id) {
    selectedServiceId = id;
    slots = [];
    _notify();
  }

  Future<void> availability() async {
    if (selectedServiceId == null) return;
    slots = _records(
      await api.request('GET', '/availability?serviceId=$selectedServiceId'),
    );
  }

  Future<void> book(JsonMap slot, String insurer, String member) async {
    final payload = <String, dynamic>{
      'slotId': slot['id'],
      'insurer': insurer.trim(),
      'memberLast4': member.trim(),
    };
    final key = _bookingKeys.putIfAbsent(jsonEncode(payload), ApiClient.uuid);
    await api.request(
      'POST',
      '/appointments',
      body: payload,
      extraHeaders: {'Idempotency-Key': key},
    );
    await load();
    await availability();
    message = 'Solicitud guardada. Pendiente de validación; cobertura en revisión manual.';
  }

  Future<void> cancel(JsonMap appointment) async {
    await api.request(
      'POST',
      '/appointments/${appointment['id']}/cancel',
      body: {'version': appointment['version']},
    );
    await load();
  }

  Future<RescheduleOptions> rescheduleOptions(JsonMap appointment) async {
    final current = Map<String, dynamic>.from(
      await api.request('GET', '/appointments/${appointment['id']}') as Map,
    );
    if (!services.any((item) => item['id'] == current['serviceId'])) {
      throw ApiFailure(409, 'Servicio no disponible para reprogramar.');
    }
    final available = _records(
      await api.request(
        'GET',
        '/availability?serviceId=${current['serviceId']}',
      ),
    );
    return RescheduleOptions(current, available);
  }

  Future<void> reschedule(JsonMap appointment, String slotId) async {
    await api.request(
      'POST',
      '/appointments/${appointment['id']}/reschedule',
      body: {'slotId': slotId, 'version': appointment['version']},
    );
    await load();
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
    api.close();
    super.dispose();
  }
}
