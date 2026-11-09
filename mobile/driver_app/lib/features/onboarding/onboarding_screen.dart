import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/driver_models.dart';
import '../../providers.dart';
import 'steps/documents_step.dart';
import 'steps/profile_step.dart';
import 'steps/rate_step.dart';
import 'steps/vehicle_step.dart';

/// Driver registration: profile → vehicle → documents → rate → submit.
/// Each step saves to the backend on its own, so the driver can stop and continue later.
class OnboardingScreen extends ConsumerStatefulWidget {
  const OnboardingScreen({super.key, required this.initial});

  /// Null for a brand-new driver.
  final DriverMe? initial;

  @override
  ConsumerState<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends ConsumerState<OnboardingScreen> {
  late DriverMe? _me = widget.initial;
  late int _step = _firstOpenStep();
  bool _submitting = false;

  int _firstOpenStep() {
    final me = _me;
    if (me == null || me.missingSteps.isEmpty) return 0;
    return me.missingSteps.first.index;
  }

  /// Called by each step after a successful save: keep the new data and move on.
  void _saved(DriverMe me, {bool advance = true}) {
    setState(() {
      _me = me;
      if (advance && _step < OnboardingStep.values.length - 1) _step++;
    });
  }

  Future<void> _submit() async {
    setState(() => _submitting = true);
    try {
      await ref.read(driverApiProvider).submit();
      ref.invalidate(driverMeProvider); // HomeGate now shows the status screen
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString())));
        setState(() => _submitting = false);
      }
    }
  }

  StepState _state(OnboardingStep step) {
    if (_me?.isDone(step) ?? false) return StepState.complete;
    return step.index == _step ? StepState.editing : StepState.indexed;
  }

  @override
  Widget build(BuildContext context) {
    final me = _me;
    final ready = me != null && me.missingSteps.isEmpty;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Become a driver'),
        actions: [
          IconButton(
            tooltip: 'Log out',
            icon: const Icon(Icons.logout),
            onPressed: () => logout(ref, context),
          ),
        ],
      ),
      body: Column(
        children: [
          if (me != null && me.status == VerificationStatus.rejected) _ChangesRequested(me),
          Expanded(
            child: Stepper(
              currentStep: _step,
              // Later steps need the driver record that step 1 creates.
              onStepTapped: me == null ? null : (i) => setState(() => _step = i),
              controlsBuilder: (_, _) => const SizedBox.shrink(),
              steps: [
                Step(
                  title: const Text('Your details'),
                  state: _state(OnboardingStep.profile),
                  isActive: _step == 0,
                  content: ProfileStep(me: me, onSaved: _saved),
                ),
                Step(
                  title: const Text('Vehicle'),
                  state: _state(OnboardingStep.vehicle),
                  isActive: _step == 1,
                  content: VehicleStep(me: me, onSaved: _saved),
                ),
                Step(
                  title: const Text('Documents'),
                  subtitle: me == null ? null : Text('${me.documents.length} of ${DocumentType.values.length} uploaded'),
                  state: _state(OnboardingStep.documents),
                  isActive: _step == 2,
                  content: DocumentsStep(me: me, onSaved: _saved),
                ),
                Step(
                  title: const Text('Your rate'),
                  state: _state(OnboardingStep.rate),
                  isActive: _step == 3,
                  content: RateStep(me: me, onSaved: _saved),
                ),
              ],
            ),
          ),
          SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  icon: const Icon(Icons.verified_user),
                  onPressed: ready && !_submitting ? _submit : null,
                  label: Text(_submitting
                      ? 'Submitting…'
                      : ready
                          ? 'Submit for verification'
                          : 'Complete all steps to submit'),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Shown after our team sends the registration back: their message plus each failed check's note.
class _ChangesRequested extends StatelessWidget {
  const _ChangesRequested(this.me);

  final DriverMe me;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final text = Theme.of(context).textTheme;

    return Container(
      width: double.infinity,
      color: colors.errorContainer,
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(children: [
            Icon(Icons.feedback_outlined, color: colors.onErrorContainer),
            const SizedBox(width: 8),
            Text('Changes needed', style: text.titleMedium?.copyWith(color: colors.onErrorContainer)),
          ]),
          const SizedBox(height: 8),
          if (me.rejectionReason case final reason?)
            Text(reason, style: TextStyle(color: colors.onErrorContainer)),
          // The reviewer's message often already quotes these notes; don't repeat them.
          for (final check in me.failedChecks)
            if (check.notes case final notes? when !(me.rejectionReason ?? '').contains(notes))
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text('• ${check.type.label}: $notes', style: TextStyle(color: colors.onErrorContainer)),
              ),
          const SizedBox(height: 8),
          Text('Fix these below, then tap "Submit for verification" again.',
              style: text.bodySmall?.copyWith(color: colors.onErrorContainer)),
        ],
      ),
    );
  }
}
