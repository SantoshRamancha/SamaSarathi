import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/driver_models.dart';
import '../../../providers.dart';
import 'step_save_mixin.dart';

class ProfileStep extends ConsumerStatefulWidget {
  const ProfileStep({super.key, required this.me, required this.onSaved});

  final DriverMe? me;
  final OnStepSaved onSaved;

  @override
  ConsumerState<ProfileStep> createState() => _ProfileStepState();
}

class _ProfileStepState extends ConsumerState<ProfileStep> with StepSaveMixin {
  late final _name = TextEditingController(text: widget.me?.fullName);
  final _form = GlobalKey<FormState>();

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: _form,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextFormField(
            controller: _name,
            textCapitalization: TextCapitalization.words,
            decoration: const InputDecoration(
              labelText: 'Full name (as on driving licence)',
              border: OutlineInputBorder(),
            ),
            validator: (v) => (v ?? '').trim().length < 2 ? 'Enter your full name' : null,
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: saving
                ? null
                : () {
                    if (!_form.currentState!.validate()) return;
                    save(() async =>
                        widget.onSaved(await ref.read(driverApiProvider).saveProfile(_name.text.trim())));
                  },
            child: Text(saving ? 'Saving…' : 'Save & continue'),
          ),
        ],
      ),
    );
  }
}
