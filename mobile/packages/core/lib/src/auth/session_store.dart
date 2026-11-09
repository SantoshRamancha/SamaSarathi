import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'auth_models.dart';

class Session {
  const Session({required this.token, required this.phone});

  final String token;
  final String phone;
}

/// Keeps the login token on the device so the user stays logged in between app launches.
/// Uses the phone's secure keystore (Android Keystore / iOS Keychain), not plain storage.
class SessionStore {
  SessionStore([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;
  static const _tokenKey = 'auth_token';
  static const _phoneKey = 'auth_phone';

  Future<Session?> read() async {
    final token = await _storage.read(key: _tokenKey);
    final phone = await _storage.read(key: _phoneKey);
    return token == null || phone == null ? null : Session(token: token, phone: phone);
  }

  Future<void> save(AuthResult result) async {
    await _storage.write(key: _tokenKey, value: result.token);
    await _storage.write(key: _phoneKey, value: result.phone);
  }

  Future<void> clear() => _storage.deleteAll();
}
