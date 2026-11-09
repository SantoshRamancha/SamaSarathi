import 'package:flutter/material.dart';

/// Full-screen error with retry, used when a screen's data fails to load.
class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.message, required this.onRetry, this.onLogout});

  final String message;
  final VoidCallback onRetry;
  final VoidCallback? onLogout;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.cloud_off, size: 48, color: Theme.of(context).colorScheme.error),
            const SizedBox(height: 16),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 16),
            FilledButton(onPressed: onRetry, child: const Text('Try again')),
            if (onLogout != null) TextButton(onPressed: onLogout, child: const Text('Log out')),
          ],
        ),
      ),
    );
  }
}
