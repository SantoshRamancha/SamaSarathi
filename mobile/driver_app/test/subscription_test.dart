import 'package:driver_app/data/driver_models.dart';
import 'package:driver_app/features/subscription/subscription_screen.dart';
import 'package:flutter_test/flutter_test.dart';

Map<String, dynamic> _json({
  String phase = 'Active',
  bool isTrial = false,
  bool canTakeRides = true,
  bool expiringSoon = false,
  int? daysLeft = 20,
  String? validUntil = '2026-12-09T09:00:00Z',
  String? graceEndsAt,
  bool enforced = true,
}) =>
    {
      'enforced': enforced,
      'phase': phase,
      'isTrial': isTrial,
      'canTakeRides': canTakeRides,
      'expiringSoon': expiringSoon,
      'planName': 'Monthly',
      'monthlyPrice': 499,
      'paymentsEnabled': false,
      'validUntil': validUntil,
      'graceEndsAt': graceEndsAt,
      'daysLeft': daysLeft,
      'history': [
        {'kind': 'Trial', 'planName': 'Free trial', 'price': 0, 'startsAt': '2026-11-09T09:00:00Z', 'endsAt': '2026-12-09T09:00:00Z'},
        {
          'kind': 'Granted',
          'planName': 'Monthly',
          'price': 0,
          'startsAt': '2026-12-09T09:00:00Z',
          'endsAt': '2026-12-16T09:00:00Z',
          'note': 'Support case',
        },
      ],
    };

void main() {
  test('DriverSubscription parses plan, validity and history', () {
    final s = DriverSubscription.fromJson(_json(isTrial: true));

    expect(s.phase, SubscriptionPhase.active);
    expect(s.isTrial, isTrue);
    expect(s.monthlyPrice, 499);
    expect(s.daysLeft, 20);
    expect(s.history.length, 2);
    expect(s.history.first.kind, SubscriptionKind.trial);
    expect(s.history.last.kind, SubscriptionKind.granted);
    expect(s.history.last.note, 'Support case');
  });

  test('an active subscription far from its end shows no notice', () {
    final s = DriverSubscription.fromJson(_json());

    expect(subscriptionNotice(s), isNull);
    expect(s.needsAttention, isFalse);
  });

  test('an ending subscription shows a gentle warning', () {
    final s = DriverSubscription.fromJson(_json(expiringSoon: true, daysLeft: 2, isTrial: true));
    final notice = subscriptionNotice(s)!;

    expect(notice.urgent, isFalse);
    expect(notice.title, contains('trial'));
    expect(notice.body, contains('2 days'));
    expect(s.needsAttention, isTrue);
  });

  test('grace, expired and none show urgent notices and block rides', () {
    final grace = DriverSubscription.fromJson(
        _json(phase: 'Grace', canTakeRides: true, expiringSoon: true, daysLeft: null, graceEndsAt: '2026-12-12T09:00:00Z'));
    final expired = DriverSubscription.fromJson(_json(phase: 'Expired', canTakeRides: false, daysLeft: null));
    final none = DriverSubscription.fromJson(_json(phase: 'None', canTakeRides: false, daysLeft: null, validUntil: null));

    expect(subscriptionNotice(grace)!.urgent, isTrue);
    expect(subscriptionNotice(expired)!.title, contains('ended'));
    expect(subscriptionNotice(none)!.title, contains('No subscription'));
    expect(expired.canTakeRides, isFalse);
  });

  test('when subscriptions are switched off nothing is shown', () {
    final s = DriverSubscription.fromJson(_json(enforced: false, phase: 'Active'));

    expect(subscriptionNotice(s), isNull);
    expect(s.needsAttention, isFalse);
  });
}
