import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import '../../providers.dart';

class DriverLoginPage extends ConsumerWidget {
  const DriverLoginPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return LoginScreen(
      api: ref.watch(apiClientProvider),
      role: UserRole.driver,
      title: 'Drive with Samasarathi',
      subtitle: 'Keep 100% of every fare. Pay one fixed monthly subscription.',
      onLoggedIn: (result) async {
        await ref.read(sessionStoreProvider).save(result);
        ref.read(apiClientProvider).token = result.token;
        ref.invalidate(driverMeProvider);
        if (context.mounted) context.go('/');
      },
    );
  }
}
