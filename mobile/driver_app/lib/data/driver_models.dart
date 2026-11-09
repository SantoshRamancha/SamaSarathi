import 'package:samasarathi_core/samasarathi_core.dart';

enum VehicleType {
  bike('Bike'),
  auto('Auto'),
  car('Car');

  const VehicleType(this.label);
  final String label;
}

enum DocumentType {
  profilePhoto('Your photo', 'A clear photo of your face'),
  drivingLicence('Driving licence', 'Front side, all details readable'),
  vehicleRC('Vehicle RC', 'Registration certificate of your vehicle'),
  identityProof('ID proof', 'Aadhaar, PAN or Voter ID');

  const DocumentType(this.label, this.hint);
  final String label;
  final String hint;
}

enum OnboardingStep { profile, vehicle, documents, rate }

enum VerificationStatus {
  pending('Not submitted'),
  inReview('Under verification'),
  approved('Approved'),
  rejected('Changes needed'),
  suspended('Suspended');

  const VerificationStatus(this.label);
  final String label;
}

enum VerificationCheckType {
  drivingLicence('Driving licence check'),
  identityProof('Identity check'),
  policeRecord('Police / criminal record'),
  drivingSkillTest('Driving skill test'),
  behaviourAssessment('Behaviour assessment');

  const VerificationCheckType(this.label);
  final String label;
}

enum CheckResult { pending, passed, failed }

class VehicleInfo {
  const VehicleInfo({required this.type, required this.registrationNumber, this.model});

  final VehicleType type;
  final String registrationNumber;
  final String? model;

  factory VehicleInfo.fromJson(Map<String, dynamic> j) => VehicleInfo(
        type: enumFromApi(VehicleType.values, j['type'] as String),
        registrationNumber: j['registrationNumber'] as String,
        model: j['model'] as String?,
      );
}

class UploadedDocument {
  const UploadedDocument({required this.type, required this.fileName, required this.uploadedAt});

  final DocumentType type;
  final String fileName;
  final DateTime uploadedAt;

  factory UploadedDocument.fromJson(Map<String, dynamic> j) => UploadedDocument(
        type: enumFromApi(DocumentType.values, j['type'] as String),
        fileName: j['fileName'] as String,
        uploadedAt: DateTime.parse(j['uploadedAt'] as String),
      );
}

class VerificationCheck {
  const VerificationCheck({required this.type, required this.result, this.notes});

  final VerificationCheckType type;
  final CheckResult result;
  final String? notes;

  factory VerificationCheck.fromJson(Map<String, dynamic> j) => VerificationCheck(
        type: enumFromApi(VerificationCheckType.values, j['type'] as String),
        result: enumFromApi(CheckResult.values, j['result'] as String),
        notes: j['notes'] as String?,
      );
}

/// The logged-in driver, as returned by GET /api/drivers/me.
class DriverMe {
  const DriverMe({
    required this.fullName,
    required this.phone,
    required this.ratePerKm,
    required this.status,
    required this.canEdit,
    required this.vehicle,
    required this.documents,
    required this.missingSteps,
    required this.checks,
    this.isOnline = false,
    this.rejectionReason,
  });

  final String fullName;
  final String phone;
  final double ratePerKm;
  final VerificationStatus status;
  final bool canEdit;
  final VehicleInfo? vehicle;
  final List<UploadedDocument> documents;
  final List<OnboardingStep> missingSteps;
  final List<VerificationCheck> checks;

  /// Taking rides: riders nearby can see this driver on their map.
  final bool isOnline;

  /// Our team's message when they send the registration back for changes.
  final String? rejectionReason;

  List<VerificationCheck> get failedChecks => checks.where((c) => c.result == CheckResult.failed).toList();

  bool isDone(OnboardingStep step) => !missingSteps.contains(step);
  UploadedDocument? document(DocumentType type) =>
      documents.where((d) => d.type == type).firstOrNull;

