import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/driver_models.dart';
import '../../providers.dart';
import '../online/online_panel.dart';
import '../subscription/subscription_screen.dart';

/// Shown after submitting: where the driver is in our verification process.
class StatusScreen extends ConsumerWidget {
  const StatusScreen({super.key, required this.driver});

  final DriverMe driver;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final text = Theme.of(context).textTheme;
    final colors = Theme.of(context).colorScheme;
    final approved = driver.status == VerificationStatus.approved;
    final suspended = driver.status == VerificationStatus.suspended;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Samasarathi Driver'),
        actions: [
          if (approved) IconButton(tooltip: 'Your rides', icon: const Icon(Icons.history), onPressed: () => context.push('/history')),
          if (approved)
            IconButton(
              tooltip: 'Subscription',
              icon: const Icon(Icons.workspace_premium_outlined),
              onPressed: () => context.push('/subscription').then((_) => ref.invalidate(subscriptionProvider)),
            ),
          IconButton(tooltip: 'Contact support', icon: const Icon(Icons.support_agent), onPressed: () => contactSupport(ref, context)),
          // Pull-to-refresh needs a touch screen; this works with a mouse too.
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.invalidate(driverMeProvider),
          ),
          IconButton(tooltip: 'Log out', icon: const Icon(Icons.logout), onPressed: () => logout(ref, context)),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(driverMeProvider.future),
        child: ListView(
          // Short content doesn't scroll on its own, and pull-to-refresh needs a scroll.
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(16),
          children: [
            Text('Hi ${driver.fullName.split(' ').first}', style: text.headlineSmall),
            const SizedBox(height: 16),
            Card(
              color: approved
                  ? colors.primaryContainer
                  : suspended
                      ? colors.errorContainer
                      : colors.secondaryContainer,
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(children: [
                      Icon(approved
                          ? Icons.verified
                          : suspended
                              ? Icons.block
                              : Icons.hourglass_top),
                      const SizedBox(width: 8),
                      Text(driver.status.label, style: text.titleLarge),
                    ]),
                    const SizedBox(height: 8),
                    Text(approved
                        ? 'You are verified. You can go online and start taking rides.'
                        : suspended
                            ? 'Your account has been suspended, so you cannot go online or take rides for now.'
                                '${driver.rejectionReason == null ? '' : '\n\nMessage from our team: ${driver.rejectionReason}'}'
                                '\n\nContact Samasarathi support to find out what happens next.'
                            : 'Our team is checking your documents and background. '
                                'This usually takes 2–3 working days. Pull down or tap the refresh icon to check for updates.'),
                    if (suspended) ...[
                      const SizedBox(height: 12),
                      FilledButton.icon(
                        icon: const Icon(Icons.support_agent),
                        label: const Text('Contact support'),
                        onPressed: () => contactSupport(ref, context, topic: 'My account has been suspended and I would like to know what happens next.'),
                      ),
                    ],
                  ],
                ),
              ),
            ),
            if (approved) ...[
              const SizedBox(height: 8),
              const SubscriptionBanner(),
              const SizedBox(height: 8),
              OnlinePanel(driver: driver),
            ],
            const SizedBox(height: 16),
            Text('Verification checks', style: text.titleMedium),
            for (final check in driver.checks)
              ListTile(
                leading: switch (check.result) {
                  CheckResult.passed => Icon(Icons.check_circle, color: colors.primary),
                  CheckResult.failed => Icon(Icons.cancel, color: colors.error),
                  CheckResult.pending => Icon(Icons.schedule, color: colors.outline),
                },
                title: Text(check.type.label),
                subtitle: check.notes == null ? null : Text(check.notes!),
                trailing: Text(check.result.name),
              ),
            const Divider(height: 32),
            Text('Your details', style: text.titleMedium),
            ListTile(title: const Text('Phone'), trailing: Text(driver.phone)),
            if (driver.vehicle case final v?)
              ListTile(title: Text('${v.type.label}${v.model == null ? '' : ' · ${v.model}'}'), trailing: Text(v.registrationNumber)),
            ListTile(title: const Text('Your rate'), trailing: Text('₹${driver.ratePerKm.toStringAsFixed(0)} / km')),
          ],
        ),
      ),
    );
  }
}
