import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../../data/ride_models.dart';
import '../../data/rider_api.dart';
import '../../providers.dart';

/// What the booking screen starts with. Used to come back after a declined ride with the same trip already filled in.
class BookRideArgs {
  const BookRideArgs({required this.pickup, this.drop, this.excludeDriverIds = const {}});

  final LatLng pickup;

  /// Keep this destination instead of asking for one again.
  final LatLng? drop;

  /// Drivers not to offer again (the one who just declined or didn't answer).
  final Set<String> excludeDriverIds;
}

/// Book a ride: tap the map for the destination, choose one of the drivers nearby, see the price, request.
class BookRideScreen extends ConsumerStatefulWidget {
  const BookRideScreen({
    super.key,
    required this.pickup,
    this.autofocusSearch = false,
    this.initialDrop,
    this.excludeDriverIds = const {},
  });

  final LatLng pickup;

  /// A destination to start with (see [BookRideArgs]).
  final LatLng? initialDrop;
  final Set<String> excludeDriverIds;

  /// Opened from the home map's search bar: put the cursor in the search box straight away.
  final bool autofocusSearch;

  @override
  ConsumerState<BookRideScreen> createState() => _BookRideScreenState();
}

class _BookRideScreenState extends ConsumerState<BookRideScreen> {
  Timer? _timer;
  List<NearbyDriver> _drivers = const [];
  String? _selectedId;
  LatLng? _drop;
  RideQuote? _quote;
  bool _quoting = false;
  bool _requesting = false;
  String? _error;

  // Destination search
  final _searchText = TextEditingController();
  Timer? _debounce;
  List<PlaceResult> _results = const [];
  String? _searched; // the query the current results belong to
  bool _searching = false;
  String? _searchError;
  String? _dropName; // the chosen place's name; null when the drop-off was set by tapping the map

  NearbyDriver? get _selected => _drivers.where((d) => d.id == _selectedId).firstOrNull;

  @override
  void initState() {
    super.initState();
    _drop = widget.initialDrop;
    _loadDrivers();
    _timer = Timer.periodic(const Duration(seconds: 5), (_) => _loadDrivers());
  }

  @override
  void dispose() {
    _timer?.cancel();
    _debounce?.cancel();
    _searchText.dispose();
    super.dispose();
  }

  Future<void> _loadDrivers() async {
    try {
      final all = await ref.read(riderApiProvider).nearbyDrivers(widget.pickup);
      final drivers = [for (final d in all) if (!widget.excludeDriverIds.contains(d.id)) d];
      if (!mounted) return;
      final gone = _selectedId != null && drivers.every((d) => d.id != _selectedId);
      setState(() {
        _drivers = drivers;
        if (gone) {
          _selectedId = null;
          _quote = null;
          _error = 'The driver you picked is no longer available. Choose another.';
        }
      });
    } catch (_) {
      // Keep the last list; the next refresh tries again.
    }
  }

  void _pickDrop(LatLng point, {String? name}) {
    setState(() {
      _drop = point;
      _dropName = name; // null when the rider tapped the map
    });
    _refreshQuote();
  }

  /// Waits until the rider pauses typing (the search service allows about one request per second).
  void _onQueryChanged(String text) {
    _debounce?.cancel();
    final query = text.trim();
    if (query.length < 3) {
      setState(() {
        _results = const [];
        _searched = null;
        _searching = false;
        _searchError = null;
      });
      return;
    }
    setState(() => _searching = true);
    _debounce = Timer(const Duration(milliseconds: 600), () => _search(query));
  }

  Future<void> _search(String query) async {
    try {
      final results = await ref.read(riderApiProvider).searchPlaces(query, widget.pickup);
      // The rider kept typing while this was loading: that newer search will show its own results.
      if (!mounted || _searchText.text.trim() != query) return;
      setState(() {
        _results = results;
        _searched = query;
        _searchError = null;
        _searching = false;
      });
    } on ApiException catch (e) {
      if (mounted && _searchText.text.trim() == query) {
        setState(() {
          _results = const [];
          _searchError = e.message;
          _searching = false;
        });
      }
    }
  }

  void _pickPlace(PlaceResult place) {
    FocusScope.of(context).unfocus();
    _searchText.text = place.name;
    setState(() {
      _results = const [];
      _searched = null;
    });
    _pickDrop(place.position, name: place.name);
  }

