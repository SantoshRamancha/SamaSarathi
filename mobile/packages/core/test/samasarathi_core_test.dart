import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

enum _Doc { profilePhoto, drivingLicence, vehicleRC }

DioException _httpError(int status, Object? data) {
  final options = RequestOptions(path: '/x');
  return DioException(
    requestOptions: options,
    response: Response(requestOptions: options, statusCode: status, data: data),
  );
}

void main() {
  test('FareQuote parses backend JSON', () {
    final quote = FareQuote.fromJson({
      'mode': 'Time',
      'distanceFare': 100,
      'timeFare': 250,
      'fare': 250,
    });

    expect(quote.mode, PricingMode.time);
    expect(quote.fare, 250);
  });

  test('AuthResult parses backend JSON', () {
    final result = AuthResult.fromJson({
      'token': 'abc',
      'phone': '+919876543210',
      'role': 'driver',
      'isRegistered': false,
    });

    expect(result.isRegistered, isFalse);
    expect(result.role, UserRole.driver);
  });

  group('enum mapping', () {
    test('reads PascalCase from the API', () {
      expect(enumFromApi(_Doc.values, 'DrivingLicence'), _Doc.drivingLicence);
      expect(enumFromApi(_Doc.values, 'VehicleRC'), _Doc.vehicleRC);
    });

    test('writes PascalCase to the API', () {
      expect(enumToApi(_Doc.profilePhoto), 'ProfilePhoto');
      expect(enumToApi(_Doc.vehicleRC), 'VehicleRC');
    });
  });

  group('ApiException', () {
    test('uses ProblemDetails detail', () {
      final e = ApiException.fromDio(_httpError(400, {'title': 'Bad Request', 'detail': 'Enter a valid number.'}));
      expect(e.message, 'Enter a valid number.');
      expect(e.statusCode, 400);
    });

    test('falls back to first validation error', () {
      final e = ApiException.fromDio(_httpError(400, {
        'title': 'One or more validation errors occurred.',
        'errors': {
          'FullName': ['The FullName field is required.']
        },
      }));
      expect(e.message, 'The FullName field is required.');
    });

    test('explains a missing connection', () {
      final e = ApiException.fromDio(DioException(requestOptions: RequestOptions(path: '/x')));
      expect(e.message, contains("Can't reach the server"));
    });

    test('flags 404 and 401', () {
      expect(ApiException.fromDio(_httpError(404, null)).isNotFound, isTrue);
      expect(ApiException.fromDio(_httpError(401, '')).isUnauthorized, isTrue);
    });
  });
}
