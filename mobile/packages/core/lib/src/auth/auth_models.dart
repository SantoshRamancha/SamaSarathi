class UserRole {
  static const driver = 'driver';
  static const rider = 'rider';
}

class AuthResult {
  const AuthResult({
    required this.token,
    required this.phone,
    required this.role,
    required this.isRegistered,
  });

  final String token;
  final String phone;
  final String role;

  /// False the first time this number logs in to this app: show registration next.
  final bool isRegistered;

  factory AuthResult.fromJson(Map<String, dynamic> json) => AuthResult(
        token: json['token'] as String,
        phone: json['phone'] as String,
        role: json['role'] as String,
        isRegistered: json['isRegistered'] as bool,
      );
}
