using Ellipse.Common.Enums.Geocoding;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Geocoding;

namespace Ellipse.Server.Services;

public class GeoService(CensusGeocoderClient geocoder)
{
    private readonly CensusGeocoderClient _geocoder = geocoder;
    private readonly Dictionary<GeoPoint2d, string> _addressCache = [];

    public async Task<string> GetAddressCached(double longitude, double latitude)
    {
        var latLng = new GeoPoint2d(longitude, latitude);
        Console.WriteLine(
            $"[GetAddressCached] Searching cache for coordinates: {longitude}, {latitude}"
        );
        if (_addressCache.TryGetValue(latLng, out var cachedAddress))
        {
            Console.WriteLine(
                $"[GetAddressCached] Cache hit for {longitude}, {latitude} - Address: {cachedAddress}"
            );
            return cachedAddress;
        }
        Console.WriteLine(
            $"[GetAddressCached] Cache miss for {longitude}, {latitude}. Invoking GetAddress."
        );

        var address = await GetAddress(longitude, latitude);
        Console.WriteLine(
            $"[GetAddressCached] Caching address for {longitude}, {latitude}: {address}"
        );
        _addressCache[latLng] = address;
        return address;
    }

    private async Task<string> GetAddress(double longitude, double latitude)
    {
        try
        {
            Console.WriteLine(
                $"[GetAddress] Initiating reverse geocoding for coordinates: Longitude={longitude}, Latitude={latitude}"
            );
            var request = new ReverseGeocodingRequest
            {
                X = longitude,
                Y = latitude,
                Benchmark = "4",
                Vintage = "4",
            };

            Console.WriteLine($"[GetAddress] ReverseGeocodeRequest created: {request}");
            var response = await _geocoder.ReverseGeocode(request);
            Console.WriteLine($"[GetAddress] Received response: {response}");

            if (response == null)
            {
                Console.WriteLine(
                    $"[GetAddress] Response is null for coordinates: {longitude}, {latitude}"
                );
                return string.Empty;
            }

            var addressMatch = response.Result.AddressMatches.FirstOrDefault();
            var address = addressMatch != null ? addressMatch.MatchedAddress : string.Empty;

            if (string.IsNullOrWhiteSpace(address))
                Console.WriteLine(
                    $"[GetAddress] No address found for coordinates: {longitude}, {latitude}"
                );
            else
                Console.WriteLine($"[GetAddress] Address found: {address}");

            return address;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[GetAddress] Error getting address for coordinates: {longitude}, {latitude}. Exception: {ex.Message}"
            );
            return string.Empty;
        }
    }

    public async Task<GeoPoint2d> GetLatLngCached(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            Console.WriteLine(
                "[GetLatLngCached] Provided address is empty or null. Returning GeoPoint2d.Zero."
            );
            return GeoPoint2d.Zero;
        }

        Console.WriteLine($"[GetLatLngCached] Searching cache for address: {address}");
        GeoPoint2d latLng = _addressCache
            .FirstOrDefault(
                kvp => kvp.Value.Equals(address, StringComparison.OrdinalIgnoreCase),
                new KeyValuePair<GeoPoint2d, string>(GeoPoint2d.Zero, "")
            )
            .Key;

        if (latLng != GeoPoint2d.Zero)
        {
            Console.WriteLine(
                $"[GetLatLngCached] Cache hit for address: {address}. Coordinates: {latLng}"
            );
            return latLng;
        }

        Console.WriteLine(
            $"[GetLatLngCached] Cache miss for address: {address}. Invoking GetLatLng."
        );
        latLng = await GetLatLng(address);
        Console.WriteLine(
            $"[GetLatLngCached] Caching coordinates for address: {address} as: {latLng}"
        );
        _addressCache[latLng] = address;
        return latLng;
    }

    private async Task<GeoPoint2d> GetLatLng(string address)
    {
        try
        {
            Console.WriteLine($"[GetLatLng] Initiating forward geocoding for address: {address}");
            var request = new GeocodingRequest
            {
                Address = address,
                SearchType = SearchType.OnelineAddress,
                ReturnType = ReturnType.Locations,
                Benchmark = "4",
                Vintage = "4",
            };

            Console.WriteLine($"[GetLatLng] ForwardGeocodeRequest created: {request}");
            var response = await _geocoder.Geocode(request);
            Console.WriteLine($"[GetLatLng] Received response: {response}");

            if (response == null || response.Result.AddressMatches.Count == 0)
            {
                Console.WriteLine($"[GetLatLng] No coordinates found for address: {address}");
                return GeoPoint2d.Zero;
            }

            var firstResult = response.Result.AddressMatches.FirstOrDefault();
            Console.WriteLine($"[GetLatLng] First geocoding result: {firstResult}");
            var resultPoint =
                firstResult != null
                    ? new GeoPoint2d(firstResult.Coordinates.X, firstResult.Coordinates.Y)
                    : GeoPoint2d.Zero;

            Console.WriteLine($"[GetLatLng] Returning coordinates: {resultPoint}");
            return resultPoint;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[GetLatLng] Error getting coordinates for address: {address}. Exception: {ex.Message}"
            );
        }

        return GeoPoint2d.Zero;
    }
}