  factory DriverMe.fromJson(Map<String, dynamic> j) => DriverMe(
        fullName: j['fullName'] as String,
        phone: j['phone'] as String,
        ratePerKm: (j['ratePerKm'] as num).toDouble(),
        status: enumFromApi(VerificationStatus.values, j['verificationStatus'] as String),
        canEdit: j['canEdit'] as bool,
        vehicle: j['vehicle'] == null ? null : VehicleInfo.fromJson(j['vehicle'] as Map<String, dynamic>),
        documents: [
          for (final d in j['documents'] as List) UploadedDocument.fromJson(d as Map<String, dynamic>)
        ],
        missingSteps: [
          for (final s in j['missingSteps'] as List) enumFromApi(OnboardingStep.values, s as String)
        ],
        checks: [
          for (final c in j['checks'] as List) VerificationCheck.fromJson(c as Map<String, dynamic>)
        ],
        isOnline: j['isOnline'] as bool? ?? false,
        rejectionReason: j['rejectionReason'] as String?,
      );
}

/// Requested → (driver accepts or declines) → Accepted. Later phases add the trip itself.
/// A request can also end Declined, Expired (nobody answered in time) or Cancelled.
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

/// A ride a rider asked this driver for. [riderPhone] is null until the driver accepts.
class DriverRide {
  const DriverRide({
    required this.id,
    required this.status,
    required this.pickup,
    required this.drop,
    required this.distanceKm,
    required this.minutes,
    required this.fare,
    required this.riderName,
    this.pickupDistanceKm,
    this.expiresAt,
    this.riderPhone,
    this.actualMinutes,
    this.finalFare,
    this.pricingMode = PricingMode.distance,
    this.yourRating,
  });

  final String id;
  final RideStatus status;
  final LatLng pickup;
  final LatLng drop;
  final double distanceKm;
  final double minutes;
  final double fare;
  final String riderName;

  /// How far the driver is from the pickup right now.
  final double? pickupDistanceKm;

  /// While [status] is requested: when the chance to accept runs out.
  final DateTime? expiresAt;
  final String? riderPhone;

  /// Set once the trip is completed: how long it took and what it cost.
  final double? actualMinutes;
  final double? finalFare;
  final PricingMode pricingMode;

  /// The stars this driver gave the rider after the trip; null until they rate.
  final int? yourRating;

  /// Still going: waiting for an answer, on the way, or on the trip.
  bool get isActive => switch (status) {
        RideStatus.requested || RideStatus.accepted || RideStatus.driverArrived || RideStatus.inProgress => true,
        _ => false,
      };

  factory DriverRide.fromJson(Map<String, dynamic> j) => DriverRide(
        id: j['id'] as String,
        status: enumFromApi(RideStatus.values, j['status'] as String),
        pickup: LatLng((j['pickupLatitude'] as num).toDouble(), (j['pickupLongitude'] as num).toDouble()),
        drop: LatLng((j['dropLatitude'] as num).toDouble(), (j['dropLongitude'] as num).toDouble()),
        distanceKm: (j['estimatedDistanceKm'] as num).toDouble(),
        minutes: (j['estimatedMinutes'] as num).toDouble(),
        fare: (j['estimatedFare'] as num).toDouble(),
        riderName: j['riderName'] as String,
        pickupDistanceKm: (j['pickupDistanceKm'] as num?)?.toDouble(),
        expiresAt: j['expiresAt'] == null ? null : DateTime.parse(j['expiresAt'] as String),
        riderPhone: j['riderPhone'] as String?,
        actualMinutes: (j['actualMinutes'] as num?)?.toDouble(),
        finalFare: (j['finalFare'] as num?)?.toDouble(),
        pricingMode: j['pricingMode'] == null ? PricingMode.distance : enumFromApi(PricingMode.values, j['pricingMode'] as String),
        yourRating: j['yourRating'] as int?,
      );
}

