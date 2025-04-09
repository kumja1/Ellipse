using Ellipse.Common.Models;
using Nominatim.API.Geocoders;
using Nominatim.API.Models;


namespace Ellipse.Server.Services;

public class GeoService(ForwardGeocoder geocoder, ReverseGeocoder reverseGeocoder)
{
    private readonly ForwardGeocoder _geocoder = geocoder;
    private readonly ReverseGeocoder _reverseGeocoder = reverseGeocoder;
    private readonly Dictionary<GeoPoint2d, string> _addressCache = [];

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
            var response = await _reverseGeocoder.ReverseGeocode(new ReverseGeocodeRequest
            {
                Latitude = latitude,
                Longitude = longitude,
            });

            Console.WriteLine($"Geocoding response: {response}");
            if (response == null)
                return string.Empty;

            var address = response.Address != null
                ? $"{response.Address.HouseNumber} {response.Address.Road}, {response.Address.City}, {response.Address.State}, {response.Address.PostCode}"
                : string.Empty;

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
            var response = await _geocoder.Geocode(new ForwardGeocodeRequest
            {
                queryString = address,
            });

            Console.WriteLine($"Geocoding response: {response}");
            if (response == null || response.Length == 0)
            {
                Console.WriteLine($"No coordinates found for address: {address}");
                return GeoPoint2d.Zero;
            }

            var firstResult = response.FirstOrDefault();
            Console.WriteLine($"First result: {firstResult}");
            return firstResult != null
                ? new GeoPoint2d(firstResult.Longitude, firstResult.Latitude)
                : GeoPoint2d.Zero;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting coordinates: {ex.Message}");
        }

        return GeoPoint2d.Zero;
    }
}