class ApiFailure implements Exception {
  final int status;
  final String message;
  final String? code;
  final List<String> errors;
  ApiFailure(this.status, this.message, {this.code, this.errors = const []});
  @override
  String toString() => message;
}
