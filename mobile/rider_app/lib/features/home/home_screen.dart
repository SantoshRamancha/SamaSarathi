import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../../data/ride_models.dart';
import '../../providers.dart';
import 'nearby_map.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final me = ref.watch(riderMeProvider);
    final pickup = ref.watch(pickupProvider);
    final activeRide = ref.watch(activeRideProvider).value;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Samasarathi'),
        actions: [
          IconButton(tooltip: 'Log out', icon: const Icon(Icons.logout), onPressed: () => logout(ref, context)),
        ],
      ),
      body: me.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorView(
          message: error is ApiException && error.isUnauthorized
              ? 'Your session has expired. Please log in again.'
              : error.toString(),
          onRetry: () => ref.invalidate(riderMeProvider),
          onLogout: () => logout(ref, context),
        ),
        data: (rider) {
          if (rider == null) {
            // Logged in but never finished setup (e.g. closed the app on the name screen).
            WidgetsBinding.instance.addPostFrameCallback((_) => context.go('/profile'));
            return const SizedBox.shrink();
          }
          final text = Theme.of(context).textTheme;
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Text('Hi ${rider.fullName.split(' ').first} 👋', style: text.headlineSmall),
              const SizedBox(height: 16),
              const NearbyMap(),
              const SizedBox(height: 16),
              if (activeRide != null)
                Card(
                  color: Theme.of(context).colorScheme.primaryContainer,
                  child: ListTile(
                    leading: const Icon(Icons.local_taxi),
                    title: const Text('Ride in progress'),
                    subtitle: Text(switch (activeRide.status) {
                      RideStatus.requested => 'Waiting for the driver to accept',
                      RideStatus.driverArrived => 'Your driver has arrived',
                      RideStatus.inProgress => 'Trip in progress',
                      _ => 'Driver is on the way. Tap to see them',
                    }),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => context.push('/ride', extra: activeRide).then((_) => ref.invalidate(activeRideProvider)),
                  ),
                )
              else
                Card(
                  child: ListTile(
                    leading: const Icon(Icons.search),
                    title: const Text('Where are you going?'),
                    subtitle: Text(pickup == null ? 'Allow location to book a ride' : 'Pick a destination and a driver'),
                    trailing: const Icon(Icons.chevron_right),
                    enabled: pickup != null,
                    onTap: pickup == null
                        ? null
                        : () => context.push('/book', extra: pickup).then((_) => ref.invalidate(activeRideProvider)),
                  ),
                ),
              const SizedBox(height: 8),
              Card(
                child: ListTile(
                  leading: const Icon(Icons.history),
                  title: const Text('Your rides'),
                  subtitle: const Text('Past trips and the ratings you gave'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => context.push('/history'),
                ),
              ),
              const SizedBox(height: 8),
              Card(
                child: ListTile(
                  leading: const Icon(Icons.calculate_outlined),
                  title: const Text('Check a fare'),
                  subtitle: const Text('See how normal, peak and rain pricing works'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => context.push('/fare'),
                ),
              ),
              const SizedBox(height: 8),
              Card(
                child: ListTile(
                  leading: const Icon(Icons.support_agent),
                  title: const Text('Contact support'),
                  subtitle: const Text('Questions, fare issues or lost items'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => contactSupport(ref, context),
                ),
              ),
              const SizedBox(height: 24),
              Text('Why Samasarathi?', style: text.titleMedium),
              const ListTile(
                leading: Icon(Icons.verified_user_outlined),
                title: Text('Every driver is background-verified'),
                subtitle: Text('Licence, police record, driving skill and behaviour'),
              ),
              const ListTile(
                leading: Icon(Icons.currency_rupee),
                title: Text('Drivers keep the whole fare'),
                subtitle: Text('No commission, so prices stay fair'),
              ),
            ],
          );
        },
      ),
    );
  }
}
