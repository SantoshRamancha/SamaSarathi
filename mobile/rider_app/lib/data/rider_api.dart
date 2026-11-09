import 'package:dio/dio.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import 'ride_models.dart';

class RiderMe {
  const RiderMe({required this.fullName, required this.phone});

  final String fullName;
  final String phone;

  factory RiderMe.fromJson(Map<String, dynamic> j) =>
      RiderMe(fullName: j['fullName'] as String, phone: j['phone'] as String);
}

/// A driver shown on the rider's map. No name or phone: those are shared only once a ride is booked.
class NearbyDriver {
  const NearbyDriver({required this.id, required this.position, required this.distanceKm, required this.vehicle, required this.ratePerKm, this.ratingAverage, this.ratingCount = 0});

  final String id;
  final LatLng position;
  final double distanceKm;
  final String vehicle;
  final double ratePerKm;

  /// Average stars riders gave this driver; null until the first rating.
  final double? ratingAverage;
  final int ratingCount;

  factory NearbyDriver.fromJson(Map<String, dynamic> j) => NearbyDriver(
        id: j['id'] as String,
        position: LatLng((j['latitude'] as num).toDouble(), (j['longitude'] as num).toDouble()),
        distanceKm: (j['distanceKm'] as num).toDouble(),
        vehicle: j['vehicleType'] as String,
        ratePerKm: (j['ratePerKm'] as num).toDouble(),
        ratingAverage: (j['ratingAverage'] as num?)?.toDouble(),
        ratingCount: j['ratingCount'] as int? ?? 0,
      );
}

/// Rider profile endpoints (/api/riders/me).
class RiderApi {
  RiderApi(this._client);

  final ApiClient _client;

  /// Null when this phone number hasn't set up a rider profile yet.
  Future<RiderMe?> me() async {
    try {
      return await _client.send(() async =>
          RiderMe.fromJson((await _client.dio.get<Map<String, dynamic>>('/api/riders/me')).data!));
    } on ApiException catch (e) {
      if (e.isNotFound) return null;
      rethrow;
    }
  }

  Future<RiderMe> saveProfile(String fullName) => _client.send(() async => RiderMe.fromJson(
      (await _client.dio.put<Map<String, dynamic>>('/api/riders/me', data: {'fullName': fullName})).data!));

  /// Online, verified drivers around [at], nearest first.
  Future<List<NearbyDriver>> nearbyDrivers(LatLng at) => _client.send(() async {
        final res = await _client.dio.get<List<dynamic>>('/api/riders/me/nearby-drivers',
            queryParameters: {'latitude': at.latitude, 'longitude': at.longitude});
        return [for (final d in res.data!) NearbyDriver.fromJson(d as Map<String, dynamic>)];
      });

  /// Places matching [query], preferring ones near [near] (the rider's position).
  Future<List<PlaceResult>> searchPlaces(String query, LatLng near) => _client.send(() async {
        final res = await _client.dio.get<List<dynamic>>('/api/places/search',
            queryParameters: {'query': query, 'latitude': near.latitude, 'longitude': near.longitude});
        return [for (final p in res.data!) PlaceResult.fromJson(p as Map<String, dynamic>)];
      });

  /// The price with [driverId] for this trip, before committing.
  Future<RideQuote> quote(String driverId, LatLng pickup, LatLng drop) => _client.send(() async {
        final res = await _client.dio.get<Map<String, dynamic>>('/api/rides/quote', queryParameters: _trip(driverId, pickup, drop));
        return RideQuote.fromJson(res.data!);
      });

  /// Asks one driver for a ride. They have a short time to accept.
  Future<RideInfo> requestRide(String driverId, LatLng pickup, LatLng drop) => _ride(
      () => _client.dio.post<Map<String, dynamic>>('/api/rides', data: _trip(driverId, pickup, drop)));

  /// The ride in progress, if any. Null when there is none (the server answers 204).
  Future<RideInfo?> activeRide() => _client.send(() async {
        final res = await _client.dio.get<Map<String, dynamic>>('/api/rides/active');
        final data = res.data;
        return data == null || data.isEmpty ? null : RideInfo.fromJson(data);
      });

  Future<RideInfo> ride(String id) => _ride(() => _client.dio.get<Map<String, dynamic>>('/api/rides/$id'));

  /// The road lines for the ride's map.
  Future<RideRouteLines> rideRoute(String id) => _client.send(() async {
        final res = await _client.dio.get<Map<String, dynamic>>('/api/rides/$id/route');
        return RideRouteLines.fromJson(res.data!);
      });

  /// 1 to 5 stars for the driver after the trip, once only.
  Future<RideInfo> rateRide(String id, int stars, String? comment) => _ride(() =>
      _client.dio.post<Map<String, dynamic>>('/api/rides/$id/rating', data: {'stars': stars, 'comment': comment}));

  /// Past rides, newest first.
  Future<List<RideHistoryItem>> history({int skip = 0, int take = 20}) => _client.send(() async {
        final res = await _client.dio.get<List<dynamic>>('/api/rides/history', queryParameters: {'skip': skip, 'take': take});
        return [for (final r in res.data!) RideHistoryItem.fromJson(r as Map<String, dynamic>)];
      });

  Future<RideInfo> cancelRide(String id) => _ride(() => _client.dio.post<Map<String, dynamic>>('/api/rides/$id/cancel'));

  Future<RideInfo> _ride(Future<Response<Map<String, dynamic>>> Function() request) =>
      _client.send(() async => RideInfo.fromJson((await request()).data!));

  static Map<String, dynamic> _trip(String driverId, LatLng pickup, LatLng drop) => {
        'driverId': driverId,
        'pickupLatitude': pickup.latitude,
        'pickupLongitude': pickup.longitude,
        'dropLatitude': drop.latitude,
        'dropLongitude': drop.longitude,
      };
}
