import 'package:flutter_test/flutter_test.dart';
import 'package:rider_app/data/rider_api.dart';

void main() {
  test('NearbyDriver parses backend JSON', () {
    final d = NearbyDriver.fromJson({
      'id': '3f2c0000-0000-0000-0000-000000000001',
      'latitude': 17.3616,
      'longitude': 78.4747,
      'distanceKm': 1.25,
      'vehicleType': 'Auto',
      'ratePerKm': 12,
    });

    expect(d.position.latitude, 17.3616);
    expect(d.position.longitude, 78.4747);
    expect(d.distanceKm, 1.25);
    expect(d.vehicle, 'Auto');
    expect(d.ratePerKm, 12);
  });
}