  void _clearSearch() {
    _debounce?.cancel();
    _searchText.clear();
    setState(() {
      _results = const [];
      _searched = null;
      _searching = false;
      _searchError = null;
    });
  }

  /// Tap a driver to choose them; tap the chosen driver again to un-choose.
  void _pickDriver(NearbyDriver d) {
    if (_selectedId == d.id) {
      setState(() {
        _selectedId = null;
        _quote = null; // no driver, no price
        _quoting = false;
        _error = null;
      });
      return;
    }
    setState(() {
      _selectedId = d.id;
      _error = null;
    });
    _refreshQuote();
  }

  Future<void> _refreshQuote() async {
    final drop = _drop;
    final driver = _selected;
    if (drop == null || driver == null) {
      setState(() => _quote = null);
      return;
    }
    setState(() {
      _quoting = true;
      _quote = null;
      _error = null;
    });
    try {
      final quote = await ref.read(riderApiProvider).quote(driver.id, widget.pickup, drop);
      // Ignore the answer if the rider changed their mind while it was loading.
      if (mounted && _drop == drop && _selectedId == driver.id) setState(() => _quote = quote);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _quoting = false);
    }
  }

  Future<void> _request() async {
    final drop = _drop;
    final driver = _selected;
    if (drop == null || driver == null) return;
    setState(() {
      _requesting = true;
      _error = null;
    });
    try {
      final ride = await ref.read(riderApiProvider).requestRide(driver.id, widget.pickup, drop);
      if (!mounted) return;
      ref.invalidate(activeRideProvider);
      context.pushReplacement('/ride', extra: ride);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
      _loadDrivers();
    } finally {
      if (mounted) setState(() => _requesting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final text = Theme.of(context).textTheme;
    final drop = _drop;
    final quote = _quote;

    return Scaffold(
      appBar: AppBar(title: const Text('Book a ride')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
            child: TextField(
              controller: _searchText,
              autofocus: widget.autofocusSearch,
              onChanged: _onQueryChanged,
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                hintText: 'Search for a place (e.g. HITEC City metro)',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searchText.text.isEmpty
                    ? null
                    : IconButton(tooltip: 'Clear', icon: const Icon(Icons.close), onPressed: _clearSearch),
                border: const OutlineInputBorder(),
                isDense: true,
              ),
            ),
          ),
          SizedBox(
            height: 260,
            child: AppMap(
              center: widget.pickup,
              fitPoints: drop == null ? const [] : [widget.pickup, drop],
              route: quote?.route ?? const [],
              onTap: _pickDrop,
              markers: [
                for (final d in _drivers)
                  AppMarker(
                    point: d.position,
                    child: GestureDetector(
                      onTap: () => _pickDriver(d),
                      child: CircleAvatar(
                        backgroundColor: d.id == _selectedId ? colors.tertiary : colors.primary,
                        child: Icon(vehicleIcon(d.vehicle), color: colors.onPrimary, size: 20),
                      ),
                    ),
                  ),
                AppMarker(point: widget.pickup, size: 28, child: const YouAreHereDot()),
                if (drop != null) AppMarker.pin(drop, Colors.red, tooltip: 'Drop-off'),
              ],
            ),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                ..._searchResults(context),
                _Step(
                  done: drop != null,
                  label: drop == null
                      ? 'Search above or tap the map to set where you are going'
                      : _dropName != null
                          ? 'Going to $_dropName'
                          : 'Drop-off set on the map (tap to change)',
                ),
                _Step(
                  done: _selectedId != null,
                  label: _drivers.isEmpty
                      ? 'No drivers nearby right now'
                      : _selectedId == null
                          ? 'Choose a driver (tap one below or on the map)'
                          : 'Driver chosen',
                ),
                const SizedBox(height: 8),
                for (final d in _drivers)
                  Card(
                    color: d.id == _selectedId ? colors.secondaryContainer : null,
                    child: ListTile(
                      leading: Icon(vehicleIcon(d.vehicle)),
                      title: Text('${d.vehicle} · ₹${d.ratePerKm.toStringAsFixed(0)} per km'),
                      subtitle: Text('${d.distanceKm} km away'),
                      // An empty circle shows the tile can be tapped; tapping the ticked one again un-chooses it.
                      trailing: Icon(d.id == _selectedId ? Icons.check_circle : Icons.radio_button_unchecked),
                      onTap: () => _pickDriver(d),
                    ),
                  ),
                if (_quoting) const Padding(padding: EdgeInsets.all(12), child: Center(child: CircularProgressIndicator())),
                if (quote != null)
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Estimated fare', style: text.labelLarge),
                          Text('₹${quote.fare.toStringAsFixed(0)}', style: text.displaySmall),
                          Text('About ${quote.distanceKm} km · ${quote.minutes.toStringAsFixed(0)} min at ₹${quote.ratePerKm.toStringAsFixed(0)} per km'),
                          const SizedBox(height: 8),
                          _PricingNote(quote: quote),
                          const SizedBox(height: 4),
                          Text('No commission: the full fare goes to the driver.', style: text.bodySmall),
                        ],
                      ),
                    ),
                  ),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(_error!, style: TextStyle(color: colors.error)),
                  ),
                const SizedBox(height: 12),
                FilledButton(
                  onPressed: quote == null || _requesting ? null : _request,
                  child: Text(_requesting ? 'Requesting…' : 'Request ride'),
                ),
                const SizedBox(height: 16),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

