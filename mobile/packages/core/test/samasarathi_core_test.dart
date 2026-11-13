import 'package:flutter/material.dart';
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
  driverTrustBadgeTests();
  supportTests();

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
      final e = ApiException.fromDio(
        _httpError(400, {
          'title': 'Bad Request',
          'detail': 'Enter a valid number.',
        }),
      );
      expect(e.message, 'Enter a valid number.');
      expect(e.statusCode, 400);
    });

    test('falls back to first validation error', () {
      final e = ApiException.fromDio(
        _httpError(400, {
          'title': 'One or more validation errors occurred.',
          'errors': {
            'FullName': ['The FullName field is required.'],
          },
        }),
      );
      expect(e.message, 'The FullName field is required.');
    });

    test('explains a missing connection', () {
      final e = ApiException.fromDio(
        DioException(requestOptions: RequestOptions(path: '/x')),
      );
      expect(e.message, contains("Can't reach the server"));
    });

    test('flags 404 and 401', () {
      expect(ApiException.fromDio(_httpError(404, null)).isNotFound, isTrue);
      expect(ApiException.fromDio(_httpError(401, '')).isUnauthorized, isTrue);
    });
  });
}

void driverTrustBadgeTests() {
  Widget host(double? average, int count) => MaterialApp(
    home: Scaffold(
      body: Center(
        child: DriverTrustBadge(average: average, count: count),
      ),
    ),
  );

  testWidgets('an unrated driver shows "New driver" and "Verified"', (
    tester,
  ) async {
    await tester.pumpWidget(host(null, 0));

    expect(find.text('New driver'), findsOneWidget);
    expect(find.text('Verified'), findsOneWidget);
  });

  testWidgets(
    'a driver with a few ratings shows the count, not just the stars',
    (tester) async {
      await tester.pumpWidget(host(4.5, 2));
      expect(find.text('4.5 · 2 ratings'), findsOneWidget);

      await tester.pumpWidget(host(5, 1));
      expect(find.text('5.0 · 1 rating'), findsOneWidget); // singular
      expect(find.text('Verified'), findsNothing);
    },
  );

  testWidgets('a driver with enough ratings shows the plain average', (
    tester,
  ) async {
    await tester.pumpWidget(host(4.8, 23));

    expect(find.text('4.8 (23)'), findsOneWidget);
  });

  testWidgets('the rating threshold is exactly five', (tester) async {
    await tester.pumpWidget(host(4.0, 4));
    expect(find.text('4.0 · 4 ratings'), findsOneWidget);

    await tester.pumpWidget(host(4.0, 5));
    expect(find.text('4.0 (5)'), findsOneWidget);
  });

  testWidgets('tapping the chip explains it', (tester) async {
    await tester.pumpWidget(host(null, 0));

    await tester.tap(find.byType(DriverTrustBadge));
    await tester.pumpAndSettle();

    expect(find.textContaining('background checks'), findsOneWidget);
    await tester.tap(find.text('OK'));
    await tester.pumpAndSettle();
    expect(find.textContaining('background checks'), findsNothing);
  });

  testWidgets('a few ratings warns that the score may change', (tester) async {
    await tester.pumpWidget(host(5, 1));

    await tester.tap(find.byType(DriverTrustBadge));
    await tester.pumpAndSettle();

    expect(find.textContaining('only 1 rating'), findsOneWidget);
  });
}

void supportTests() {
  const full = SupportInfo(
    phone: '+91 98765-43210',
    whatsApp: '+919876543210',
    email: 'help@samasarathi.example',
    hours: 'Mon-Sat 9-7',
  );

  group('support links', () {
    test('call link keeps only digits and the plus sign', () {
      expect(supportCallUri('+91 98765-43210').toString(), 'tel:+919876543210');
    });

    test('WhatsApp link uses digits only and carries the message', () {
      final uri = supportWhatsAppUri(
        '+91 98765-43210',
        message: 'Hi, I am Ravi & need help',
      );
      expect(uri.host, 'wa.me');
      expect(uri.path, '/919876543210');
      expect(uri.queryParameters['text'], 'Hi, I am Ravi & need help');
    });

    test('email link encodes spaces so mail apps do not show plus signs', () {
      final uri = supportEmailUri(
        'help@samasarathi.example',
        message: 'I am Ravi',
      );
      expect(uri.toString(), contains('subject=Samasarathi%20support'));
      expect(uri.toString(), contains('body=I%20am%20Ravi'));
      expect(uri.toString(), isNot(contains('+')));
    });
  });

  test('SupportInfo treats blank values as missing', () {
    final info = SupportInfo.fromJson({
      'phone': '  ',
      'whatsApp': null,
      'email': 'a@b.c',
      'hours': '',
    });
    expect(info.phone, isNull);
    expect(info.whatsApp, isNull);
    expect(info.email, 'a@b.c');
    expect(info.available, isTrue);
    expect(SupportInfo.fromJson({}).available, isFalse);
  });

  Widget host(
    SupportInfo? info, {
    String? message,
    Future<bool> Function(Uri)? launch,
  }) => MaterialApp(
    home: Scaffold(
      body: SupportSheet(
        info: info,
        message: message,
        launch: launch ?? (_) async => true,
      ),
    ),
  );

  testWidgets('shows only the ways of contact that are configured', (
    tester,
  ) async {
    await tester.pumpWidget(
      host(const SupportInfo(email: 'help@samasarathi.example')),
    );

    expect(find.text('Email'), findsOneWidget);
    expect(find.text('Call us'), findsNothing);
    expect(find.text('WhatsApp'), findsNothing);
  });

  testWidgets(
    'says so when nothing is configured or the details could not load',
    (tester) async {
      await tester.pumpWidget(host(const SupportInfo()));
      expect(find.textContaining('not available yet'), findsOneWidget);

      await tester.pumpWidget(host(null));
      expect(find.textContaining('could not load'), findsOneWidget);
    },
  );

  testWidgets('tapping WhatsApp opens the chat with the prefilled message', (
    tester,
  ) async {
    Uri? opened;
    await tester.pumpWidget(
      host(
        full,
        message: 'Hi, I am Ravi',
        launch: (uri) async {
          opened = uri;
          return true;
        },
      ),
    );

    await tester.tap(find.text('WhatsApp'));
    await tester.pump();

    expect(opened?.host, 'wa.me');
    expect(opened?.queryParameters['text'], 'Hi, I am Ravi');
  });

  testWidgets('tells the person when the phone cannot open the link', (
    tester,
  ) async {
    await tester.pumpWidget(host(full, launch: (_) async => false));

    await tester.tap(find.text('Call us'));
    await tester.pump();

    expect(find.textContaining('Could not open the phone app'), findsOneWidget);
  });
}
