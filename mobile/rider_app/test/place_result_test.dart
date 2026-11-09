import 'package:flutter_test/flutter_test.dart';
import 'package:rider_app/data/ride_models.dart';

void main() {
  test('PlaceResult parses backend JSON', () {
    final p = PlaceResult.fromJson({
      'name': 'HITEC City Metro Station',
      'address': 'HITEC City Metro Station, Madhapur, Hyderabad, Telangana, India',
      'latitude': 17.4435,
      'longitude': 78.3772,
    });

    expect(p.name, 'HITEC City Metro Station');
    expect(p.address, startsWith('HITEC City Metro Station, Madhapur'));
    expect(p.position.latitude, 17.4435);
    expect(p.position.longitude, 78.3772);
  });

  test('PlaceResult tolerates a missing address', () {
    final p = PlaceResult.fromJson({'name': 'Charminar', 'latitude': 17.3616, 'longitude': 78.4747});
    expect(p.address, '');
  });
}
