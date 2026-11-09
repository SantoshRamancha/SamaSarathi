import 'package:dio/dio.dart';

/// An error the UI can show as-is.
class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  bool get isNotFound => statusCode == 404;
  bool get isUnauthorized => statusCode == 401;

  /// Reads the backend's ProblemDetails ("detail", or the first validation error) when present.
  factory ApiException.fromDio(DioException e) {
    final response = e.response;
    if (response == null) {
      return const ApiException("Can't reach the server. Check your internet connection.");
    }

    final data = response.data;
    String? message;
    if (data is Map) {
      message = data['detail'] as String?;
      final errors = data['errors'];
      if (message == null && errors is Map && errors.isNotEmpty) {
        final first = errors.values.first;
        message = first is List && first.isNotEmpty ? first.first.toString() : first.toString();
      }
      message ??= data['title'] as String?;
    }
    return ApiException(message ?? 'Something went wrong (${response.statusCode}).',
        statusCode: response.statusCode);
  }

  @override
  String toString() => message;
}
