enum PricingMode { distance, time }

/// Mirrors the backend's FareQuote.
class FareQuote {
  const FareQuote({
    required this.mode,
    required this.distanceFare,
    required this.timeFare,
    required this.fare,
  });

  final PricingMode mode;
  final double distanceFare;
  final double timeFare;
  final double fare;

  factory FareQuote.fromJson(Map<String, dynamic> json) => FareQuote(
        mode: PricingMode.values.byName((json['mode'] as String).toLowerCase()),
        distanceFare: (json['distanceFare'] as num).toDouble(),
        timeFare: (json['timeFare'] as num).toDouble(),
        fare: (json['fare'] as num).toDouble(),
      );
}

class FareEstimateRequest {
  const FareEstimateRequest({
    required this.ratePerKm,
    required this.distanceKm,
    required this.expectedMinutes,
    this.isPeakHour = false,
    this.isRaining = false,
    this.trafficRatio = 1,
  });

  final double ratePerKm;
  final double distanceKm;
  final double expectedMinutes;
  final bool isPeakHour;
  final bool isRaining;
  final double trafficRatio;

  Map<String, dynamic> toJson() => {
        'ratePerKm': ratePerKm,
        'distanceKm': distanceKm,
        'expectedMinutes': expectedMinutes,
        'isPeakHour': isPeakHour,
        'isRaining': isRaining,
        'trafficRatio': trafficRatio,
      };
}
