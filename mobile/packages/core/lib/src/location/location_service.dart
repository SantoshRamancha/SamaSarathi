import 'package:geolocator/geolocator.dart';
import 'package:latlong2/latlong.dart';

/// Thrown when the device can't give a location; [message] can be shown as-is.
class LocationException implements Exception {
  const LocationException(this.message);
  final String message;

  @override
  String toString() => message;
}

/// Where the device is right now. Wraps geolocator (GPS on phones, the browser's location on web),
/// including asking the user for permission the first time.
class LocationService {
  /// Used until we know the real location: Hyderabad city centre.
  static const fallback = LatLng(17.3850, 78.4867);

  Future<LatLng> current() async {
    if (!await Geolocator.isLocationServiceEnabled()) {
      throw const LocationException('Location is turned off. Turn it on to see where you are.');
    }

    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }
    if (permission == LocationPermission.denied || permission == LocationPermission.deniedForever) {
      throw const LocationException('Location permission was denied. Allow it in your settings to use the map.');
    }

    final position = await Geolocator.getCurrentPosition(
      locationSettings: const LocationSettings(accuracy: LocationAccuracy.high, timeLimit: Duration(seconds: 15)),
    );
    return LatLng(position.latitude, position.longitude);
  }
}
