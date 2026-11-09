import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../../../data/driver_models.dart';
import '../../../providers.dart';
import 'step_save_mixin.dart';

/// The driver sets their own per-km rate and sees what a sample trip earns in normal traffic vs. rain.
class RateStep extends ConsumerStatefulWidget {
  const RateStep({super.key, required this.me, required this.onSaved});

  final DriverMe? me;
  final OnStepSaved onSaved;

  @override
  ConsumerState<RateStep> createState() => _RateStepState();
}

class _RateStepState extends ConsumerState<RateStep> with StepSaveMixin {
  late final _rate = TextEditingController(
      text: (widget.me?.ratePerKm ?? 0) > 0 ? widget.me!.ratePerKm.toStringAsFixed(0) : '');
  ({FareQuote normal, FareQuote rain})? _preview;

  double? get _value => double.tryParse(_rate.text.trim());

  Future<void> _updatePreview() async {
    final rate = _value;
    if (rate == null || rate < 3 || rate > 100) {
      setState(() => _preview = null);
      return;
    }
    final api = ref.read(apiClientProvider);
    try {
      final (normal, rain) = await (
        api.estimateFare(FareEstimateRequest(ratePerKm: rate, distanceKm: 10, expectedMinutes: 25)),
        api.estimateFare(FareEstimateRequest(
            ratePerKm: rate, distanceKm: 10, expectedMinutes: 60, isRaining: true, trafficRatio: 2.4)),
      ).wait;
      if (mounted) setState(() => _preview = (normal: normal, rain: rain));
    } catch (_) {
      // Preview is a nice-to-have; saving still works without it.
    }
  }

  @override
  void initState() {
    super.initState();
    if (_value != null) _updatePreview();
  }

  @override
  void dispose() {
    _rate.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final rate = _value;
    final valid = rate != null && rate >= 3 && rate <= 100;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text('Set the price you charge per km in normal traffic. '
            'In peak hours or rain you are paid for your time instead, so slow traffic never means less money.'),
        const SizedBox(height: 12),
        TextField(
          controller: _rate,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'^\d{0,3}(\.\d{0,2})?'))],
          decoration: InputDecoration(
            labelText: 'Your rate (₹ per km)',
            helperText: '₹3 – ₹100 per km',
            errorText: _rate.text.isNotEmpty && !valid ? 'Enter a rate between ₹3 and ₹100' : null,
            border: const OutlineInputBorder(),
          ),
          onChanged: (_) {
            setState(() {});
            _updatePreview();
          },
        ),
        if (_preview case final p?) ...[
          const SizedBox(height: 12),
          Text('Sample 10 km trip', style: text.titleSmall),
          ListTile(
            dense: true,
            title: const Text('Normal traffic · 25 min'),
            trailing: Text('₹${p.normal.fare.toStringAsFixed(0)}', style: text.titleMedium),
          ),
          ListTile(
            dense: true,
            title: const Text('Rainy evening · 60 min'),
            trailing: Text('₹${p.rain.fare.toStringAsFixed(0)}', style: text.titleMedium),
          ),
        ],
        const SizedBox(height: 12),
        FilledButton(
          onPressed: saving || !valid || widget.me == null
              ? null
              : () => save(() async => widget.onSaved(await ref.read(driverApiProvider).saveRate(rate))),
          child: Text(saving ? 'Saving…' : 'Save rate'),
        ),
      ],
    );
  }
}
