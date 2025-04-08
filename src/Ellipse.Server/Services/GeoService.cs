using CensusGeocoder;
using Ellipse.Common.Models;


namespace Ellipse.Server.Services;

public class GeoService(GeocodingService geocoder)
{
    private readonly GeocodingService _geocoder = geocoder;

    private readonly Dictionary<GeoPoint2d, string> _addressCache = [];

    public async Task<string> GetAddressCached(double latitude, double longitude)
    {

        var latLng = new GeoPoint2d(latitude, longitude);
        if (_addressCache.TryGetValue(latLng, out var cachedAddress))
            return cachedAddress;

        var address = await GetAddress(latitude, longitude);
        _addressCache[latLng] = address;
        return address;
    }

    private async Task<string> GetAddress(double latitude, double longitude)
    {
        try
        {
            var response = await _geocoder.Coordinates((decimal)latitude, (decimal)longitude);
            var address = response.addressMatches?.FirstOrDefault()?.matchedAddress ?? string.Empty;
            if (string.IsNullOrWhiteSpace(address))
                Console.WriteLine($"No address found for coordinates: {latitude}, {longitude}");
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

        latLng = await GetLatLng(address);
        _addressCache[latLng] = address;
        return latLng;
    }

    private async Task<GeoPoint2d> GetLatLng(string address)
    {
        try
        {
            var response = await _geocoder.OnelineAddressToLocation(address);
            var firstResult = response.addressMatches?.FirstOrDefault();
            if (firstResult == null)
            {
                Console.WriteLine($"No coordinates found for address: {address}");  
                return GeoPoint2d.Zero;
            }

            if (firstResult.coordinates == null)
            {
                Console.WriteLine($"Coordinates are null for address: {address}");
                return GeoPoint2d.Zero;
            }

            if (firstResult.coordinates.x == 0 && firstResult.coordinates.y == 0)
            {
                Console.WriteLine($"Coordinates are zero for address: {address}");
                return GeoPoint2d.Zero;
            }

            return (firstResult.coordinates.x, firstResult.coordinates.y);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting coordinates: {ex.Message}");
        }

        return GeoPoint2d.Zero;
    }
}