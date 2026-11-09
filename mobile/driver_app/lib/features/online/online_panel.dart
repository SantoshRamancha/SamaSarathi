import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../../data/driver_api.dart';
import '../../data/driver_models.dart';
import '../../providers.dart';

/// Map plus the Go online / Go offline button for an approved driver.
/// While online it reports the driver's position every few seconds so riders nearby can see them,
/// and checks for ride requests (accept or decline them here).
class OnlinePanel extends ConsumerStatefulWidget {
  const OnlinePanel({super.key, required this.driver});

  final DriverMe driver;

  @override
  ConsumerState<OnlinePanel> createState() => _OnlinePanelState();
}

class _OnlinePanelState extends ConsumerState<OnlinePanel> {
  /// The server forgets a driver after 60 s of silence, so report well inside that.
  static const _reportEvery = Duration(seconds: 5);

  final _location = LocationService();
  Timer? _timer; // reports the position
  Timer? _rideTimer; // counts down a request and asks the server for new ones
  int _tick = 0;

  /// How often (in seconds) the navigation line is refreshed from the driver's current position.
  static const _routeRefresh = 20;
  List<LatLng> _route = const []; // the road to follow; empty when there is none
  List<LatLng> _routeFit = const []; // what the camera frames for the current leg
  String? _routeKey; // ride id + status the line was fetched for
  int _routeFetchedAt = 0;
  bool _routeLoading = false;
  DriverRide? _ride;
  DriverRide? _summary; // the trip just completed, until the driver taps Done
  String? _notice;

  LatLng _position = LocationService.fallback;
  bool _hasPosition = false; // false until we have a real (or tapped) position
  bool _pinned = false; // debug only: the position was set by tapping the map, so don't overwrite it from GPS
  late bool _online = widget.driver.isOnline;
  bool _busy = false;
  String? _error;

  DriverApi get _api => ref.read(driverApiProvider);

  @override
  void initState() {
    super.initState();
    _refreshPosition();
    // The app was closed while online and reopened: carry on reporting.
    if (_online) _startReporting();
    _rideTimer = Timer.periodic(const Duration(seconds: 1), (_) => _onTick());
    _pollRide();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _rideTimer?.cancel();
    super.dispose();
  }

  /// Every second: update the countdown. Every third second: ask the server for a new request.
  void _onTick() {
    if (!mounted) return;
    setState(() => _tick++);
    if (_tick % 3 == 0 && (_online || _ride != null)) _pollRide();
    _updateRoute();
  }

  /// Keeps the navigation line on the map. Once the driver has accepted, the line leads to the pickup; after the
  /// driver arrives or the trip starts it leads to the drop-off. Fetched when the step changes, then refreshed
  /// every [_routeRefresh] so it follows the driver along the road.
  Future<void> _updateRoute() async {
    final ride = _ride;
    if (ride == null || ride.status == RideStatus.requested) {
      if (_route.isNotEmpty || _routeKey != null) {
        setState(() {
          _route = const [];
          _routeFit = const [];
          _routeKey = null;
        });
      }
      return;
    }
    final key = '${ride.id}:${ride.status.name}';
    final stepChanged = key != _routeKey;
    if (_routeLoading || (!stepChanged && _tick - _routeFetchedAt < _routeRefresh)) return;

    _routeLoading = true;
    _routeKey = key;
    _routeFetchedAt = _tick;
    try {
      final from = _position;
      final route = await _api.navigationRoute(from);
      if (!mounted || _routeKey != key) return; // the step changed while we were waiting
      setState(() {
        _route = route;
        if (stepChanged) {
          // Frame the whole leg once per step; don't re-zoom on every refresh.
          final to = ride.status == RideStatus.accepted ? ride.pickup : ride.drop;
          final start = ride.status == RideStatus.driverArrived ? ride.pickup : from;
          _routeFit = [start, to];
        }
      });
    } catch (_) {
      // Routing is a nicety: keep the previous line and retry on the next refresh.
    } finally {
      _routeLoading = false;
    }
  }

  Future<void> _pollRide() async {
    try {
      final ride = await _api.currentRide();
      if (!mounted) return;
      // A request that wasn't here a moment ago: make a sound and buzz, in case the driver isn't looking at the screen.
      if (ride != null && ride.status == RideStatus.requested && _ride?.id != ride.id) {
        SystemSound.play(SystemSoundType.alert);
        HapticFeedback.heavyImpact();
      }
      setState(() {
        if (_ride != null && ride == null) {
          _notice = 'The ride ended: the rider cancelled, or the request ran out of time.';
        } else if (ride != null) {
          _notice = null;
          _summary = null;
        }
        _ride = ride;
      });
    } catch (_) {
      // A network blip: keep what we have; the next poll tries again.
    }
  }

