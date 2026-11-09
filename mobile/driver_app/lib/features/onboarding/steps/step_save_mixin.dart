import 'package:flutter/material.dart';

import '../../../data/driver_models.dart';

typedef OnStepSaved = void Function(DriverMe me, {bool advance});

/// Shared "busy while saving, show error if it fails" behaviour for onboarding steps.
mixin StepSaveMixin<T extends StatefulWidget> on State<T> {
  bool saving = false;

  Future<void> save(Future<void> Function() action) async {
    setState(() => saving = true);
    try {
      await action();
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString())));
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }
}
