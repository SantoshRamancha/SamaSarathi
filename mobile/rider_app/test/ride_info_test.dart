import 'package:flutter_test/flutter_test.dart';
import 'package:rider_app/data/ride_models.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

Map<String, dynamic> _json({String status = 'Requested', Map<String, dynamic>? driver}) => {
      'id': '3f2c0000-0000-0000-0000-000000000009',
      'status': status,
      'pickupLatitude': 17.3616,
      'pickupLongitude': 78.4747,
      'dropLatitude': 17.4435,
      'dropLongitude': 78.3772,
      'estimatedDistanceKm': 16.9,
      'estimatedMinutes': 41,
      'estimatedFare': 169,
      'pricingMode': 'Distance',
      'ratePerKm': 10,
      'requestedAt': '2026-11-02T09:00:00+00:00',
      'expiresAt': status == 'Requested' ? '2026-11-02T09:00:45+00:00' : null,
      'cancelReason': status == 'Cancelled' ? 'Cancelled by driver' : null,
      'driver': driver,
      if (status == 'Completed') 'actualMinutes': 31.5,
      if (status == 'Completed') 'finalFare': 169,
    };

void main() {
  test('RideInfo parses a waiting request', () {
    final r = RideInfo.fromJson(_json(driver: {
      'id': '3f2c0000-0000-0000-0000-0000000000aa',
      'name': 'Ravi Kumar',
      'vehicleType': 'Auto',
      'registrationNumber': 'TS09EA4321',
      'model': null,
      'phone': null,
      'latitude': 17.36,
      'longitude': 78.47,
    }));

    expect(r.status, RideStatus.requested);
    expect(r.isActive, isTrue);
    expect(r.estimatedFare, 169);
    expect(r.expiresAt, DateTime.utc(2026, 11, 2, 9, 0, 45));
    expect(r.driver!.id, '3f2c0000-0000-0000-0000-0000000000aa');
    expect(r.driver!.name, 'Ravi Kumar');
    expect(r.driver!.phone, isNull);
    expect(r.driver!.position!.latitude, 17.36);
  });

  test('finished rides are not active', () {
    for (final status in ['Declined', 'Expired', 'Cancelled', 'Completed']) {
      expect(RideInfo.fromJson(_json(status: status)).isActive, isFalse, reason: status);
    }
  });

  test('accepted and in-progress rides are active', () {
    for (final status in ['Accepted', 'DriverArrived', 'InProgress']) {
      expect(RideInfo.fromJson(_json(status: status)).isActive, isTrue, reason: status);
    }
  });

  test('RideInfo keeps the cancel reason', () {
    expect(RideInfo.fromJson(_json(status: 'Cancelled')).cancelReason, 'Cancelled by driver');
  });

  test('RideQuote parses backend JSON', () {
    final q = RideQuote.fromJson({'distanceKm': 16.9, 'minutes': 41, 'fare': 169, 'ratePerKm': 10, 'mode': 'Distance'});
    expect(q.fare, 169);
    expect(q.distanceKm, 16.9);
  });

  test('a completed ride carries the final fare and the time taken', () {
    final r = RideInfo.fromJson(_json(status: 'Completed'));
    expect(r.finalFare, 169);
    expect(r.actualMinutes, 31.5);
  });

  test('a ride that has not finished has no final fare', () {
    final r = RideInfo.fromJson(_json(status: 'InProgress'));
    expect(r.finalFare, isNull);
    expect(r.actualMinutes, isNull);
  });

  test('RideQuote reads the pricing reasons and the road route', () {
    final q = RideQuote.fromJson({
      'distanceKm': 19.3,
      'minutes': 38,
      'fare': 241,
      'ratePerKm': 10,
      'mode': 'Time',
      'isPeakHour': true,
      'isRaining': false,
      'isRoadRoute': true,
      'isWeatherKnown': true,
      'route': [
        [17.3616, 78.4747],
        [17.4, 78.4],
        [17.4435, 78.3772],
      ],
    });

    expect(q.mode, PricingMode.time);
    expect(q.isPeakHour, isTrue);
    expect(q.isRaining, isFalse);
    expect(q.route, hasLength(3));
    expect(q.route.first.latitude, 17.3616);
    expect(q.route.last.longitude, 78.3772);
  });

  test('RideQuote from an older server (no new fields) still parses as a dry, distance-priced trip', () {
    final q = RideQuote.fromJson({'distanceKm': 10, 'minutes': 20, 'fare': 100, 'ratePerKm': 10});

    expect(q.mode, PricingMode.distance);
    expect(q.isPeakHour, isFalse);
    expect(q.isRoadRoute, isTrue);
    expect(q.route, isEmpty);
  });

  test('RideInfo reads the pricing mode', () {
    expect(RideInfo.fromJson({..._json(), 'pricingMode': 'Time'}).pricingMode, PricingMode.time);
    expect(RideInfo.fromJson(_json()).pricingMode, PricingMode.distance);
  });
}