  /// Runs accept / decline / cancel, then shows the result.
  Future<void> _answer(Future<DriverRide> Function(String id) action) async {
    final ride = _ride;
    if (ride == null) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final updated = await action(ride.id);
      if (mounted) {
        setState(() {
          // Accepted / arrived / started stay on screen; declined, cancelled and completed end the ride.
          _ride = updated.isActive ? updated : null;
          _summary = updated.status == RideStatus.completed ? updated : null;
          _notice = null;
        });
      }
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
      await _pollRide();
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Reads the device location. Returns false (and shows why) when it can't.
  Future<bool> _refreshPosition({bool quiet = false}) async {
    if (_pinned) return true;
    try {
      final here = await _location.current();
      if (!mounted) return false;
      setState(() {
        _position = here;
        _hasPosition = true;
        if (!quiet) _error = null;
      });
      return true;
    } on LocationException catch (e) {
      if (mounted && !quiet) setState(() => _error = e.message);
      return false;
    } catch (_) {
      if (mounted && !quiet) setState(() => _error = "Couldn't read your location.");
      return false;
    }
  }

  void _startReporting() {
    _timer?.cancel();
    _timer = Timer.periodic(_reportEvery, (_) => _report());
  }

  Future<void> _report() async {
    await _refreshPosition(quiet: true);
    if (!_online || !mounted) return;
    try {
      await _api.updateLocation(_position);
    } on ApiException catch (e) {
      // 409: the server no longer considers us online (e.g. suspended). Stop pretending.
      if (e.statusCode == 409 && mounted) {
        _timer?.cancel();
        setState(() {
          _online = false;
          _error = e.message;
        });
        ref.invalidate(subscriptionProvider); // maybe the subscription ended: show the banner
      }
      // Network blips are ignored; the next tick tries again.
    }
  }

  Future<void> _toggle() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      if (_online) {
        _timer?.cancel();
        await _api.goOffline();
        if (mounted) setState(() => _online = false);
      } else {
        if (!_hasPosition && !await _refreshPosition()) return;
        await _api.goOnline(_position);
        if (!mounted) return;
        setState(() => _online = true);
        _startReporting();
      }
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
      // The refusal may be about the subscription: refresh it so the banner and button match the server.
      ref.invalidate(subscriptionProvider);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Debug builds only: lets us "drive" around on a PC or emulator that has no real GPS movement.
  void _moveTo(LatLng point) {
    setState(() {
      _position = point;
      _hasPosition = true;
      _pinned = true;
    });
    if (_online) _report();
  }

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final ride = _ride;
    // Unknown (still loading, or the call failed) counts as allowed: the server has the final say.
    final canWork = ref.watch(subscriptionProvider).value?.canTakeRides ?? true;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ClipRRect(
          borderRadius: BorderRadius.circular(12),
          child: SizedBox(
            height: 260,
            child: Stack(
              children: [
                AppMap(
                  center: _position,
                  fitPoints: ride == null ? const [] : (_routeFit.length >= 2 ? _routeFit : [ride.pickup, ride.drop]),
                  route: _route,
                  markers: [
                    if (ride != null) ...[
                      AppMarker.pin(ride.pickup, Colors.green, tooltip: 'Pickup'),
                      AppMarker.pin(ride.drop, Colors.red, tooltip: 'Drop-off'),
                    ],
                    AppMarker(point: _position, size: 28, child: const YouAreHereDot()),
                  ],
                  onTap: kDebugMode ? _moveTo : null,
                ),
                const Positioned(left: 8, top: 8, child: DebugTapHint()),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        if (ride != null) _rideCard(context, ride),
        if (ride == null && _summary != null) _summaryCard(context, _summary!),
        if (ride == null && _notice != null)
          Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(_notice!, textAlign: TextAlign.center)),
        if (_error != null)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: Text(_error!, style: TextStyle(color: colors.error)),
          ),
        Text(
          _online
              ? 'You are online. Riders within a few km can see you on their map.'
              : 'You are offline. Go online to start getting rides.',
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 8),
        _online
            ? OutlinedButton(
                // Going offline with a rider waiting would leave them stranded: answer or cancel the ride first.
                onPressed: _busy || ride != null ? null : _toggle,
                child: Text(ride != null
                    ? 'Finish or cancel your ride first'
                    : _busy
                        ? 'Please wait…'
                        : 'Go offline'),
              )
            : FilledButton(
                // The server enforces this too; disabling the button just saves a pointless tap.
                onPressed: _busy || !canWork ? null : _toggle,
                child: Text(_busy ? 'Please wait…' : canWork ? 'Go online' : 'Renew your subscription to go online'),
              ),
      ],
    );
  }

