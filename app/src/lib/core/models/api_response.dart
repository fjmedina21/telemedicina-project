class ApiResponse<T> {
  final bool success;
  final int status;
  final String detail;
  final T? data;

  const ApiResponse({
    required this.success,
    required this.status,
    required this.detail,
    this.data,
  });

  factory ApiResponse.fromJson(
    Map<String, dynamic> json,
    T Function(dynamic json)? fromJsonT,
  ) {
    return ApiResponse<T>(
      success: json['success'] as bool? ?? false,
      status: json['status'] as int? ?? 500,
      detail: json['detail'] as String? ?? '',
      data: json['data'] != null && fromJsonT != null
          ? fromJsonT(json['data'])
          : null,
    );
  }
}

class ErrorApiResponse {
  final bool success;
  final int status;
  final String title;
  final String type;
  final String instance;
  final String? detail;
  final List<String> errors;

  const ErrorApiResponse({
    this.success = false,
    required this.status,
    required this.title,
    required this.type,
    required this.instance,
    this.detail,
    this.errors = const [],
  });

  factory ErrorApiResponse.fromJson(Map<String, dynamic> json) {
    return ErrorApiResponse(
      success: json['success'] as bool? ?? false,
      status: json['status'] as int? ?? 500,
      title: json['title'] as String? ?? 'Error en la solicitud',
      type: json['type'] as String? ?? '',
      instance: json['instance'] as String? ?? '',
      detail: json['detail'] as String?,
      errors:
          (json['errors'] as List<dynamic>?)
              ?.map((e) => e.toString())
              .toList() ??
          [],
    );
  }
}
