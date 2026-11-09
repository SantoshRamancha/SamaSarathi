import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../api/api_client.dart';
import 'auth_models.dart';

/// Phone number → OTP login, shared by both apps.
class LoginScreen extends StatefulWidget {
  const LoginScreen({
    super.key,
    required this.api,
    required this.role,
    required this.title,
    required this.subtitle,
    required this.onLoggedIn,
  });

  final ApiClient api;
  final String role;
  final String title;
  final String subtitle;
  final Future<void> Function(AuthResult result) onLoggedIn;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  static const _resendSeconds = 30;

  final _phone = TextEditingController();
  final _code = TextEditingController();
  bool _codeSent = false;
  bool _busy = false;
  String? _error;
  int _resendIn = 0;
  Timer? _timer;

  Future<void> _sendCode() async {
    final phone = _phone.text.trim();
    if (phone.replaceAll(RegExp(r'\D'), '').length != 10) {
      setState(() => _error = 'Enter your 10-digit mobile number');
      return;
    }
    await _run(() async {
      await widget.api.requestOtp(phone);
      _code.clear();
      setState(() => _codeSent = true);
      _startResendTimer();
    });
  }

  Future<void> _verify() async {
    if (_code.text.trim().length != 6) {
      setState(() => _error = 'Enter the 6-digit code');
      return;
    }
    await _run(() async {
      final result = await widget.api.verifyOtp(
        phone: _phone.text.trim(),
        code: _code.text.trim(),
        role: widget.role,
      );
      await widget.onLoggedIn(result);
    });
  }

  Future<void> _run(Future<void> Function() action) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await action();
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _startResendTimer() {
    _timer?.cancel();
    setState(() => _resendIn = _resendSeconds);
    _timer = Timer.periodic(const Duration(seconds: 1), (t) {
      if (_resendIn <= 1) t.cancel();
      setState(() => _resendIn--);
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _phone.dispose();
    _code.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: ListView(
              padding: const EdgeInsets.all(24),
              shrinkWrap: true,
              children: [
                Text(widget.title, style: text.headlineMedium),
                const SizedBox(height: 8),
                Text(widget.subtitle, style: text.bodyLarge),
                const SizedBox(height: 32),
                if (!_codeSent) ..._phoneStep() else ..._codeStep(text),
                if (_error != null) ...[
                  const SizedBox(height: 16),
                  Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }

  List<Widget> _phoneStep() => [
        TextField(
          controller: _phone,
          keyboardType: TextInputType.phone,
          autofocus: true,
          maxLength: 10,
          inputFormatters: [FilteringTextInputFormatter.digitsOnly],
          decoration: const InputDecoration(
            labelText: 'Mobile number',
            prefixText: '+91 ',
            border: OutlineInputBorder(),
            counterText: '',
          ),
          onSubmitted: (_) => _sendCode(),
        ),
        const SizedBox(height: 16),
        FilledButton(
          onPressed: _busy ? null : _sendCode,
          child: Text(_busy ? 'Sending…' : 'Get OTP'),
        ),
      ];

  List<Widget> _codeStep(TextTheme text) => [
        Text('Enter the 6-digit code sent to +91 ${_phone.text.trim()}', style: text.bodyMedium),
        if (kDebugMode)
          Text('Development build: the code is 123456', style: text.bodySmall),
        const SizedBox(height: 12),
        TextField(
          controller: _code,
          keyboardType: TextInputType.number,
          autofocus: true,
          maxLength: 6,
          inputFormatters: [FilteringTextInputFormatter.digitsOnly],
          style: text.headlineSmall?.copyWith(letterSpacing: 8),
          decoration: const InputDecoration(border: OutlineInputBorder(), counterText: ''),
          onSubmitted: (_) => _verify(),
        ),
        const SizedBox(height: 16),
        FilledButton(
          onPressed: _busy ? null : _verify,
          child: Text(_busy ? 'Verifying…' : 'Verify & continue'),
        ),
        const SizedBox(height: 8),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            TextButton(
              onPressed: _busy ? null : () => setState(() => _codeSent = false),
              child: const Text('Change number'),
            ),
            TextButton(
              onPressed: _busy || _resendIn > 0 ? null : _sendCode,
              child: Text(_resendIn > 0 ? 'Resend in ${_resendIn}s' : 'Resend code'),
            ),
          ],
        ),
      ];
}
