import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../../data/ride_models.dart';
import '../../providers.dart';
import 'book_ride_screen.dart';

/// Follows a ride after the rider has asked a driver: waiting for the answer, then the accepted driver.
class RideTrackingScreen extends ConsumerStatefulWidget {
  const RideTrackingScreen({super.key, required this.initial});

  final RideInfo initial;

  @override
  ConsumerState<RideTrackingScreen> createState() => _RideTrackingScreenState();
}

class _RideTrackingScreenState extends ConsumerState<RideTrackingScreen> {
  Timer? _timer;
  late RideInfo _ride = widget.initial;
  int _tick = 0;
  bool _busy = false;
  String? _error;

  /// How often (in seconds) the road lines are re-fetched so the driver's way to the pickup follows them.
  static const _routeRefresh = 15;
  RideRouteLines _lines = const RideRouteLines();
  String? _routeKey; // ride status the lines were last fetched for
  int _routeFetchedAt = 0;
  bool _routeLoading = false;

  @override
  void initState() {
    super.initState();
    _loadRoute();
    // Every second: update the countdown. Every third second: ask the server for news.
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      setState(() => _tick++);
      if (_ride.isActive && _tick % 3 == 0) _poll();
      _loadRoute();
    });
  }

  /// Fetches the blue trip line (pickup to drop-off) and the orange line for the driver's way to the pickup.
  /// Refetched when the ride's step changes, and every [_routeRefresh] seconds while the driver is still on the way.
  /// Routing is a nicety: on any failure the map keeps what it has (or just the pins).
  Future<void> _loadRoute() async {
    final ride = _ride;
    if (ride.status == RideStatus.requested || (!ride.isActive && _routeKey != null)) return;
    final key = ride.status.name;
    final stepChanged = key != _routeKey;
    final onTheWay = ride.status == RideStatus.accepted;
    if (_routeLoading || (!stepChanged && !(onTheWay && _tick - _routeFetchedAt >= _routeRefresh))) return;

    _routeLoading = true;
    _routeKey = key;
    _routeFetchedAt = _tick;
    try {
      final lines = await ref.read(riderApiProvider).rideRoute(ride.id);
      if (mounted) setState(() => _lines = lines);
    } catch (_) {
      // Keep the previous lines; the next refresh tries again.
    } finally {
      _routeLoading = false;
    }
  }

  Future<void> _rate(RatingResult result) async {
    final updated = await ref.read(riderApiProvider).rateRide(_ride.id, result.stars, result.comment);
    if (mounted) setState(() => _ride = updated);
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _poll() async {
    try {
      final ride = await ref.read(riderApiProvider).ride(_ride.id);
      if (mounted) setState(() => _ride = ride);
    } catch (_) {
      // A network blip: keep what we have; the next poll tries again.
    }
  }

  Future<void> _cancel() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final ride = await ref.read(riderApiProvider).cancelRide(_ride.id);
      if (mounted) setState(() => _ride = ride);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Reopens booking with the same pickup and drop-off. The driver who declined or didn't answer is left out of the list.
  void _chooseAnother() {
    final ride = _ride;
    final skipDriver = ride.driver?.id;
    final skip = (ride.status == RideStatus.declined || ride.status == RideStatus.expired) && skipDriver != null && skipDriver.isNotEmpty;
    ref.invalidate(activeRideProvider);
    // Replace this screen (not go): the back button on the booking screen still returns to home.
    context.pushReplacement('/book', extra: BookRideArgs(pickup: ride.pickup, drop: ride.drop, excludeDriverIds: {if (skip) skipDriver}));
  }

  void _home() {
    ref.invalidate(activeRideProvider);
    context.go('/');
  }

  @override
  Widget build(BuildContext context) {
    final ride = _ride;
    final driver = ride.driver;
    final driverAt = driver?.position;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Your ride'),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: _home),
      ),
      body: Column(
        children: [
          SizedBox(
            height: 260,
            child: AppMap(
              center: ride.pickup,
              fitPoints: [ride.pickup, ride.drop],
              route: _lines.trip,
              extraRoutes: [MapRoute(_lines.toPickup, Colors.orange)],
              markers: [
                AppMarker.pin(ride.pickup, Colors.green, tooltip: 'Pickup'),
                AppMarker.pin(ride.drop, Colors.red, tooltip: 'Drop-off'),
                if (driverAt != null && ride.isActive && ride.status != RideStatus.requested)
                  AppMarker(
                    point: driverAt,
                    child: CircleAvatar(
                      backgroundColor: Theme.of(context).colorScheme.primary,
                      child: Icon(vehicleIcon(driver?.vehicleType ?? ''), color: Theme.of(context).colorScheme.onPrimary, size: 20),
                    ),
                  ),
              ],
            ),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                _statusCard(context, ride),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                  ),
                const SizedBox(height: 12),
                // Once the trip has started the rider can't back out from here.
                if (ride.status == RideStatus.requested ||
                    ride.status == RideStatus.accepted ||
                    ride.status == RideStatus.driverArrived)
                  OutlinedButton(
                    onPressed: _busy ? null : _cancel,
                    child: Text(_busy ? 'Please wait…' : 'Cancel ride'),
                  ),
                // Help with a ride that has a driver: a problem on the way, or a dispute about the fare afterwards.
                if (ride.driver != null && ride.status != RideStatus.declined && ride.status != RideStatus.expired)
                  TextButton.icon(
                    icon: const Icon(Icons.support_agent),
                    label: const Text('Need help with this ride?'),
                    onPressed: () => contactSupport(ref, context,
                        topic: 'I need help with my ride with ${ride.driver!.name} (${ride.driver!.registrationNumber}), ride id ${ride.id}.'),
                  ),
                // A request that didn't work out: go straight back to the driver list for the same trip.
                if (!ride.isActive && ride.status != RideStatus.completed) ...[
                  FilledButton(onPressed: _chooseAnother, child: const Text('Choose another driver')),
                  const SizedBox(height: 8),
                  OutlinedButton(onPressed: _home, child: const Text('Back to home')),
                ] else if (!ride.isActive)
                  FilledButton(onPressed: _home, child: const Text('Back to home')),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _statusCard(BuildContext context, RideInfo ride) {
    final text = Theme.of(context).textTheme;
    final driver = ride.driver;
    final fare = Text('Estimated fare ₹${ride.estimatedFare.toStringAsFixed(0)} · about ${ride.estimatedDistanceKm} km'
        '${ride.pricingMode == PricingMode.time ? ' · charged by travel time' : ''}');

    switch (ride.status) {
      case RideStatus.requested:
        final left = ride.expiresAt?.difference(DateTime.now()).inSeconds ?? 0;
        return Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text('Waiting for ${driver?.name.split(' ').first ?? 'the driver'} to accept…', style: text.titleMedium),
              const SizedBox(height: 8),
              const LinearProgressIndicator(),
              const SizedBox(height: 8),
              Text(left > 0 ? 'They have $left s to answer.' : 'Checking…'),
              const SizedBox(height: 4),
              fare,
            ]),
          ),
        );
      case RideStatus.accepted || RideStatus.driverArrived || RideStatus.inProgress:
        return Card(
          color: Theme.of(context).colorScheme.primaryContainer,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                Icon(switch (ride.status) {
                  RideStatus.driverArrived => Icons.place,
                  RideStatus.inProgress => Icons.directions,
                  _ => Icons.check_circle,
                }),
                const SizedBox(width: 8),
                Expanded(child: Text(_progressTitle(ride), style: text.titleMedium)),
              ]),
              const SizedBox(height: 8),
              if (driver != null) ...[
                Row(children: [
                  Flexible(child: Text(driver.name, style: text.titleLarge)),
                  const SizedBox(width: 8),
                  DriverTrustBadge(average: driver.ratingAverage, count: driver.ratingCount),
                ]),
                Text('${driver.vehicleType}${driver.model == null ? '' : ' · ${driver.model}'} · ${driver.registrationNumber}'),
                if (driver.phone != null) SelectableText('Phone: ${driver.phone}'),
              ],
              const SizedBox(height: 8),
              fare,
              const SizedBox(height: 4),
            ]),
          ),
        );
      case RideStatus.declined:
        return _endedCard(context, Icons.block, 'The driver can’t take this ride', 'Choose another driver for the same trip.');
      case RideStatus.expired:
        return _endedCard(context, Icons.timer_off, 'The driver didn’t answer in time', 'Choose another driver for the same trip.');
      case RideStatus.cancelled:
        return _endedCard(context, Icons.cancel_outlined, ride.cancelReason ?? 'Ride cancelled', 'You can book again any time.');
      case RideStatus.completed:
        final paid = ride.finalFare ?? ride.estimatedFare;
        return Card(
          color: Theme.of(context).colorScheme.primaryContainer,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                const Icon(Icons.flag),
                const SizedBox(width: 8),
                Text('Trip complete', style: text.titleMedium),
              ]),
              const SizedBox(height: 8),
              Text('Fare ₹${paid.toStringAsFixed(0)}', style: text.displaySmall),
              Text('About ${ride.estimatedDistanceKm} km · ${(ride.actualMinutes ?? ride.estimatedMinutes).toStringAsFixed(0)} min'),
              if (driver != null) Text('Driver: ${driver.name} · ${driver.registrationNumber}'),
              const SizedBox(height: 8),
              Text('Pay ${driver?.name.split(' ').first ?? 'the driver'} directly (cash or UPI). No commission: the full fare goes to the driver.',
                  style: text.bodySmall),
              const SizedBox(height: 12),
              // Rating happens once, after the trip.
              if (ride.myRating case final stars?) ...[
                Row(children: [
                  Text('You rated ${driver?.name.split(' ').first ?? 'the driver'}  ', style: text.bodyMedium),
                  StarRating(value: stars.toDouble()),
                ]),
                if (ride.myComment != null) Text('“${ride.myComment}”', style: text.bodySmall),
              ] else
                RatingForm(title: 'How was your ride with ${driver?.name.split(' ').first ?? 'the driver'}?', onSubmit: _rate),
            ]),
          ),
        );
    }
  }

  /// What the rider is told while a driver is committed to the ride.
  String _progressTitle(RideInfo ride) {
    switch (ride.status) {
      case RideStatus.driverArrived:
        return 'Your driver has arrived';
      case RideStatus.inProgress:
        return 'Trip in progress';
      default:
        final here = ride.driver?.position;
        if (here == null) return 'Driver accepted! On the way';
        final km = const Distance().as(LengthUnit.Kilometer, here, ride.pickup);
        return 'Driver is on the way · ${km.toStringAsFixed(1)} km away';
    }
  }

  Widget _endedCard(BuildContext context, IconData icon, String title, String body) => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [
              Icon(icon),
              const SizedBox(width: 8),
              Expanded(child: Text(title, style: Theme.of(context).textTheme.titleMedium)),
            ]),
            const SizedBox(height: 8),
            Text(body),
          ]),
        ),
      );
}
