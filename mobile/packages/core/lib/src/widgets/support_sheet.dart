import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../models/support_info.dart';

/// `tel:` link that opens the phone dialer.
Uri supportCallUri(String phone) =>
    Uri(scheme: 'tel', path: phone.replaceAll(RegExp(r'[^0-9+]'), ''));

/// WhatsApp chat with [message] already typed. wa.me wants the number as digits only, no "+".
Uri supportWhatsAppUri(String number, {String? message}) => Uri.https(
  'wa.me',
  '/${number.replaceAll(RegExp(r'[^0-9]'), '')}',
  message == null || message.isEmpty ? null : {'text': message},
);

/// Email with the subject and body already filled in.
Uri supportEmailUri(
  String address, {
  String subject = 'Samasarathi support',
  String? message,
}) => Uri(
  scheme: 'mailto',
  path: address,
  // Uri encodes spaces as "+" in queryParameters, which mail apps show literally, so build the query by hand.
  query: [
    'subject=${Uri.encodeComponent(subject)}',
    if (message != null && message.isNotEmpty)
      'body=${Uri.encodeComponent(message)}',
  ].join('&'),
);

/// A bottom sheet with ways to reach support: call, WhatsApp and email (only those the team has set up).
///
/// [info] is null when the details could not be loaded (for example no network).
/// [message] is typed into the WhatsApp chat and the email so the team knows who is asking and about what.
Future<void> showSupportSheet(
  BuildContext context, {
  required SupportInfo? info,
  String? message,
}) => showModalBottomSheet<void>(
  context: context,
  showDragHandle: true,
  isScrollControlled: true,
  builder: (context) => SupportSheet(info: info, message: message),
);

class SupportSheet extends StatelessWidget {
  const SupportSheet({
    super.key,
    required this.info,
    this.message,
    this.launch = _launch,
  });

  final SupportInfo? info;
  final String? message;

  /// Opens a link. Replaceable so tests don't need a phone.
  final Future<bool> Function(Uri uri) launch;

  static Future<bool> _launch(Uri uri) =>
      launchUrl(uri, mode: LaunchMode.externalApplication);

  Future<void> _open(BuildContext context, Uri uri, String what) async {
    var opened = false;
    try {
      opened = await launch(uri);
    } catch (_) {}
    if (!context.mounted) return;
    if (opened) {
      Navigator.of(context).pop();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Could not open $what on this phone.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final info = this.info;
    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Contact Samasarathi support', style: text.titleLarge),
            const SizedBox(height: 8),
            if (info == null || !info.available)
              Text(
                info == null
                    ? 'We could not load our contact details. Check your connection and try again.'
                    : 'Support contact details are not available yet. Please try again later.',
              )
            else ...[
              if (info.phone case final phone?)
                ListTile(
                  leading: const Icon(Icons.call_outlined),
                  title: const Text('Call us'),
                  subtitle: Text(phone),
                  onTap: () =>
                      _open(context, supportCallUri(phone), 'the phone app'),
                ),
              if (info.whatsApp case final whatsApp?)
                ListTile(
                  leading: const Icon(Icons.chat_outlined),
                  title: const Text('WhatsApp'),
                  subtitle: Text(whatsApp),
                  onTap: () => _open(
                    context,
                    supportWhatsAppUri(whatsApp, message: message),
                    'WhatsApp',
                  ),
                ),
              if (info.email case final email?)
                ListTile(
                  leading: const Icon(Icons.mail_outline),
                  title: const Text('Email'),
                  subtitle: Text(email),
                  onTap: () => _open(
                    context,
                    supportEmailUri(email, message: message),
                    'the email app',
                  ),
                ),
              if (info.hours case final hours?)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(hours, style: text.bodySmall),
                ),
            ],
          ],
        ),
      ),
    );
  }
}
