import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../../data/rider_api.dart';
import '../../providers.dart';

/// The rider's map: where they are, and the verified drivers online around them (refreshed every few seconds).
class NearbyMap extends ConsumerStatefulWidget {
  const NearbyMap({super.key});

  @override
  ConsumerState<NearbyMap> createState() => _NearbyMapState();
}

class _NearbyMapState extends ConsumerState<NearbyMap> {
  static const _refreshEvery = Duration(seconds: 5);

  final _location = LocationService();
  Timer? _timer;

  LatLng _position = LocationService.fallback;
  bool _hasPosition = false;
  List<NearbyDriver> _drivers = const [];
  String? _error;

  @override
  void initState() {
    super.initState();
    _locate();
    _timer = Timer.periodic(_refreshEvery, (_) => _loadDrivers());
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _locate() async {
    try {
      final here = await _location.current();
      if (!mounted) return;
      setState(() {
        _position = here;
        _hasPosition = true;
        _error = null;
      });
      ref.read(pickupProvider.notifier).set(here);
      await _loadDrivers();
    } on LocationException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } catch (_) {
      if (mounted) setState(() => _error = "Couldn't read your location.");
    }
  }

  Future<void> _loadDrivers() async {
    // Until we know where the rider is, there is nothing sensible to search around.
    if (!_hasPosition) return;
    try {
      final drivers = await ref.read(riderApiProvider).nearbyDrivers(_position);
      if (mounted) setState(() => _drivers = drivers);
    } catch (_) {
      // A network blip: keep showing the last drivers; the next refresh tries again.
    }
  }

  /// Debug builds only: pretend to be somewhere else (e.g. next to a test driver) on a PC or emulator.
  void _moveTo(LatLng point) {
    setState(() {
      _position = point;
      _hasPosition = true;
    });
    ref.read(pickupProvider.notifier).set(point);
    _loadDrivers();
  }

  /// Looks like a map search box; tapping it opens Book a ride with the keyboard ready.
  Widget _searchBar(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Material(
      elevation: 3,
      borderRadius: BorderRadius.circular(28),
      color: colors.surface,
      child: InkWell(
        borderRadius: BorderRadius.circular(28),
        // Needs a position to search around and to book from.
        onTap: !_hasPosition
            ? null
            : () => context.push('/book?search=1', extra: _position).then((_) => ref.invalidate(activeRideProvider)),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          child: Row(children: [
            Icon(Icons.search, color: colors.primary),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                _hasPosition ? 'Where to? Search for a place' : 'Finding your location…',
                style: TextStyle(color: colors.onSurfaceVariant),
              ),
            ),
          ]),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final count = _drivers.length;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ClipRRect(
          borderRadius: BorderRadius.circular(12),
          child: SizedBox(
            height: 280,
            child: Stack(
              children: [
                AppMap(
                  center: _position,
                  markers: [
                    for (final d in _drivers)
                      AppMarker(
                        point: d.position,
                        child: Tooltip(
                          message: '${d.vehicle} · ₹${d.ratePerKm.toStringAsFixed(0)}/km · ${d.distanceKm} km away',
                          child: CircleAvatar(
                            backgroundColor: colors.primary,
                            child: Icon(vehicleIcon(d.vehicle), color: colors.onPrimary, size: 20),
                          ),
                        ),
                      ),
                    AppMarker(point: _position, size: 28, child: const YouAreHereDot()),
                  ],
                  onTap: kDebugMode ? _moveTo : null,
                ),
                // The search bar is the first thing a rider should see; the debug hint moves out of its way.
                Positioned(left: 8, right: 8, top: 8, child: _searchBar(context)),
                const Positioned(left: 8, bottom: 8, child: DebugTapHint()),
              ],
            ),
          ),
        ),
        const SizedBox(height: 8),
        if (_error != null)
          Text(_error!, style: TextStyle(color: colors.error))
        else
          Text(
            !_hasPosition
                ? 'Finding your location…'
                : count == 0
                    ? 'No drivers nearby right now'
                    : count == 1
                        ? '1 driver nearby'
                        : '$count drivers nearby',
            style: Theme.of(context).textTheme.titleSmall,
          ),
        if (_error != null)
          TextButton(onPressed: _locate, child: const Text('Try again')),
      ],
    );
  }
}
