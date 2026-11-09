import 'package:samasarathi_core/samasarathi_core.dart';

/// Requested → Accepted → ... A request can also end Declined (driver said no), Expired (no answer in time)
/// or Cancelled. Later phases add the trip itself (driverArrived, inProgress, completed).
enum RideStatus {
  requested,
  accepted,
  driverArrived,
  inProgress,
  completed,
  cancelled,
  declined,
  expired,
}

/// The driver, as the rider sees them. [phone] is null until the driver has accepted.
class RideDriver {
  const RideDriver({
    this.id = '',
    required this.name,
    required this.vehicleType,
    required this.registrationNumber,
    this.model,
    this.phone,
    this.position,
    this.ratingAverage,
    this.ratingCount = 0,
  });

  final String id;
  final String name;
  final String vehicleType;
  final String registrationNumber;
  final String? model;
  final String? phone;
  final LatLng? position;

  /// Average stars riders gave this driver; null until the first rating.
  final double? ratingAverage;
  final int ratingCount;

  factory RideDriver.fromJson(Map<String, dynamic> j) => RideDriver(
        id: j['id'] as String? ?? '',
        name: j['name'] as String,
        vehicleType: j['vehicleType'] as String,
        registrationNumber: j['registrationNumber'] as String,
        model: j['model'] as String?,
        phone: j['phone'] as String?,
        position: j['latitude'] == null
            ? null
            : LatLng((j['latitude'] as num).toDouble(), (j['longitude'] as num).toDouble()),
        ratingAverage: (j['ratingAverage'] as num?)?.toDouble(),
        ratingCount: j['ratingCount'] as int? ?? 0,
      );
}

class RideInfo {
  const RideInfo({
    required this.id,
    required this.status,
    required this.pickup,
    required this.drop,
    required this.estimatedDistanceKm,
    required this.estimatedMinutes,
    required this.estimatedFare,
    required this.ratePerKm,
    this.expiresAt,
    this.cancelReason,
    this.driver,
    this.actualMinutes,
    this.finalFare,
    this.pricingMode = PricingMode.distance,
    this.myRating,
    this.myComment,
  });

  final String id;
  final RideStatus status;
  final LatLng pickup;
  final LatLng drop;
  final double estimatedDistanceKm;
  final double estimatedMinutes;
  final double estimatedFare;
  final double ratePerKm;

  /// While [status] is requested: when the driver's chance to accept runs out.
  final DateTime? expiresAt;
  final String? cancelReason;
  final RideDriver? driver;

  /// Set once the trip is completed: how long it took and what it cost.
  final double? actualMinutes;
  final double? finalFare;
  final PricingMode pricingMode;

  /// The stars this rider gave the driver after the trip; null until they rate.
  final int? myRating;
  final String? myComment;

  /// Still going: the rider is waiting for, or riding with, a driver.
  bool get isActive => switch (status) {
        RideStatus.requested || RideStatus.accepted || RideStatus.driverArrived || RideStatus.inProgress => true,
        _ => false,
      };

  factory RideInfo.fromJson(Map<String, dynamic> j) => RideInfo(
        id: j['id'] as String,
        status: enumFromApi(RideStatus.values, j['status'] as String),
        pickup: LatLng((j['pickupLatitude'] as num).toDouble(), (j['pickupLongitude'] as num).toDouble()),
        drop: LatLng((j['dropLatitude'] as num).toDouble(), (j['dropLongitude'] as num).toDouble()),
        estimatedDistanceKm: (j['estimatedDistanceKm'] as num).toDouble(),
        estimatedMinutes: (j['estimatedMinutes'] as num).toDouble(),
        estimatedFare: (j['estimatedFare'] as num).toDouble(),
        ratePerKm: (j['ratePerKm'] as num).toDouble(),
        expiresAt: j['expiresAt'] == null ? null : DateTime.parse(j['expiresAt'] as String),
        cancelReason: j['cancelReason'] as String?,
        driver: j['driver'] == null ? null : RideDriver.fromJson(j['driver'] as Map<String, dynamic>),
        actualMinutes: (j['actualMinutes'] as num?)?.toDouble(),
        finalFare: (j['finalFare'] as num?)?.toDouble(),
        pricingMode: j['pricingMode'] == null ? PricingMode.distance : enumFromApi(PricingMode.values, j['pricingMode'] as String),
        myRating: j['myRating'] as int?,
        myComment: j['myComment'] as String?,
      );
}