  /// A new request to answer, or the ride the driver accepted.
  Widget _rideCard(BuildContext context, DriverRide ride) {
    final text = Theme.of(context).textTheme;
    final colors = Theme.of(context).colorScheme;
    final trip = Text('Trip: about ${ride.distanceKm} km · ${ride.minutes.toStringAsFixed(0)} min\n'
        'Fare: ₹${ride.fare.toStringAsFixed(0)} (you keep all of it)'
        '${ride.pricingMode == PricingMode.time ? '\nCharged by travel time (peak hour or rain): you are paid for time stuck in traffic.' : ''}');
    final toPickup = ride.pickupDistanceKm == null ? '' : '\nPickup is ${ride.pickupDistanceKm} km from you.';

    if (ride.status == RideStatus.requested) {
      final left = ride.expiresAt?.difference(DateTime.now()).inSeconds ?? 0;
      return Card(
        color: colors.tertiaryContainer,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text('New ride request', style: text.titleMedium),
            const SizedBox(height: 4),
            Text('${ride.riderName} wants a ride.$toPickup'),
            const SizedBox(height: 4),
            trip,
            const SizedBox(height: 4),
            Text(left > 0 ? 'Answer within $left s' : 'Time is up…', style: text.labelLarge),
            const SizedBox(height: 12),
            Row(children: [
              Expanded(
                child: FilledButton(
                  onPressed: _busy ? null : () => _answer(_api.acceptRide),
                  child: const Text('Accept'),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: OutlinedButton(
                  onPressed: _busy ? null : () => _answer(_api.declineRide),
                  child: const Text('Decline'),
                ),
              ),
            ]),
          ]),
        ),
      );
    }

    // The ride the driver accepted: one primary action for the current step.
    final (title, body, actionLabel, action) = switch (ride.status) {
      RideStatus.driverArrived => (
          'Waiting for ${ride.riderName}',
          'You are at the pickup. Start the trip when the rider is in the vehicle.',
          'Start trip',
          _api.startTrip,
        ),
      RideStatus.inProgress => (
          'Trip in progress',
          'Take ${ride.riderName} to the red pin. Complete the trip at drop-off.',
          'Complete trip',
          _api.completeTrip,
        ),
      _ => (
          'Ride accepted',
          '${ride.riderName} is waiting at the green pin.$toPickup',
          "I've arrived",
          _api.arrivedAtPickup,
        ),
    };

    return Card(
      color: colors.primaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(title, style: text.titleMedium),
          const SizedBox(height: 4),
          Text(body),
          if (ride.riderPhone != null) SelectableText('Phone: ${ride.riderPhone}'),
          const SizedBox(height: 4),
          trip,
          const SizedBox(height: 12),
          FilledButton(
            onPressed: _busy ? null : () => _answer(action),
            child: Text(_busy ? 'Please wait…' : actionLabel),
          ),
          // Once the rider is on board the trip can only be completed, not cancelled.
          if (ride.status != RideStatus.inProgress) ...[
            const SizedBox(height: 8),
            OutlinedButton(
              onPressed: _busy ? null : () => _answer(_api.cancelRide),
              child: const Text('Cancel ride'),
            ),
          ],
        ]),
      ),
    );
  }

  /// Shown after Complete trip, until the driver taps Done.
  Widget _summaryCard(BuildContext context, DriverRide ride) {
    final text = Theme.of(context).textTheme;
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
          Text('Collect ₹${(ride.finalFare ?? ride.fare).toStringAsFixed(0)}', style: text.displaySmall),
          Text('${ride.riderName} · about ${ride.distanceKm} km · ${(ride.actualMinutes ?? ride.minutes).toStringAsFixed(0)} min'),
          const SizedBox(height: 4),
          Text('Paid by the rider directly (cash or UPI). No commission: you keep the full fare.', style: text.bodySmall),
          const SizedBox(height: 12),
          // Rating happens once; skipping it here is fine, it can be done later from Your rides.
          if (ride.yourRating case final stars?)
            Row(children: [
              Text('You rated ${ride.riderName.split(' ').first}  ', style: text.bodyMedium),
              StarRating(value: stars.toDouble()),
            ])
          else
            RatingForm(
              title: 'How was ${ride.riderName.split(' ').first}?',
              onSubmit: (r) async {
                final updated = await _api.rateRider(ride.id, r.stars, r.comment);
                if (mounted) setState(() => _summary = updated);
              },
            ),
          const SizedBox(height: 12),
          FilledButton(onPressed: () => setState(() => _summary = null), child: const Text('Done')),
        ]),
      ),
    );
  }
}
