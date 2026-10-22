namespace MS.Cobroup.Backend.Domain.Agenda;

public static class HaversineGeoCalculator
{
    private const double EarthRadiusKm = 6371.0088;

    public static decimal CalculateDistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        ValidateCoordinates(lat1, lon1);
        ValidateCoordinates(lat2, lon2);

        if (lat1 == lat2 && lon1 == lon2)
        {
            return 0m;
        }

        var dLat = DegreesToRadians((double)(lat2 - lat1));
        var dLon = DegreesToRadians((double)(lon2 - lon1));

        var lat1Rad = DegreesToRadians((double)lat1);
        var lat2Rad = DegreesToRadians((double)lat2);

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(Math.Max(0.0, 1.0 - a)));
        var distanceKm = EarthRadiusKm * c;

        return Math.Round((decimal)distanceKm, 3);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static void ValidateCoordinates(decimal lat, decimal lon)
    {
        if (lat is < -90m or > 90m)
        {
            throw new ArgumentOutOfRangeException(nameof(lat), "Latitude must be between -90 and 90 degrees.");
        }

        if (lon is < -180m or > 180m)
        {
            throw new ArgumentOutOfRangeException(nameof(lon), "Longitude must be between -180 and 180 degrees.");
        }
    }
}
