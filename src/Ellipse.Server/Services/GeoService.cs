using Ellipse.Common.Models;
using Mapbox.AspNetCore.Models;
using MapboxGeocoder = Mapbox.AspNetCore.Services.MapBoxService;

namespace Ellipse.Server.Services;

public class GeoService(MapboxGeocoder geocoder)
{
    private readonly MapboxGeocoder _geocoder = geocoder;

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
            var response = await _geocoder.ReverseGeocodingAsync(new ReverseGeocodingParameters
            {
                Coordinates = new GeoCoordinate
                {
                    Latitude = latitude,
                    Longitude = longitude
                }
            });

            return response?.Place?.Name ?? string.Empty;
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
            var response = await _geocoder.GeocodingAsync(new GeocodingParameters
            {
                Query = address
            });

            var firstResult = response?.Places?.FirstOrDefault();
            if (firstResult != null)
            {
                return (firstResult.Coordinates.Latitude, firstResult.Coordinates.Longitude);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting coordinates: {ex.Message}");
        }

        return GeoPoint2d.Zero;
    }
}