import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../../providers.dart';

/// Temporary screen to try the pricing rules. Later, distance/minutes/traffic
/// come from the maps API and the rate from the matched driver.
class FareEstimateScreen extends ConsumerStatefulWidget {
  const FareEstimateScreen({super.key});

  @override
  ConsumerState<FareEstimateScreen> createState() => _FareEstimateScreenState();
}

class _FareEstimateScreenState extends ConsumerState<FareEstimateScreen> {
  final _rate = TextEditingController(text: '10');
  final _km = TextEditingController(text: '10');
  final _minutes = TextEditingController(text: '25');
  bool _raining = false;
  bool _peak = false;

  FareQuote? _quote;
  String? _error;
  bool _loading = false;

  Future<void> _estimate() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final quote = await ref.read(apiClientProvider).estimateFare(
            FareEstimateRequest(
              ratePerKm: double.parse(_rate.text),
              distanceKm: double.parse(_km.text),
              expectedMinutes: double.parse(_minutes.text),
              isRaining: _raining,
              isPeakHour: _peak,
            ),
          );
      setState(() => _quote = quote);
    } catch (e) {
      setState(() => _error = 'Could not get fare. Is the backend running?\n$e');
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  void dispose() {
    _rate.dispose();
    _km.dispose();
    _minutes.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Fare estimate')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _numberField(_rate, "Driver's rate (₹ per km)"),
          _numberField(_km, 'Distance (km)'),
          _numberField(_minutes, 'Expected travel time (min)'),
          SwitchListTile(
            title: const Text('Raining'),
            value: _raining,
            onChanged: (v) => setState(() => _raining = v),
          ),
          SwitchListTile(
            title: const Text('Peak hour'),
            value: _peak,
            onChanged: (v) => setState(() => _peak = v),
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: _loading ? null : _estimate,
            child: Text(_loading ? 'Calculating…' : 'Get fare'),
          ),
          const SizedBox(height: 24),
          if (_error != null)
            Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          if (_quote case final q?) _QuoteCard(quote: q),
        ],
      ),
    );
  }

  Widget _numberField(TextEditingController c, String label) => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: TextField(
          controller: c,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          decoration: InputDecoration(labelText: label, border: const OutlineInputBorder()),
        ),
      );
}

class _QuoteCard extends StatelessWidget {
  const _QuoteCard({required this.quote});

  final FareQuote quote;

  @override
  Widget build(BuildContext context) {
    final byTime = quote.mode == PricingMode.time;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('₹${quote.fare.toStringAsFixed(0)}',
                style: Theme.of(context).textTheme.displaySmall),
            const SizedBox(height: 8),
            Text(byTime
                ? 'Heavy traffic / rain: charged by travel time'
                : 'Normal traffic: charged by distance'),
            const SizedBox(height: 8),
            Text('Distance fare ₹${quote.distanceFare.toStringAsFixed(0)} · '
                'Time fare ₹${quote.timeFare.toStringAsFixed(0)}'),
            const Text('No commission: the full fare goes to the driver.'),
          ],
        ),
      ),
    );
  }
}
