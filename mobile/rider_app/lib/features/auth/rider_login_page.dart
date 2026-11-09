import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../../providers.dart';

class RiderLoginPage extends ConsumerWidget {
  const RiderLoginPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return LoginScreen(
      api: ref.watch(apiClientProvider),
      role: UserRole.rider,
      title: 'Samasarathi',
      subtitle: 'Fair fares from verified drivers. No surge tricks, no commission.',
      onLoggedIn: (result) async {
        await ref.read(sessionStoreProvider).save(result);
        ref.read(apiClientProvider).token = result.token;
        ref.invalidate(riderMeProvider);
        if (context.mounted) context.go(result.isRegistered ? '/' : '/profile');
      },
    );
  }
}
