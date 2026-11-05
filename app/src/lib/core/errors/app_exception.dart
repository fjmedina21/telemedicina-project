class AppException implements Exception {
  final String message;
  final int? statusCode;
  final String? code;

  const AppException({required this.message, this.statusCode, this.code});

  @override
  String toString() =>
      'AppException: $message (code: $code, status: $statusCode)';
}

class NetworkException extends AppException {
  const NetworkException({
    super.message = 'Sin conexión a internet. Verifique su red.',
  }) : super(code: 'NETWORK_ERROR');
}

class UnauthorizedException extends AppException {
  const UnauthorizedException({
    super.message = 'Sesión expirada o credenciales incorrectas.',
  }) : super(statusCode: 401, code: 'UNAUTHORIZED');
}

class ConflictException extends AppException {
  const ConflictException({
    super.message = 'El horario seleccionado ya no está disponible.',
  }) : super(statusCode: 409, code: 'CONFLICT');
}

class ValidationException extends AppException {
  final List<String> errors;

  const ValidationException({required super.message, this.errors = const []})
    : super(statusCode: 400, code: 'VALIDATION_ERROR');
}
