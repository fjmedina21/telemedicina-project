import 'dart:convert';
import 'dart:io';

import '../constants/api_constants.dart';
import '../errors/app_exception.dart';
import '../models/api_response.dart';

/// Servicio de red abstracto con manejo de errores consistentes (RFC 7807)
class HttpClientService {
  final String baseUrl;

  HttpClientService({this.baseUrl = ApiConstants.baseUrl});

  Future<ApiResponse<T>> get<T>({
    required String path,
    Map<String, String>? headers,
    T Function(dynamic)? fromJsonT,
  }) async {
    try {
      final client = HttpClient();
      final uri = Uri.parse('$baseUrl$path');
      final request = await client.getUrl(uri);

      headers?.forEach((key, value) {
        request.headers.add(key, value);
      });
      request.headers.add('Accept', 'application/json');

      final response = await request.close();
      final responseBody = await response.transform(utf8.decoder).join();
      final jsonMap = jsonDecode(responseBody) as Map<String, dynamic>;

      if (response.statusCode >= 200 && response.statusCode < 300) {
        return ApiResponse.fromJson(jsonMap, fromJsonT);
      } else {
        throw _handleErrorResponse(response.statusCode, jsonMap);
      }
    } on SocketException {
      throw const NetworkException();
    } on FormatException {
      throw const AppException(
        message: 'Error al interpretar la respuesta del servidor.',
      );
    }
  }

  AppException _handleErrorResponse(int statusCode, Map<String, dynamic> json) {
    final title =
        json['title'] as String? ??
        json['detail'] as String? ??
        'Error desconocido';
    if (statusCode == 401) {
      return UnauthorizedException(message: title);
    } else if (statusCode == 409) {
      return ConflictException(message: title);
    } else if (statusCode == 400) {
      final errors =
          (json['errors'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          [];
      return ValidationException(message: title, errors: errors);
    }
    return AppException(message: title, statusCode: statusCode);
  }
}
