import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import 'features/auth/driver_login_page.dart';
import 'features/history/ride_history_screen.dart';
import 'features/home_gate.dart';
import 'features/subscription/subscription_screen.dart';
import 'providers.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Restore a previous login so the driver doesn't have to enter an OTP every launch.
  final sessionStore = SessionStore();
  final session = await sessionStore.read();

  runApp(ProviderScope(
    overrides: [
      sessionStoreProvider.overrideWithValue(sessionStore),
      apiClientProvider.overrideWithValue(ApiClient(token: session?.token)),
    ],
    child: DriverApp(loggedIn: session != null),
  ));
}

class DriverApp extends StatefulWidget {
  const DriverApp({super.key, required this.loggedIn});

  final bool loggedIn;

  @override
  State<DriverApp> createState() => _DriverAppState();
}

class _DriverAppState extends State<DriverApp> {
  late final _router = GoRouter(
    initialLocation: widget.loggedIn ? '/' : '/login',
    routes: [
      GoRoute(path: '/login', builder: (_, _) => const DriverLoginPage()),
      GoRoute(path: '/', builder: (_, _) => const HomeGate()),
      GoRoute(path: '/history', builder: (_, _) => const RideHistoryScreen()),
      GoRoute(path: '/subscription', builder: (_, _) => const SubscriptionScreen()),
    ],
  );

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'Samasarathi Driver',
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      routerConfig: _router,
    );
  }
}
