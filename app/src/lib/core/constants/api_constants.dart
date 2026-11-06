class ApiConstants {
  ApiConstants._();

  // Android Emulator usa 10.0.2.2; iOS Simulator puede usar localhost.
  static const String baseUrl = 'http://127.0.0.1:5000/api/v1';
  static const String healthEndpoint = '/health';
  static const String authLogin = '/auth/login';
  static const String authRegister = '/auth/register';
  static const String authRefresh = '/auth/refresh';
  static const String authMe = '/auth/me';
  static const String appointments = '/appointments';
  static const String patientProfile = '/patients/me';
  static const String documents = '/documents';
}
