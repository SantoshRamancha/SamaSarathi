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

/// How to reach the support team (set on the server). Loaded when the driver taps a "Contact support" button.
final supportProvider = FutureProvider.autoDispose<SupportInfo>((ref) => ref.watch(apiClientProvider).support());

/// Opens the contact sheet. The WhatsApp chat and email start with who the driver is, plus [topic], so the team can help at once.
Future<void> contactSupport(WidgetRef ref, BuildContext context, {String? topic}) async {
  final me = ref.read(driverMeProvider).value;
  final message = [
    'Hi Samasarathi support, I am ${me?.fullName ?? 'a driver'}${me == null ? '' : ' (${me.phone})'}, a driver.',
    ?topic,
  ].join(' ');
  SupportInfo? info;
  try {
    info = await ref.refresh(supportProvider.future);
  } catch (_) {}
  if (context.mounted) await showSupportSheet(context, info: info, message: message);
}

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
