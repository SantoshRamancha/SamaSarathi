import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../providers.dart';

/// First-time riders only need a name; the phone number is already verified.
class ProfileSetupScreen extends ConsumerStatefulWidget {
  const ProfileSetupScreen({super.key});

  @override
  ConsumerState<ProfileSetupScreen> createState() => _ProfileSetupScreenState();
}

class _ProfileSetupScreenState extends ConsumerState<ProfileSetupScreen> {
  final _name = TextEditingController();
  final _form = GlobalKey<FormState>();
  bool _saving = false;

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _saving = true);
    try {
      await ref.read(riderApiProvider).saveProfile(_name.text.trim());
      ref.invalidate(riderMeProvider);
      if (mounted) context.go('/');
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString())));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Welcome'),
        actions: [
          // Lets a rider who entered the wrong number start over.
          IconButton(tooltip: 'Log out', icon: const Icon(Icons.logout), onPressed: () => logout(ref, context)),
        ],
      ),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            Text('What should drivers call you?', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 16),
            TextFormField(
              controller: _name,
              autofocus: true,
              textCapitalization: TextCapitalization.words,
              decoration: const InputDecoration(labelText: 'Your name', border: OutlineInputBorder()),
              validator: (v) => (v ?? '').trim().length < 2 ? 'Enter your name' : null,
              onFieldSubmitted: (_) => _save(),
            ),
            const SizedBox(height: 16),
            FilledButton(onPressed: _saving ? null : _save, child: Text(_saving ? 'Saving…' : 'Continue')),
          ],
        ),
      ),
    );
  }
}