extension on _BookRideScreenState {
  /// What the search box shows under it: a spinner while searching, the matches, or why there are none.
  List<Widget> _searchResults(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    if (_searchError != null) {
      return [Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(_searchError!, style: TextStyle(color: colors.error)))];
    }
    if (_searching && _results.isEmpty) {
      return const [Padding(padding: EdgeInsets.only(bottom: 8), child: LinearProgressIndicator())];
    }
    if (_results.isEmpty) {
      return [
        if (_searched != null)
          const Padding(
            padding: EdgeInsets.only(bottom: 8),
            child: Text('No places found. Try another name, or tap the map.'),
          ),
      ];
    }
    return [
      for (final p in _results)
        Card(
          child: ListTile(
            leading: const Icon(Icons.place_outlined),
            title: Text(p.name, maxLines: 1, overflow: TextOverflow.ellipsis),
            subtitle: Text(p.address, maxLines: 2, overflow: TextOverflow.ellipsis),
            onTap: () => _pickPlace(p),
          ),
        ),
      const SizedBox(height: 8),
    ];
  }
}

/// Explains how this fare was worked out: by distance, or by travel time and why, plus any estimate caveats.
class _PricingNote extends StatelessWidget {
  const _PricingNote({required this.quote});

  final RideQuote quote;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final text = Theme.of(context).textTheme;
    final reasons = [if (quote.isPeakHour) 'peak hour traffic', if (quote.isRaining) 'rain'];

    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      if (quote.mode == PricingMode.time)
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(color: colors.tertiaryContainer, borderRadius: BorderRadius.circular(8)),
          child: Row(mainAxisSize: MainAxisSize.min, children: [
            Icon(quote.isRaining ? Icons.water_drop : Icons.traffic, size: 18, color: colors.onTertiaryContainer),
            const SizedBox(width: 6),
            Flexible(
              child: Text(
                'Charged by travel time because of ${reasons.join(' and ')}. '
                'If the trip is slow you pay for the time, never less than the distance fare, and never more than 50% above this estimate.',
                style: text.bodySmall?.copyWith(color: colors.onTertiaryContainer),
              ),
            ),
          ]),
        )
      else
        Text('Charged by distance (normal traffic, dry weather).', style: text.bodySmall),
      if (!quote.isRoadRoute)
        Padding(
          padding: const EdgeInsets.only(top: 4),
          child: Text('Route unavailable right now: distance is approximate.', style: text.bodySmall?.copyWith(color: colors.error)),
        ),
      if (!quote.isWeatherKnown)
        Padding(
          padding: const EdgeInsets.only(top: 4),
          child: Text('Weather unavailable: priced as dry weather.', style: text.bodySmall?.copyWith(color: colors.outline)),
        ),
    ]);
  }
}

class _Step extends StatelessWidget {
  const _Step({required this.done, required this.label});

  final bool done;
  final String label;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 2),
        child: Row(children: [
          Icon(done ? Icons.check_circle : Icons.radio_button_unchecked,
              size: 18, color: done ? Theme.of(context).colorScheme.primary : Theme.of(context).colorScheme.outline),
          const SizedBox(width: 8),
          Expanded(child: Text(label)),
        ]),
      );
}
