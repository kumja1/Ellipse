using CensusGeocoder;
using Ellipse.Common.Models;



namespace Ellipse.Server.Services;

public class GeoService
{
    private readonly GeocodingService _geocoder;
    private readonly Dictionary<GeoPoint2d, string> _addressCache = [];

    public GeoService(GeocodingService geocoder)
    {
        _geocoder = geocoder;
        geocoder.Vintage = "4";
    }

    public async Task<string> GetAddressCached(double longitude, double latitude)
    {
        var latLng = new GeoPoint2d(longitude, latitude);
        if (_addressCache.TryGetValue(latLng, out var cachedAddress))
            return cachedAddress;

        var address = await GetAddress(longitude, latitude);
        _addressCache[latLng] = address;
        return address;
    }

    private async Task<string> GetAddress(double longitude, double latitude)
    {
        try
        {
            Console.WriteLine($"Getting address for coordinates: {longitude}, {latitude}");
            var response = await _geocoder.Coordinates((decimal)longitude, (decimal)latitude);

            Console.WriteLine($"Geocoding response: {response}");
            if (response == null || response.addressMatches.Length == 0)
            {
                Console.WriteLine($"No coordinates found for address: {longitude}, {latitude}");
                return string.Empty;
            }

            var addressMatch = response.addressMatches.FirstOrDefault();
            var address = addressMatch != null
                ? addressMatch.matchedAddress
                : string.Empty;

            Console.WriteLine($"Address: {address}");
            if (string.IsNullOrWhiteSpace(address))
                Console.WriteLine($"No address found for coordinates: {longitude}, {latitude}");

            return address;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting address: {ex.Message}");
            return string.Empty;
        }
    }


    public async Task<GeoPoint2d> GetLatLngCached(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return GeoPoint2d.Zero;

        GeoPoint2d latLng = _addressCache.FirstOrDefault(kvp => kvp.Value == address, new KeyValuePair<GeoPoint2d, string>(GeoPoint2d.Zero, "")).Key;
        if (latLng != GeoPoint2d.Zero)
            return latLng;

        Console.WriteLine($"Getting coordinates for address: {address}");
        latLng = await GetLatLng(address);
        _addressCache[latLng] = address;
        return latLng;
    }

    private async Task<GeoPoint2d> GetLatLng(string address)
    {
        try
        {
            Console.WriteLine($"Getting coordinates for address: {address}");
            var response = await _geocoder.OnelineAddressToGeography(address);

            Console.WriteLine($"Geocoding response: {response}");
            if (response == null || response.addressMatches.Length == 0)
            {
                Console.WriteLine($"No coordinates found for address: {address}");
                return GeoPoint2d.Zero;
            }

            foreach (var result in response.addressMatches)
            {
                Console.WriteLine($"Result: {result} - {result.coordinates.x}, {result.coordinates.y}");
            }

            var firstResult = response.addressMatches.FirstOrDefault();
            Console.WriteLine($"First result: {firstResult}");
            return firstResult != null
                ? new GeoPoint2d(firstResult.coordinates.x, firstResult.coordinates.y)
                : GeoPoint2d.Zero;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting coordinates: {ex.Message}");
        }

        return GeoPoint2d.Zero;
    }
}