import 'package:dio/dio.dart';

import '../auth/auth_models.dart';
import '../models/fare_quote.dart';
import 'api_config.dart';
import 'api_exception.dart';

/// Single entry point for talking to the C# backend.
/// App-specific APIs (DriverApi, RiderApi) build on [dio] and [send].
class ApiClient {
  ApiClient({Dio? dio, this.token})
      : dio = dio ??
            Dio(BaseOptions(
              baseUrl: ApiConfig.baseUrl,
              connectTimeout: const Duration(seconds: 10),
              receiveTimeout: const Duration(seconds: 30),
            )) {
    // Attach the login token to every request.
    this.dio.interceptors.add(InterceptorsWrapper(onRequest: (options, handler) {
      final current = token;
      if (current != null) options.headers['Authorization'] = 'Bearer $current';
      handler.next(options);
    }));
  }

  final Dio dio;

  /// Set after login, cleared on logout.
  String? token;

  /// Runs a request and turns network/HTTP errors into a readable [ApiException].
  Future<T> send<T>(Future<T> Function() request) async {
    try {
      return await request();
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }

  Future<void> requestOtp(String phone) =>
      send(() => dio.post<void>('/api/auth/otp/request', data: {'phone': phone}));

  Future<AuthResult> verifyOtp({required String phone, required String code, required String role}) =>
      send(() async {
        final res = await dio.post<Map<String, dynamic>>(
          '/api/auth/otp/verify',
          data: {'phone': phone, 'code': code, 'role': role},
        );
        return AuthResult.fromJson(res.data!);
      });

  Future<FareQuote> estimateFare(FareEstimateRequest request) => send(() async {
        final res = await dio.post<Map<String, dynamic>>(
          '/api/fares/estimate',
          data: request.toJson(),
        );
        return FareQuote.fromJson(res.data!);
      });
}
