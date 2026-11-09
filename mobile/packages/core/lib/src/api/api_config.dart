/// Backend address. Override per build:
///   flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5299   (Android emulator)
///   flutter run --dart-define=API_BASE_URL=http://192.168.1.20:5299 (real phone on same Wi-Fi)
class ApiConfig {
  static const baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://localhost:5299',
  );
}
