import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:samasarathi_core/samasarathi_core.dart';

import 'features/auth/rider_login_page.dart';
import 'features/fare/fare_estimate_screen.dart';
import 'data/ride_models.dart';
import 'features/history/ride_history_screen.dart';
import 'features/home/home_screen.dart';
import 'features/profile/profile_setup_screen.dart';
import 'features/ride/book_ride_screen.dart';
import 'features/ride/ride_tracking_screen.dart';
import 'providers.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Restore a previous login so the rider doesn't have to enter an OTP every launch.
  final sessionStore = SessionStore();
  final session = await sessionStore.read();

  // ProviderScope holds all Riverpod state for the app.
  runApp(ProviderScope(
    overrides: [
      sessionStoreProvider.overrideWithValue(sessionStore),
      apiClientProvider.overrideWithValue(ApiClient(token: session?.token)),
    ],
    child: RiderApp(loggedIn: session != null),
  ));
}

class RiderApp extends StatefulWidget {
  const RiderApp({super.key, required this.loggedIn});

  final bool loggedIn;

  @override
  State<RiderApp> createState() => _RiderAppState();
}

class _RiderAppState extends State<RiderApp> {
  late final _router = GoRouter(
    initialLocation: widget.loggedIn ? '/' : '/login',
    routes: [
      GoRoute(path: '/login', builder: (_, _) => const RiderLoginPage()),
      GoRoute(path: '/profile', builder: (_, _) => const ProfileSetupScreen()),
      GoRoute(path: '/', builder: (_, _) => const HomeScreen()),
      GoRoute(path: '/fare', builder: (_, _) => const FareEstimateScreen()),
      GoRoute(path: '/history', builder: (_, _) => const RideHistoryScreen()),
      // These screens get their data through `extra`, which a browser reload loses: go home instead (home resumes any active ride).
      GoRoute(
        path: '/book',
        redirect: (_, state) => state.extra is LatLng || state.extra is BookRideArgs ? null : '/',
        builder: (_, state) {
          final extra = state.extra;
          final args = extra is BookRideArgs ? extra : BookRideArgs(pickup: extra! as LatLng);
          return BookRideScreen(
            pickup: args.pickup,
            initialDrop: args.drop,
            excludeDriverIds: args.excludeDriverIds,
            autofocusSearch: state.uri.queryParameters['search'] == '1',
          );
        },
      ),
      GoRoute(
        path: '/ride',
        redirect: (_, state) => state.extra is RideInfo ? null : '/',
        builder: (_, state) => RideTrackingScreen(initial: state.extra! as RideInfo),
      ),
    ],
  );

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'Samasarathi',
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      routerConfig: _router,
    );
  }
}
