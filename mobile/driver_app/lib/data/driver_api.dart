import 'package:dio/dio.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import 'driver_models.dart';

/// Driver registration endpoints (/api/drivers/me/...).
class DriverApi {
  DriverApi(this._client);

  final ApiClient _client;
  Dio get _dio => _client.dio;

  /// Null when this phone number hasn't started registration yet.
  Future<DriverMe?> me() async {
    try {
      return await _call(() => _dio.get('/api/drivers/me'));
    } on ApiException catch (e) {
      if (e.isNotFound) return null;
      rethrow;
    }
  }

  Future<DriverMe> saveProfile(String fullName) =>
      _call(() => _dio.put('/api/drivers/me/profile', data: {'fullName': fullName}));

  Future<DriverMe> saveVehicle(VehicleType type, String registrationNumber, String? model) =>
      _call(() => _dio.put('/api/drivers/me/vehicle', data: {
            'type': enumToApi(type),
            'registrationNumber': registrationNumber,
            'model': model,
          }));

  Future<DriverMe> uploadDocument(DocumentType type, String fileName, List<int> bytes) =>
      _call(() => _dio.post('/api/drivers/me/documents',
          data: FormData.fromMap({
            'type': enumToApi(type),
            'file': MultipartFile.fromBytes(bytes, filename: fileName, contentType: _contentType(fileName)),
          })));

  Future<DriverMe> saveRate(double ratePerKm) =>
      _call(() => _dio.put('/api/drivers/me/rate', data: {'ratePerKm': ratePerKm}));

  Future<DriverMe> submit() => _call(() => _dio.post('/api/drivers/me/submit'));

  /// Starts taking rides at [at]. Riders nearby see this driver on their map.
  Future<DriverMe> goOnline(LatLng at) => _call(() => _dio.post('/api/drivers/me/online', data: _body(at)));

  /// Sent every few seconds while online. If it stops, the driver drops off riders' maps after a minute.
  Future<DriverMe> updateLocation(LatLng at) => _call(() => _dio.put('/api/drivers/me/location', data: _body(at)));

  Future<DriverMe> goOffline() => _call(() => _dio.post('/api/drivers/me/offline'));

  /// The request waiting for an answer, or the accepted ride. Null when there is none (the server answers 204).
  Future<DriverRide?> currentRide() => _client.send(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/drivers/me/ride');
        final data = res.data;
        return data == null || data.isEmpty ? null : DriverRide.fromJson(data);
      });

  /// The road to follow next: from [from] to the pickup (before the rider is picked up) or to the drop-off.
  /// Empty when routing is unavailable; the map then shows only the pins.
  Future<List<LatLng>> navigationRoute(LatLng from) => _client.send(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/drivers/me/ride/route',
            queryParameters: {'lat': from.latitude, 'lng': from.longitude});
        return [
          for (final p in (res.data?['route'] as List? ?? const []))
            LatLng(((p as List)[0] as num).toDouble(), (p[1] as num).toDouble()),
        ];
      });

  /// Past rides, newest first.
  Future<List<DriverHistoryItem>> history({int skip = 0, int take = 20}) => _client.send(() async {
        final res = await _dio.get<List<dynamic>>('/api/drivers/me/rides/history', queryParameters: {'skip': skip, 'take': take});
        return [for (final r in res.data!) DriverHistoryItem.fromJson(r as Map<String, dynamic>)];
      });

  /// 1 to 5 stars for the rider after the trip, once only.
  Future<DriverRide> rateRider(String id, int stars, String? comment) => _client.send(() async => DriverRide.fromJson(
      (await _dio.post<Map<String, dynamic>>('/api/drivers/me/ride/$id/rating', data: {'stars': stars, 'comment': comment})).data!));

  /// The average stars riders gave this driver.
  Future<DriverRating> myRating() => _client.send(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/drivers/me/rating');
        return DriverRating.fromJson(res.data!);
      });

  /// Plan, validity and history. The first call after approval starts the free trial.
  Future<DriverSubscription> subscription() => _client.send(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/drivers/me/subscription');
        return DriverSubscription.fromJson(res.data!);
      });

  Future<DriverRide> acceptRide(String id) => _rideAction(id, 'accept');

  Future<DriverRide> declineRide(String id) => _rideAction(id, 'decline');

  /// At the pickup point.
  Future<DriverRide> arrivedAtPickup(String id) => _rideAction(id, 'arrived');

  /// The rider is in: the trip (and its timer) starts.
  Future<DriverRide> startTrip(String id) => _rideAction(id, 'start');

  /// Drop-off. The result carries the final fare.
  Future<DriverRide> completeTrip(String id) => _rideAction(id, 'complete');

  /// For when the driver accepted but can't make it.
  Future<DriverRide> cancelRide(String id) => _rideAction(id, 'cancel');

  Future<DriverRide> _rideAction(String id, String action) => _client.send(() async =>
      DriverRide.fromJson((await _dio.post<Map<String, dynamic>>('/api/drivers/me/ride/$id/$action')).data!));

  static Map<String, double> _body(LatLng at) => {'latitude': at.latitude, 'longitude': at.longitude};

  Future<DriverMe> _call(Future<Response<dynamic>> Function() request) =>
      _client.send(() async => DriverMe.fromJson((await request()).data as Map<String, dynamic>));

  static DioMediaType _contentType(String fileName) => switch (fileName.split('.').last.toLowerCase()) {
        'png' => DioMediaType('image', 'png'),
        'pdf' => DioMediaType('application', 'pdf'),
        _ => DioMediaType('image', 'jpeg'),
      };
}
