import 'package:driver_app/data/driver_models.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

Map<String, dynamic> _json({String status = 'Requested', String? riderPhone, String? expiresAt, double? finalFare}) => {
      'id': '3f2c0000-0000-0000-0000-000000000009',
      'status': status,
      'pickupLatitude': 17.3616,
      'pickupLongitude': 78.4747,
      'dropLatitude': 17.4435,
      'dropLongitude': 78.3772,
      'estimatedDistanceKm': 16.9,
      'estimatedMinutes': 41,
      'estimatedFare': 169,
      'pickupDistanceKm': 0.8,
      'requestedAt': '2026-11-02T09:00:00+00:00',
      'expiresAt': expiresAt,
      'riderName': 'Asha',
      'riderPhone': riderPhone,
      'actualMinutes': finalFare == null ? null : 31.5,
      'finalFare': finalFare,
    };

void main() {
  test('DriverRide parses a waiting request (no rider phone yet)', () {
    final r = DriverRide.fromJson(_json(expiresAt: '2026-11-02T09:00:45+00:00'));

    expect(r.status, RideStatus.requested);
    expect(r.pickup.latitude, 17.3616);
    expect(r.drop.longitude, 78.3772);
    expect(r.fare, 169);
    expect(r.pickupDistanceKm, 0.8);
    expect(r.riderName, 'Asha');
    expect(r.riderPhone, isNull);
    expect(r.expiresAt, DateTime.utc(2026, 11, 2, 9, 0, 45));
  });

  test('DriverRide parses an accepted ride with the rider phone', () {
    final r = DriverRide.fromJson(_json(status: 'Accepted', riderPhone: '+919000000001'));

    expect(r.status, RideStatus.accepted);
    expect(r.riderPhone, '+919000000001');
    expect(r.expiresAt, isNull);
  });

  test('DriverRide.isActive is true until the ride ends', () {
    for (final status in ['Requested', 'Accepted', 'DriverArrived', 'InProgress']) {
      expect(DriverRide.fromJson(_json(status: status)).isActive, isTrue, reason: status);
    }
    for (final status in ['Completed', 'Cancelled', 'Declined', 'Expired']) {
      expect(DriverRide.fromJson(_json(status: status)).isActive, isFalse, reason: status);
    }
  });

  test('DriverRide parses the final fare of a completed trip', () {
    final r = DriverRide.fromJson(_json(status: 'Completed', riderPhone: '+919000000001', finalFare: 169));
    expect(r.status, RideStatus.completed);
    expect(r.finalFare, 169);
    expect(r.actualMinutes, 31.5);
  });

  test('DriverRide reads the pricing mode', () {
    expect(DriverRide.fromJson({..._json(), 'pricingMode': 'Time'}).pricingMode, PricingMode.time);
    expect(DriverRide.fromJson(_json()).pricingMode, PricingMode.distance);
  });

  test('DriverRide reads the stars the driver gave the rider', () {
    expect(DriverRide.fromJson({..._json(status: 'Completed'), 'yourRating': 4}).yourRating, 4);
    expect(DriverRide.fromJson(_json()).yourRating, isNull);
  });

  test('DriverHistoryItem parses a completed ride and a declined one', () {
    final done = DriverHistoryItem.fromJson({
      'id': 'a',
      'status': 'Completed',
      'requestedAt': '2026-11-06T10:00:00Z',
      'endedAt': '2026-11-06T10:30:00Z',
      'distanceKm': 12.5,
      'fare': 180,
      'otherName': 'Asha',
      'givenRating': null,
      'receivedRating': 5,
      'canRate': true,
    });
    expect(done.status, RideStatus.completed);
    expect(done.riderName, 'Asha');
    expect(done.canRate, isTrue);
    expect(done.receivedRating, 5);

    final declined = DriverHistoryItem.fromJson({
      'id': 'b',
      'status': 'Declined',
      'requestedAt': '2026-11-06T11:00:00Z',
      'distanceKm': 3,
      'fare': 50,
      'otherName': 'Ravi',
    });
    expect(declined.status, RideStatus.declined);
    expect(declined.endedAt, isNull);
    expect(declined.canRate, isFalse);
  });

  test('DriverRating is "new" until the first rating', () {
    expect(DriverRating.fromJson({'average': null, 'count': 0}).average, isNull);
    final r = DriverRating.fromJson({'average': 4.6, 'count': 12});
    expect(r.average, 4.6);
    expect(r.count, 12);
  });
}
