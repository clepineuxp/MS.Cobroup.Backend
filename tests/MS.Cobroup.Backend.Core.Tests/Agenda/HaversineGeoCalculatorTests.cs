using MS.Cobroup.Backend.Domain.Agenda;
using Xunit;

namespace MS.Cobroup.Backend.Core.Tests.Agenda;

public sealed class HaversineGeoCalculatorTests
{
    [Fact]
    public void CalculateDistanceKm_SamePoint_ReturnsZero()
    {
        var dist = HaversineGeoCalculator.CalculateDistanceKm(4.60971m, -74.08175m, 4.60971m, -74.08175m);
        Assert.Equal(0m, dist);
    }

    [Fact]
    public void CalculateDistanceKm_BogotaToMedellin_ReturnsExpectedApproximateDistance()
    {
        // Bogotá: 4.6097, -74.0817
        // Medellín: 6.2442, -75.5812
        // Approximate distance ~240 km
        var dist = HaversineGeoCalculator.CalculateDistanceKm(4.6097m, -74.0817m, 6.2442m, -75.5812m);
        Assert.InRange(dist, 240m, 250m);
    }

    [Fact]
    public void CalculateDistanceKm_InvalidLatitude_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HaversineGeoCalculator.CalculateDistanceKm(95m, 0m, 10m, 10m));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HaversineGeoCalculator.CalculateDistanceKm(0m, 0m, -91m, 10m));
    }

    [Fact]
    public void CalculateDistanceKm_InvalidLongitude_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HaversineGeoCalculator.CalculateDistanceKm(10m, -185m, 10m, 10m));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HaversineGeoCalculator.CalculateDistanceKm(10m, 10m, 10m, 190m));
    }
}
