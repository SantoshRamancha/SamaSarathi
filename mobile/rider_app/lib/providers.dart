import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import 'data/ride_models.dart';
import 'data/rider_api.dart';

/// Created in main() with any saved login token, then injected here.
final apiClientProvider = Provider<ApiClient>((ref) => throw UnimplementedError('Overridden in main()'));
final sessionStoreProvider = Provider<SessionStore>((ref) => SessionStore());

final riderApiProvider = Provider<RiderApi>((ref) => RiderApi(ref.watch(apiClientProvider)));

/// The logged-in rider. Call ref.invalidate(riderMeProvider) to reload.
final riderMeProvider = FutureProvider.autoDispose<RiderMe?>((ref) => ref.watch(riderApiProvider).me());

/// The ride in progress, if any, so the home screen can offer to continue it.
final activeRideProvider = FutureProvider.autoDispose<RideInfo?>((ref) => ref.watch(riderApiProvider).activeRide());

/// Where the rider is (or, in debug builds, where they tapped), shared from the home map to the booking screen.
class PickupNotifier extends Notifier<LatLng?> {
  @override
  LatLng? build() => null;

  void set(LatLng point) => state = point;
}

final pickupProvider = NotifierProvider<PickupNotifier, LatLng?>(PickupNotifier.new);

Future<void> logout(WidgetRef ref, BuildContext context) async {
  await ref.read(sessionStoreProvider).clear();
  ref.read(apiClientProvider).token = null;
  ref.invalidate(riderMeProvider);
  if (context.mounted) context.go('/login');
}
