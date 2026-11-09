import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../providers.dart';
import 'onboarding/onboarding_screen.dart';
import 'status/status_screen.dart';

/// Decides what a logged-in driver sees: registration while details are editable, otherwise their status.
class HomeGate extends ConsumerWidget {
  const HomeGate({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final me = ref.watch(driverMeProvider);

    return me.when(
      loading: () => const Scaffold(body: Center(child: CircularProgressIndicator())),
      error: (error, _) => Scaffold(
        body: ErrorView(
          message: error is ApiException && error.isUnauthorized
              ? 'Your session has expired. Please log in again.'
              : error.toString(),
          onRetry: () => ref.invalidate(driverMeProvider),
          onLogout: () => logout(ref, context),
        ),
      ),
      data: (driver) => driver == null || driver.canEdit
          ? OnboardingScreen(initial: driver)
          : StatusScreen(driver: driver),
    );
  }
}
