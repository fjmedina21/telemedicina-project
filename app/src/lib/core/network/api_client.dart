import 'dart:async';
import 'dart:convert';
import 'dart:math';

import 'package:http/http.dart' as http;

import '../errors/api_failure.dart';
import '../models/json_map.dart';
export '../errors/api_failure.dart';
export '../models/json_map.dart';

/// Tokens remain in process memory: no disk preferences, logs or URLs.
class ApiClient {
  final http.Client _client;
  final String baseUrl;
  String? _access;
  String? _refresh;
  Future<void>? _refreshing;
  int _sessionGeneration = 0;
  ApiClient({
    http.Client? client,
    this.baseUrl = const String.fromEnvironment(
      'API_BASE_URL',
      defaultValue: 'http://127.0.0.1:5000/api/v1',
    ),
  }) : _client = client ?? http.Client();

  bool get authenticated => _access != null;
  int get sessionGeneration => _sessionGeneration;
  void clearSession() {
    _sessionGeneration++;
    _access = null;
    _refresh = null;
  }

  void close() {
    clearSession();
    _client.close();
  }

  static String uuid() {
    final random = Random.secure();
    final bytes = List<int>.generate(16, (_) => random.nextInt(256));
    bytes[6] = (bytes[6] & 15) | 64;
    bytes[8] = (bytes[8] & 63) | 128;
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  Future<dynamic> request(
    String method,
    String path, {
    JsonMap? body,
    Map<String, String> extraHeaders = const {},
    bool retry = true,
  }) async {
    final generation = _sessionGeneration;
    try {
      final request = http.Request(method, Uri.parse('$baseUrl$path'));
      request.headers.addAll({
        'Content-Type': 'application/json',
        'X-Telemedicina-Client': 'milestone',
        if (_access != null) 'Authorization': 'Bearer $_access',
        ...extraHeaders,
      });
      if (body != null) request.body = jsonEncode(body);
      final response = await http.Response.fromStream(
        await _client.send(request).timeout(const Duration(seconds: 15)),
      ).timeout(const Duration(seconds: 15));
      if (generation != _sessionGeneration) {
        throw ApiFailure(
          401,
          'La sesión ya no está disponible. Inicia sesión nuevamente.',
        );
      }
      if (response.statusCode == 401 &&
          retry &&
          _refresh != null &&
          (path == '/auth/me' || !path.startsWith('/auth/'))) {
        _refreshing ??= _rotate().whenComplete(() => _refreshing = null);
        await _refreshing;
        return await this.request(
          method,
          path,
          body: body,
          extraHeaders: extraHeaders,
          retry: false,
        );
      }
      if (response.statusCode == 401) clearSession();
      final parsed = response.body.isEmpty
          ? <String, dynamic>{}
          : jsonDecode(response.body);
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw ApiFailure(
          response.statusCode,
          response.statusCode == 401 && path != '/auth/login'
              ? 'La sesión ya no está disponible. Inicia sesión nuevamente.'
              : response.statusCode == 403
              ? 'No tienes permiso para realizar esta acción.'
              : parsed is Map
              ? (parsed['title'] ?? parsed['detail'] ?? 'Solicitud rechazada')
                    .toString()
              : 'Solicitud rechazada (${response.statusCode})',
          code: parsed is Map ? parsed['code'] as String? : null,
          errors: parsed is Map && parsed['errors'] is List
              ? (parsed['errors'] as List).whereType<String>().toList()
              : const [],
        );
      }
      return parsed is Map ? parsed['data'] : null;
    } on ApiFailure {
      rethrow;
    } on TimeoutException {
      throw ApiFailure(503, 'La solicitud tardó demasiado. Puede reintentar.');
    } on FormatException {
      throw ApiFailure(502, 'Respuesta del servidor no válida.');
    } on http.ClientException {
      throw ApiFailure(503, 'No se pudo conectar con la API.');
    }
  }

  Future<void> login(String email, String password) async {
    clearSession();
    final data = await request(
      'POST',
      '/auth/login',
      body: {'email': email, 'password': password},
    );
    if (data['role'] != 'Patient') {
      _access = data['accessToken'];
      _refresh = data['refreshToken'];
      await logout();
      throw ApiFailure(403, 'Utilice una cuenta de paciente en esta app.');
    }
    _access = data['accessToken'];
    _refresh = data['refreshToken'];
  }

  Future<void> _rotate() async {
    final generation = _sessionGeneration;
    try {
      final data = await request(
        'POST',
        '/auth/refresh',
        body: {'refreshToken': _refresh},
        retry: false,
      );
      if (generation != _sessionGeneration) {
        throw ApiFailure(
          401,
          'La sesión ya no está disponible. Inicia sesión nuevamente.',
        );
      }
      _access = data['accessToken'];
      _refresh = data['refreshToken'];
    } catch (_) {
      if (generation == _sessionGeneration) clearSession();
      rethrow;
    }
  }

  Future<void> logout() async {
    try {
      await request('POST', '/auth/logout', body: {});
    } finally {
      clearSession();
    }
  }
}
