import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../../data/driver_models.dart';
import '../../providers.dart';

const _months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

String _date(DateTime at) {
  final t = at.toLocal();
  return '${t.day} ${_months[t.month - 1]} ${t.year}';
}

String _money(double v) => '₹${v.toStringAsFixed(v.truncateToDouble() == v ? 0 : 2)}';

/// A short message about the subscription, or null when there is nothing to say.
({String title, String body, bool urgent})? subscriptionNotice(DriverSubscription s) {
  if (!s.enforced) return null;
  switch (s.phase) {
    case SubscriptionPhase.none:
      return (title: 'No subscription yet', body: 'Contact support to activate your ${s.planName} plan.', urgent: true);
    case SubscriptionPhase.expired:
      return (
        title: 'Subscription ended',
        body: 'It ended on ${_date(s.validUntil!)}. Renew to go online and take rides.',
        urgent: true,
      );
    case SubscriptionPhase.grace:
      return (
        title: 'Subscription ended: renew now',
        body: 'It ended on ${_date(s.validUntil!)}. You can keep working until ${_date(s.graceEndsAt!)}, then you will be taken offline.',
        urgent: true,
      );
    case SubscriptionPhase.active:
      if (!s.expiringSoon) return null;
      final days = s.daysLeft ?? 0;
      return (
        title: s.isTrial ? 'Your free trial is ending' : 'Your subscription is ending',
        body: days == 0 ? 'It ends today (${_date(s.validUntil!)}).' : 'It ends in $days day${days == 1 ? '' : 's'} (${_date(s.validUntil!)}).',
        urgent: false,
      );
  }
}

/// A card on the home screen that appears only when the subscription needs the driver's attention. Tap for details.
class SubscriptionBanner extends ConsumerWidget {
  const SubscriptionBanner({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colors = Theme.of(context).colorScheme;
    final sub = ref.watch(subscriptionProvider).value;
    final notice = sub == null ? null : subscriptionNotice(sub);
    if (notice == null) return const SizedBox.shrink();
    return Card(
      color: notice.urgent ? colors.errorContainer : colors.tertiaryContainer,
      child: ListTile(
        leading: Icon(notice.urgent ? Icons.warning_amber_rounded : Icons.schedule),
        title: Text(notice.title),
        subtitle: Text(notice.body),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => context.push('/subscription').then((_) => ref.invalidate(subscriptionProvider)),
      ),
    );
  }
}

/// Plan, validity and history. Payment is not online yet, so for now the driver is pointed to support.
class SubscriptionScreen extends ConsumerWidget {
  const SubscriptionScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sub = ref.watch(subscriptionProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Your subscription')),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(subscriptionProvider.future),
        child: sub.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => ListView(physics: const AlwaysScrollableScrollPhysics(), children: [
            const SizedBox(height: 120),
            Center(child: Text(e is ApiException ? e.message : e.toString())),
            Center(child: TextButton(onPressed: () => ref.invalidate(subscriptionProvider), child: const Text('Try again'))),
          ]),
          data: (s) => _content(context, s),
        ),
      ),
    );
  }

  Widget _content(BuildContext context, DriverSubscription s) {
    final text = Theme.of(context).textTheme;
    final colors = Theme.of(context).colorScheme;
    final notice = subscriptionNotice(s);

    final (headline, detail, icon) = !s.enforced
        ? ('No subscription needed', 'Subscriptions are switched off right now. You can take rides freely.', Icons.check_circle)
        : switch (s.phase) {
            SubscriptionPhase.active => (
                s.isTrial ? 'Free trial active' : 'Subscription active',
                'Valid until ${_date(s.validUntil!)}${s.daysLeft == null ? '' : ' · ${s.daysLeft} day${s.daysLeft == 1 ? '' : 's'} left'}',
                Icons.verified,
              ),
            SubscriptionPhase.grace => ('Ended: renew now', 'Ended ${_date(s.validUntil!)}. Grace until ${_date(s.graceEndsAt!)}.', Icons.hourglass_bottom),
            SubscriptionPhase.expired => ('Subscription ended', 'Ended ${_date(s.validUntil!)}.', Icons.block),
            SubscriptionPhase.none => ('No subscription yet', 'Nothing is active on your account.', Icons.info_outline),
          };

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(16),
      children: [
        Card(
          color: s.canTakeRides ? colors.primaryContainer : colors.errorContainer,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [Icon(icon), const SizedBox(width: 8), Expanded(child: Text(headline, style: text.titleLarge))]),
              const SizedBox(height: 8),
              Text(detail),
              if (notice != null && s.phase != SubscriptionPhase.active) ...[const SizedBox(height: 8), Text(notice.body)],
            ]),
          ),
        ),
        const SizedBox(height: 16),
        Text('Plan', style: text.titleMedium),
        ListTile(
          title: Text(s.planName),
          subtitle: const Text('One fixed fee. No commission on any ride: you keep the full fare.'),
          trailing: Text('${_money(s.monthlyPrice)} / month', style: text.titleMedium),
        ),
        if (s.enforced && !s.canTakeRides || (s.enforced && s.expiringSoon)) ...[
          const SizedBox(height: 8),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Text(s.paymentsEnabled
                  ? 'Renew from here.'
                  : 'Online payment is coming soon. To renew now, contact Samasarathi support and we will activate your subscription.'),
            ),
          ),
        ],
        const SizedBox(height: 16),
        Text('History', style: text.titleMedium),
        if (s.history.isEmpty)
          const ListTile(title: Text('Nothing yet'))
        else
          for (final p in s.history)
            ListTile(
              leading: Icon(switch (p.kind) {
                SubscriptionKind.trial => Icons.card_giftcard,
                SubscriptionKind.granted => Icons.volunteer_activism,
                SubscriptionKind.paid => Icons.receipt_long,
              }),
              title: Text(switch (p.kind) {
                SubscriptionKind.trial => 'Free trial',
                SubscriptionKind.granted => 'Added by Samasarathi',
                SubscriptionKind.paid => p.planName,
              }),
              subtitle: Text('${_date(p.startsAt)} to ${_date(p.endsAt)}${p.note == null ? '' : '\n${p.note}'}'),
              trailing: p.price > 0 ? Text(_money(p.price)) : null,
              isThreeLine: p.note != null,
            ),
      ],
    );
  }
}