/// The price for a trip with one driver, shown before the rider commits.
class RideQuote {
  const RideQuote({
    required this.distanceKm,
    required this.minutes,
    required this.fare,
    required this.ratePerKm,
    this.mode = PricingMode.distance,
    this.isPeakHour = false,
    this.isRaining = false,
    this.isRoadRoute = true,
    this.isWeatherKnown = true,
    this.route = const [],
  });

  final double distanceKm;
  final double minutes;
  final double fare;
  final double ratePerKm;
  final PricingMode mode;
  final bool isPeakHour;
  final bool isRaining;

  /// False when routing was unavailable and the distance is a straight-line estimate.
  final bool isRoadRoute;

  /// False when the weather service was unreachable (the trip is then priced as dry).
  final bool isWeatherKnown;

  /// The road route to draw on the map.
  final List<LatLng> route;

  factory RideQuote.fromJson(Map<String, dynamic> j) => RideQuote(
        distanceKm: (j['distanceKm'] as num).toDouble(),
        minutes: (j['minutes'] as num).toDouble(),
        fare: (j['fare'] as num).toDouble(),
        ratePerKm: (j['ratePerKm'] as num).toDouble(),
        mode: j['mode'] == null ? PricingMode.distance : enumFromApi(PricingMode.values, j['mode'] as String),
        isPeakHour: j['isPeakHour'] as bool? ?? false,
        isRaining: j['isRaining'] as bool? ?? false,
        isRoadRoute: j['isRoadRoute'] as bool? ?? true,
        isWeatherKnown: j['isWeatherKnown'] as bool? ?? true,
        route: [
          for (final p in (j['route'] as List? ?? const []))
            LatLng(((p as List)[0] as num).toDouble(), (p[1] as num).toDouble()),
        ],
      );
}

/// A destination found by name search. [address] is the full line shown under [name].
class PlaceResult {
  const PlaceResult({required this.name, required this.address, required this.position});

  final String name;
  final String address;
  final LatLng position;

  factory PlaceResult.fromJson(Map<String, dynamic> j) => PlaceResult(
        name: j['name'] as String,
        address: j['address'] as String? ?? '',
        position: LatLng((j['latitude'] as num).toDouble(), (j['longitude'] as num).toDouble()),
      );
}

/// The road lines for a ride's map.
class RideRouteLines {
  const RideRouteLines({this.trip = const [], this.toPickup = const []});

  /// Pickup to drop-off.
  final List<LatLng> trip;

  /// The driver's way to the pickup (only while they are on it).
  final List<LatLng> toPickup;

  static List<LatLng> _points(Object? raw) => [
        for (final p in (raw as List? ?? const [])) LatLng(((p as List)[0] as num).toDouble(), (p[1] as num).toDouble()),
      ];

  factory RideRouteLines.fromJson(Map<String, dynamic> j) =>
      RideRouteLines(trip: _points(j['trip']), toPickup: _points(j['toPickup']));
}

/// One past ride in the rider's history.
class RideHistoryItem {
  const RideHistoryItem({
    required this.id,
    required this.status,
    required this.requestedAt,
    required this.distanceKm,
    required this.fare,
    required this.driverName,
    required this.canRate,
    this.endedAt,
    this.vehicle,
    this.givenRating,
    this.receivedRating,
    this.cancelReason,
  });

  final String id;
  final RideStatus status;
  final DateTime requestedAt;
  final DateTime? endedAt;
  final double distanceKm;

  /// What was charged, or the estimate if the trip never finished.
  final double fare;
  final String driverName;

  /// e.g. "Auto · TS09EA1001".
  final String? vehicle;

  /// Stars the rider gave the driver / the driver gave the rider.
  final int? givenRating;
  final int? receivedRating;
  final bool canRate;
  final String? cancelReason;

  factory RideHistoryItem.fromJson(Map<String, dynamic> j) => RideHistoryItem(
        id: j['id'] as String,
        status: enumFromApi(RideStatus.values, j['status'] as String),
        requestedAt: DateTime.parse(j['requestedAt'] as String),
        endedAt: j['endedAt'] == null ? null : DateTime.parse(j['endedAt'] as String),
        distanceKm: (j['distanceKm'] as num).toDouble(),
        fare: (j['fare'] as num).toDouble(),
        driverName: j['otherName'] as String,
        vehicle: j['otherDetail'] as String?,
        givenRating: j['givenRating'] as int?,
        receivedRating: j['receivedRating'] as int?,
        canRate: j['canRate'] as bool? ?? false,
        cancelReason: j['cancelReason'] as String?,
      );
}