/// One past ride in the driver's history.
class DriverHistoryItem {
  const DriverHistoryItem({
    required this.id,
    required this.status,
    required this.requestedAt,
    required this.distanceKm,
    required this.fare,
    required this.riderName,
    required this.canRate,
    this.endedAt,
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
  final String riderName;

  /// Stars the driver gave the rider / the rider gave the driver.
  final int? givenRating;
  final int? receivedRating;
  final bool canRate;
  final String? cancelReason;

  factory DriverHistoryItem.fromJson(Map<String, dynamic> j) => DriverHistoryItem(
        id: j['id'] as String,
        status: enumFromApi(RideStatus.values, j['status'] as String),
        requestedAt: DateTime.parse(j['requestedAt'] as String),
        endedAt: j['endedAt'] == null ? null : DateTime.parse(j['endedAt'] as String),
        distanceKm: (j['distanceKm'] as num).toDouble(),
        fare: (j['fare'] as num).toDouble(),
        riderName: j['otherName'] as String,
        givenRating: j['givenRating'] as int?,
        receivedRating: j['receivedRating'] as int?,
        canRate: j['canRate'] as bool? ?? false,
        cancelReason: j['cancelReason'] as String?,
      );
}

enum SubscriptionPhase { none, active, grace, expired }

enum SubscriptionKind { trial, paid, granted }

/// One period in the subscription history.
class SubscriptionPeriod {
  const SubscriptionPeriod({required this.kind, required this.planName, required this.price, required this.startsAt, required this.endsAt, this.note});

  final SubscriptionKind kind;
  final String planName;
  final double price;
  final DateTime startsAt;
  final DateTime endsAt;
  final String? note;

  factory SubscriptionPeriod.fromJson(Map<String, dynamic> j) => SubscriptionPeriod(
        kind: enumFromApi(SubscriptionKind.values, j['kind'] as String),
        planName: j['planName'] as String,
        price: (j['price'] as num).toDouble(),
        startsAt: DateTime.parse(j['startsAt'] as String),
        endsAt: DateTime.parse(j['endsAt'] as String),
        note: j['note'] as String?,
      );
}

/// The driver's subscription: what the plan costs, how long it is valid and whether they may take rides.
/// Every number comes from the server, so changing the price or trial length needs no app update.
class DriverSubscription {
  const DriverSubscription({
    required this.enforced,
    required this.phase,
    required this.isTrial,
    required this.canTakeRides,
    required this.expiringSoon,
    required this.planName,
    required this.monthlyPrice,
    required this.paymentsEnabled,
    this.validUntil,
    this.graceEndsAt,
    this.daysLeft,
    this.history = const [],
  });

  /// False when subscriptions are switched off on the server: nothing is needed to take rides.
  final bool enforced;
  final SubscriptionPhase phase;
  final bool isTrial;
  final bool canTakeRides;
  final bool expiringSoon;
  final String planName;
  final double monthlyPrice;

  /// False until online payment exists: the app tells the driver to contact support instead of showing Pay.
  final bool paymentsEnabled;
  final DateTime? validUntil;
  final DateTime? graceEndsAt;
  final int? daysLeft;
  final List<SubscriptionPeriod> history;

  /// Worth showing a banner on the home screen: expiring, in grace, ended, or never started.
  bool get needsAttention => enforced && (expiringSoon || phase != SubscriptionPhase.active);

  factory DriverSubscription.fromJson(Map<String, dynamic> j) => DriverSubscription(
        enforced: j['enforced'] as bool? ?? true,
        phase: enumFromApi(SubscriptionPhase.values, j['phase'] as String),
        isTrial: j['isTrial'] as bool? ?? false,
        canTakeRides: j['canTakeRides'] as bool? ?? false,
        expiringSoon: j['expiringSoon'] as bool? ?? false,
        planName: j['planName'] as String? ?? 'Monthly',
        monthlyPrice: (j['monthlyPrice'] as num?)?.toDouble() ?? 0,
        paymentsEnabled: j['paymentsEnabled'] as bool? ?? false,
        validUntil: j['validUntil'] == null ? null : DateTime.parse(j['validUntil'] as String),
        graceEndsAt: j['graceEndsAt'] == null ? null : DateTime.parse(j['graceEndsAt'] as String),
        daysLeft: j['daysLeft'] as int?,
        history: [for (final p in (j['history'] as List? ?? const [])) SubscriptionPeriod.fromJson(p as Map<String, dynamic>)],
      );
}

/// The driver's average stars from riders; [average] is null until the first rating.
class DriverRating {
  const DriverRating({this.average, this.count = 0});

  final double? average;
  final int count;

  factory DriverRating.fromJson(Map<String, dynamic> j) =>
      DriverRating(average: (j['average'] as num?)?.toDouble(), count: j['count'] as int? ?? 0);
}
