import '../../../core/network/api_client.dart';

class AuthRepository {
  final ApiClient api;
  AuthRepository(this.api);
  Future<void> login(String email, String password) =>
      api.login(email, password);
  Future<void> logout() => api.logout();
  Future<void> register(String email, String password) async {
    await api.request(
      'POST',
      '/auth/register',
      body: {'email': email, 'password': password},
    );
  }

  Future<void> verify(String email, String token) async {
    await api.request('POST', '/auth/verify', body: {
      'email': email, 'token': token.trim().toUpperCase(),
    });
  }

  Future<void> resend(String email) async {
    await api.request(
      'POST',
      '/auth/verification/resend',
      body: {'email': email},
    );
  }

  Future<void> forgot(String email) async {
    await api.request('POST', '/auth/password/forgot', body: {'email': email});
  }

  Future<void> reset(String token, String password) async {
    await api.request(
      'POST',
      '/auth/password/reset',
      body: {'token': token, 'password': password},
    );
  }
}
