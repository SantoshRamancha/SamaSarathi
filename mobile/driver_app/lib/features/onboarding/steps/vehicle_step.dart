import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/driver_models.dart';
import '../../../providers.dart';
import 'step_save_mixin.dart';

class VehicleStep extends ConsumerStatefulWidget {
  const VehicleStep({super.key, required this.me, required this.onSaved});

  final DriverMe? me;
  final OnStepSaved onSaved;

  @override
  ConsumerState<VehicleStep> createState() => _VehicleStepState();
}

class _VehicleStepState extends ConsumerState<VehicleStep> with StepSaveMixin {
  late VehicleType _type = widget.me?.vehicle?.type ?? VehicleType.bike;
  late final _number = TextEditingController(text: widget.me?.vehicle?.registrationNumber);
  late final _model = TextEditingController(text: widget.me?.vehicle?.model);
  final _form = GlobalKey<FormState>();

  @override
  void dispose() {
    _number.dispose();
    _model.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: _form,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SegmentedButton<VehicleType>(
            segments: [
              for (final t in VehicleType.values) ButtonSegment(value: t, label: Text(t.label)),
            ],
            selected: {_type},
            onSelectionChanged: (s) => setState(() => _type = s.first),
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: _number,
            textCapitalization: TextCapitalization.characters,
            inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[a-zA-Z0-9 -]'))],
            decoration: const InputDecoration(
              labelText: 'Vehicle number',
              hintText: 'KA 01 AB 1234',
              border: OutlineInputBorder(),
            ),
            validator: (v) {
              final value = (v ?? '').trim();
              if (value.isEmpty) return 'Enter your vehicle number';
              if (value.length < 6) return 'Enter a valid vehicle number, e.g. KA01AB1234';
              return null;
            },
          ),
          const SizedBox(height: 12),
          TextFormField(
            controller: _model,
            decoration: const InputDecoration(
              labelText: 'Make & model (optional)',
              hintText: 'Honda Shine',
              border: OutlineInputBorder(),
            ),
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: saving || widget.me == null
                ? null
                : () {
                    if (!_form.currentState!.validate()) return;
                    save(() async => widget.onSaved(await ref
                        .read(driverApiProvider)
                        .saveVehicle(_type, _number.text.trim(), _model.text.trim())));
                  },
            child: Text(saving ? 'Saving…' : 'Save & continue'),
          ),
        ],
      ),
    );
  }
}
