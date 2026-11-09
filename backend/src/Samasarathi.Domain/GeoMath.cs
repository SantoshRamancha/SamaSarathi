namespace Samasarathi.Domain;

public static class GeoMath
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>Straight-line ("as the crow flies") distance between two GPS points, in km (haversine formula).</summary>
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double deg) => deg * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
