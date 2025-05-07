using System.Collections.Concurrent;
using System.Text;
using AngleSharp.Text;
using Ellipse.Common.Enums.Geocoding;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Geocoding.CensusGeocoder;
using Ellipse.Common.Models.Geocoding.CensusGeocoder.PhotonGeocoder;
using Ellipse.Server.Utils.Objects;
using Geo.ArcGIS;
using Geo.ArcGIS.Models;
using Geo.ArcGIS.Models.Parameters;
using Geo.ArcGIS.Models.Responses;

namespace Ellipse.Server.Services;

public class GeoService(
    CensusGeocoderClient censusGeocoder,
    PhotonGeocoderClient photonGeocoder,
    SupabaseStorageClient storageClient
) : IDisposable
{
    private const string FolderName = "geocoding_cache";

    public async Task<string> GetAddressCached(double longitude, double latitude)
    {
        var latLng = new GeoPoint2d(longitude, latitude);
        Console.WriteLine(
            $"[GetAddressCached] Searching cache for coordinates: {longitude}, {latitude}"
        );

        string? cachedAddress = await storageClient.Get(latLng, FolderName);
        if (!string.IsNullOrEmpty(cachedAddress))
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
        if (string.IsNullOrEmpty(address))
            address = await GetAddressWithArcGIS(longitude, latitude);

        Console.WriteLine(
            $"[GetAddressCached] Caching address for {longitude}, {latitude}: {address}"
        );

        await storageClient.Set(latLng, address, FolderName);
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
            var response = await censusGeocoder.ReverseGeocode(request);
            Console.WriteLine($"[GetAddress] Received response: {response}");

            if (response == null)
                return string.Empty;

            var addressMatch = response.Result.AddressMatches.FirstOrDefault();
            var address = addressMatch != null ? addressMatch.MatchedAddress : string.Empty;

            if (string.IsNullOrWhiteSpace(address))
            {
                Console.WriteLine(
                 $"[GetAddress] No address found for coordinates: {longitude}, {latitude}"
             );
            }
            else
            { Console.WriteLine($"[GetAdditress] Address found: {address}"); }

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

    private async Task<string> GetAddressWithArcGIS(double longitude, double latitude)
    {
        Console.WriteLine($"[GetLatLng] No address found for coordinates: {longitude}, {latitude}");
        Console.WriteLine($"[GetLatLng] Switching to Mapbox geocoder");
        var response = await photonGeocoder.ReverseGeocodeAsync(
            new PhotonReverseGeocodeRequest
            {
                Longitude = longitude,
                Latitude = latitude
            }
        );

        if (response == null)
        {
            Console.WriteLine(
                $"[GetLatLng] No address found for coordinates: {longitude}, {latitude}"
            );
            return string.Empty;
        }

        var props = response.Features[0].Properties;
        var addressParts = StringBuilderPool.Obtain();

        if (!string.IsNullOrWhiteSpace(props.Street))
            addressParts.Append(props.Street);

        if (!string.IsNullOrWhiteSpace(props.Postcode))
            addressParts.Append(props.Postcode);

        if (!string.IsNullOrWhiteSpace(props.City))
            addressParts.Append(props.City);

        if (!string.IsNullOrWhiteSpace(props.Country))
            addressParts.Append(props.Country);

        return addressParts.ToString();
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
        GeoPoint2d latLng = GeoPoint2d.From(await storageClient.Get(address, FolderName));

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
        if (latLng == GeoPoint2d.Zero)
            latLng = await GetLatLngWithArcGIS(address);

        Console.WriteLine(
            $"[GetLatLngCached] Caching coordinates for address: {address} as: {latLng}"
        );

        await storageClient.Set(address, latLng.ToString(), FolderName);
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
            var response = await censusGeocoder.Geocode(request);
            Console.WriteLine($"[GetLatLng] Received response: {response}");

            if (response == null || response.Result.AddressMatches.Count == 0)
                return GeoPoint2d.Zero;

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
            return GeoPoint2d.Zero;
        }
    }

    private async Task<GeoPoint2d> GetLatLngWithArcGIS(string address)
    {
        try
        {
            Console.WriteLine($"[GetLatLng] No coordinates found for address: {address}");
            Console.WriteLine("[GetLatLng] Switching to Mapbox geocoder");

            var geocodeResponse = await photonGeocoder.GeocodeAsync(new PhotonGeocodeRequest
            {
                Query = address
            });

            if (geocodeResponse == null)
                return GeoPoint2d.Zero;

            var location = geocodeResponse.Features[0];
            if (location == null)
            {
                Console.WriteLine($"[GetLatLng] No coordinates found for address: {address}");
                return GeoPoint2d.Zero;
            }

            return new GeoPoint2d(location.Geometry.Coordinates[0], location.Geometry.Coordinates[1]);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[GetLatLng] Error getting coordinates for address: {address}. Exception: {ex.Message}, {ex.StackTrace}"
            );
            return GeoPoint2d.Zero;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        censusGeocoder.Dispose();
    }
}
