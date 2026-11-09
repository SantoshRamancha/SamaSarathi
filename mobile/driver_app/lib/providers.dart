import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import 'data/driver_api.dart';
import 'data/driver_models.dart';

/// Created in main() with any saved login token, then injected here.
final apiClientProvider = Provider<ApiClient>((ref) => throw UnimplementedError('Overridden in main()'));
final sessionStoreProvider = Provider<SessionStore>((ref) => SessionStore());

final driverApiProvider = Provider<DriverApi>((ref) => DriverApi(ref.watch(apiClientProvider)));

/// The logged-in driver. Call ref.invalidate(driverMeProvider) to reload.
final driverMeProvider = FutureProvider.autoDispose<DriverMe?>((ref) => ref.watch(driverApiProvider).me());

/// The driver's subscription. Call ref.invalidate(subscriptionProvider) to reload.
final subscriptionProvider = FutureProvider.autoDispose<DriverSubscription>((ref) => ref.watch(driverApiProvider).subscription());

Future<void> logout(WidgetRef ref, BuildContext context) async {
  // Don't leave a logged-out driver showing on riders' maps. Best effort: expiry removes them within a minute anyway.
  try {
    await ref.read(driverApiProvider).goOffline();
  } catch (_) {}
  await ref.read(sessionStoreProvider).clear();
  ref.read(apiClientProvider).token = null;
  ref.invalidate(driverMeProvider);
  if (context.mounted) context.go('/login');
}
