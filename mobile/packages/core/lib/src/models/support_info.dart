/// How to reach the Samasarathi team, as configured on the server (`GET /api/support`).
/// A field is null when the team has not set it, so the apps show only what exists.
class SupportInfo {
  const SupportInfo({this.phone, this.whatsApp, this.email, this.hours});

  final String? phone;
  final String? whatsApp;
  final String? email;
  final String? hours;

  bool get available => phone != null || whatsApp != null || email != null;

  factory SupportInfo.fromJson(Map<String, dynamic> j) => SupportInfo(
    phone: _clean(j['phone']),
    whatsApp: _clean(j['whatsApp']),
    email: _clean(j['email']),
    hours: _clean(j['hours']),
  );

  static String? _clean(Object? v) {
    final s = v is String ? v.trim() : '';
    return s.isEmpty ? null : s;
  }
}
