import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import 'package:url_launcher/url_launcher.dart';

/// The icon for a vehicle type as the backend names it ("Bike", "Auto", "Car"); anything else shows a car.
IconData vehicleIcon(String vehicleType) => switch (vehicleType) {
      'Bike' => Icons.two_wheeler,
      'Auto' => Icons.electric_rickshaw,
      _ => Icons.directions_car,
    };

/// A coloured road line, e.g. the driver's way to the pickup drawn in orange next to the trip's blue line.
class MapRoute {
  const MapRoute(this.points, this.color);

  final List<LatLng> points;
  final Color color;
}

/// Something drawn on the map at a point.
class AppMarker {
  const AppMarker({required this.point, required this.child, this.size = 36, this.alignment = Alignment.center});

  final LatLng point;
  final Widget child;
  final double size;

  /// Which part of [child] sits on [point]. A pin's tip is at its bottom, so pins use [Alignment.topCenter].
  final Alignment alignment;

  /// A map pin whose tip is on [point]: green for pickup, red for drop-off, and so on.
  factory AppMarker.pin(LatLng point, Color color, {String? tooltip}) => AppMarker(
        point: point,
        size: 40,
        alignment: Alignment.topCenter,
        child: Tooltip(
          message: tooltip ?? '',
          child: Icon(Icons.location_on, color: color, size: 40, shadows: const [Shadow(blurRadius: 4, color: Colors.black38)]),
        ),
      );
}

/// The one map widget both apps use. Today it draws OpenStreetMap tiles via flutter_map;
/// switching to Google Maps later means changing only this file.
class AppMap extends StatefulWidget {
  const AppMap({
    super.key,
    required this.center,
    this.zoom = 15,
    this.markers = const [],
    this.onTap,
    this.fitPoints = const [],
    this.route = const [],
    this.extraRoutes = const [],
  });

  /// The map follows this point: when it changes, the map moves there.
  final LatLng center;

  /// When this list changes (and has 2+ points), the camera zooms to show them all, e.g. pickup and drop-off.
  final List<LatLng> fitPoints;

  /// A road route to draw as a line (e.g. pickup to drop-off). Empty draws nothing.
  final List<LatLng> route;

  /// More lines drawn underneath [route], each in its own colour.
  final List<MapRoute> extraRoutes;
  final double zoom;
  final List<AppMarker> markers;
  final void Function(LatLng point)? onTap;

  @override
  State<AppMap> createState() => _AppMapState();
}

class _AppMapState extends State<AppMap> {
  final _controller = MapController();

  @override
  void didUpdateWidget(AppMap old) {
    super.didUpdateWidget(old);
    if (widget.fitPoints.length >= 2 && !listEquals(old.fitPoints, widget.fitPoints)) {
      _controller.fitCamera(CameraFit.coordinates(
        coordinates: widget.fitPoints,
        padding: const EdgeInsets.all(56),
        maxZoom: 17,
      ));
    } else if (old.center != widget.center && widget.fitPoints.length < 2) {
      _controller.move(widget.center, _controller.camera.zoom);
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final onTap = widget.onTap;
    return FlutterMap(
      mapController: _controller,
      options: MapOptions(
        initialCenter: widget.center,
        initialZoom: widget.zoom,
        minZoom: 5,
        maxZoom: 19,
        onTap: onTap == null ? null : (_, point) => onTap(point),
      ),
      children: [
        TileLayer(
          // Free OpenStreetMap tiles: fine for development. Their usage policy doesn't allow heavy
          // production traffic, so before launch switch to a paid tile provider (or Google Maps).
          urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
          userAgentPackageName: 'com.samasarathi.app',
        ),
        if (widget.extraRoutes.any((r) => r.points.length >= 2))
          PolylineLayer(polylines: [
            for (final r in widget.extraRoutes)
              if (r.points.length >= 2)
                Polyline(
                  points: r.points,
                  strokeWidth: 5,
                  color: r.color.withValues(alpha: 0.85),
                  borderStrokeWidth: 2,
                  borderColor: Colors.white,
                ),
          ]),
        if (widget.route.length >= 2)
          PolylineLayer(polylines: [
            Polyline(
              points: widget.route,
              strokeWidth: 5,
              color: Colors.blue.shade700.withValues(alpha: 0.8),
              borderStrokeWidth: 2,
              borderColor: Colors.white,
            ),
          ]),
        MarkerLayer(
          markers: [
            for (final m in widget.markers) Marker(point: m.point, width: m.size, height: m.size, alignment: m.alignment, child: m.child),
          ],
        ),
        // OpenStreetMap's licence requires this credit to be visible.
        RichAttributionWidget(
          showFlutterMapAttribution: false,
          attributions: [
            TextSourceAttribution(
              'OpenStreetMap contributors',
              onTap: () => launchUrl(Uri.parse('https://openstreetmap.org/copyright')),
            ),
          ],
        ),
      ],
    );
  }
}

/// The blue "you are here" dot.
class YouAreHereDot extends StatelessWidget {
  const YouAreHereDot({super.key});

  @override
  Widget build(BuildContext context) => Container(
        decoration: BoxDecoration(
          color: Colors.blue,
          shape: BoxShape.circle,
          border: Border.all(color: Colors.white, width: 3),
          boxShadow: const [BoxShadow(blurRadius: 6, color: Colors.black26)],
        ),
      );
}

/// Shown on maps in debug builds only: explains the tap-to-move testing aid.
class DebugTapHint extends StatelessWidget {
  const DebugTapHint({super.key});

  @override
  Widget build(BuildContext context) {
    if (!kDebugMode) return const SizedBox.shrink();
    return Card(
      color: Colors.amber.shade100,
      child: const Padding(
        padding: EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        child: Text('Testing: tap the map to move yourself', style: TextStyle(fontSize: 12)),
      ),
    );
  }
}
