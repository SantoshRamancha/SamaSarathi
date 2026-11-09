import 'package:driver_app/data/driver_models.dart';
import 'package:flutter_test/flutter_test.dart';

Map<String, dynamic> _json({bool? isOnline}) => {
      'fullName': 'Ravi Kumar',
      'phone': '+919876543210',
      'ratePerKm': 12,
      'verificationStatus': 'Approved',
      'canEdit': false,
      'vehicle': null,
      'documents': [],
      'missingSteps': [],
      'checks': [],
      'isOnline': ?isOnline,
    };

void main() {
  test('DriverMe reads isOnline', () {
    expect(DriverMe.fromJson(_json(isOnline: true)).isOnline, isTrue);
    expect(DriverMe.fromJson(_json(isOnline: false)).isOnline, isFalse);
  });

  test('DriverMe treats a missing isOnline as offline', () {
    expect(DriverMe.fromJson(_json()).isOnline, isFalse);
  });
}
