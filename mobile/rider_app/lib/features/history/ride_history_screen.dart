import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../../data/ride_models.dart';
import '../../providers.dart';

/// Past rides, newest first, with the rating the rider gave (or a button to rate a trip they skipped).
class RideHistoryScreen extends ConsumerStatefulWidget {
  const RideHistoryScreen({super.key});

  @override
  ConsumerState<RideHistoryScreen> createState() => _RideHistoryScreenState();
}

class _RideHistoryScreenState extends ConsumerState<RideHistoryScreen> {
  static const _pageSize = 20;

  final _rides = <RideHistoryItem>[];
  bool _loading = true;
  bool _hasMore = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load({bool reset = false}) async {
    if (reset) {
      _rides.clear();
      _hasMore = true;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final page = await ref.read(riderApiProvider).history(skip: _rides.length, take: _pageSize);
      if (!mounted) return;
      setState(() {
        _rides.addAll(page);
        _hasMore = page.length == _pageSize;
      });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  /// Rates a trip that was skipped at the time, then reloads the list.
  Future<void> _rate(RideHistoryItem ride) async {
    final done = await showRatingDialog(
      context,
      title: 'How was your ride with ${ride.driverName.split(' ').first}?',
      onSubmit: (r) async {
        await ref.read(riderApiProvider).rateRide(ride.id, r.stars, r.comment);
      },
    );
    if (done == true) _load(reset: true);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Your rides')),
      body: RefreshIndicator(
        onRefresh: () => _load(reset: true),
        child: _body(context),
      ),
    );
  }

  Widget _body(BuildContext context) {
    if (_rides.isEmpty) {
      if (_loading) return const Center(child: CircularProgressIndicator());
      return ListView(physics: const AlwaysScrollableScrollPhysics(), children: [
        const SizedBox(height: 120),
        Center(child: Text(_error ?? 'No rides yet. Book your first one from the home screen.')),
        if (_error != null) Center(child: TextButton(onPressed: () => _load(reset: true), child: const Text('Try again'))),
      ]);
    }
    return ListView.separated(
      physics: const AlwaysScrollableScrollPhysics(),
      itemCount: _rides.length + 1,
      separatorBuilder: (_, _) => const Divider(height: 1),
      itemBuilder: (context, i) {
        if (i == _rides.length) {
          if (_error != null) return ListTile(title: Text(_error!), trailing: TextButton(onPressed: _load, child: const Text('Retry')));
          if (_loading) return const Padding(padding: EdgeInsets.all(16), child: Center(child: CircularProgressIndicator()));
          if (_hasMore) return TextButton(onPressed: _load, child: const Text('Load more'));
          return const SizedBox.shrink();
        }
        return _tile(context, _rides[i]);
      },
    );
  }

  Widget _tile(BuildContext context, RideHistoryItem r) {
    final colors = Theme.of(context).colorScheme;
    final completed = r.status == RideStatus.completed;
    return ListTile(
      leading: Icon(completed ? Icons.check_circle : Icons.cancel_outlined, color: completed ? colors.primary : colors.outline),
      title: Text('${r.driverName}${r.vehicle == null ? '' : ' · ${r.vehicle}'}'),
      subtitle: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text('${_when(r.requestedAt)} · ${completed ? '${r.distanceKm} km' : _statusLabel(r)}'),
        if (completed && r.givenRating != null)
          Row(children: [const Text('You rated  '), StarRating(value: r.givenRating!.toDouble(), size: 14)])
        else if (completed && r.canRate)
          TextButton(
            style: TextButton.styleFrom(padding: EdgeInsets.zero, minimumSize: const Size(0, 32), alignment: Alignment.centerLeft),
            onPressed: () => _rate(r),
            child: const Text('Rate this ride'),
          ),
        if (completed && r.receivedRating != null)
          Row(children: [const Text('Driver rated you  '), StarRating(value: r.receivedRating!.toDouble(), size: 14)]),
      ]),
      trailing: completed ? Text('₹${r.fare.toStringAsFixed(0)}', style: Theme.of(context).textTheme.titleMedium) : null,
      isThreeLine: completed && (r.givenRating != null || r.canRate || r.receivedRating != null),
    );
  }

  static String _statusLabel(RideHistoryItem r) => switch (r.status) {
        RideStatus.declined => 'Driver declined',
        RideStatus.expired => 'Driver didn’t answer',
        RideStatus.cancelled => r.cancelReason ?? 'Cancelled',
        _ => r.status.name,
      };

  static const _months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

  static String _when(DateTime at) {
    final t = at.toLocal();
    final hour = t.hour % 12 == 0 ? 12 : t.hour % 12;
    return '${t.day} ${_months[t.month - 1]}, $hour:${t.minute.toString().padLeft(2, '0')} ${t.hour < 12 ? 'am' : 'pm'}';
  }
}
