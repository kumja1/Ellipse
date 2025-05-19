using System.Text;
using AngleSharp.Text;
using Ellipse.Common.Enums.Geocoding;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Geocoding.CensusGeocoder;
using Ellipse.Common.Models.Geocoding.OpenRoute;
using Ellipse.Common.Models.Snapping.OpenRoute;
using Ellipse.Server.Utils.Objects.Clients;
using Ellipse.Server.Utils.Objects.Clients.Geocoding;

namespace Ellipse.Server.Services;

public class GeoService(
    CensusGeocoderClient censusGeocoder,
    OpenRouteClient openRouteClient,
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
            address = await GetAddressWithOpenRoute(longitude, latitude);

        Console.WriteLine(
            $"[GetAddressCached] Caching address for {longitude}, {latitude}: {address}"
        );

        if (string.IsNullOrWhiteSpace(address))
        {
            Console.WriteLine(
                $"[GetAddressCached] No address found for coordinates: {longitude}, {latitude}"
            );
            return string.Empty;
        }

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
            var request = new CensusReverseGeocodingRequest
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
                SnappedLocation? snapped = await SnapCoordinatesToRoad(longitude, latitude);
                if (snapped == null || string.IsNullOrWhiteSpace(snapped.Name))
                    return string.Empty;
            }
            else
            {
                Console.WriteLine($"[GetAdditress] Address found: {address}");
            }

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

    private async Task<string> GetAddressWithOpenRoute(double longitude, double latitude)
    {
        Console.WriteLine($"[GetLatLng] No address found for coordinates: {longitude}, {latitude}");
        Console.WriteLine($"[GetLatLng] Switching to Mapbox geocoder");

        SnappedLocation? snapped = await SnapCoordinatesToRoad(longitude, latitude);
        if (snapped == null || string.IsNullOrWhiteSpace(snapped.Name))
        {
            Console.WriteLine(
                $"[GetLatLng] No address found for coordinates: {longitude}, {latitude}"
            );
            return string.Empty;
        }

        var response = await openRouteClient.ReverseGeocode(
            new OpenRouteReverseGeocodingRequest
            {
                Longitude = snapped.Location[0],
                Latitude = snapped.Location[1],
                Size = 10,
            }
        );

        if (response == null)
        {
            Console.WriteLine(
                $"[GetLatLng] No address found for coordinates: {longitude}, {latitude}"
            );
            return string.Empty;
        }

        var props = response.Features.OrderBy(f => f.Properties.Confidence).FirstOrDefault();
        if (props == null)
        {
            Console.WriteLine(
                $"[GetLatLng] No address found for coordinates: {longitude}, {latitude}"
            );
            return string.Empty;
        }

        return props.Properties.Label;
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
            latLng = await GetLatLngWithOpenRoute(address);

        Console.WriteLine(
            $"[GetLatLngCached] Caching coordinates for address: {address} as: {latLng}"
        );

        if (latLng == GeoPoint2d.Zero)
        {
            Console.WriteLine(
                $"[GetLatLngCached] No coordinates found for address: {address}. Returning GeoPoint2d.Zero."
            );
            return GeoPoint2d.Zero;
        }

        await storageClient.Set(address, latLng.ToString(), FolderName);
        return latLng;
    }

    private async Task<GeoPoint2d> GetLatLng(string address)
    {
        try
        {
            Console.WriteLine($"[GetLatLng] Initiating forward geocoding for address: {address}");
            var request = new CensusGeocodingRequest
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

    private async Task<GeoPoint2d> GetLatLngWithOpenRoute(string address)
    {
        try
        {
            Console.WriteLine($"[GetLatLng] No coordinates found for address: {address}");
            Console.WriteLine("[GetLatLng] Switching to Mapbox geocoder");

            var geocodeResponse = await openRouteClient.Geocode(
                new OpenRouteGeocodingRequest { Query = address, Size = 10 }
            );

            if (geocodeResponse == null)
                return GeoPoint2d.Zero;

            var location = geocodeResponse
                .Features.OrderBy(f => f.Properties.Confidence)
                .FirstOrDefault();

            if (location == null)
            {
                Console.WriteLine($"[GetLatLng] No coordinates found for address: {address}");
                return GeoPoint2d.Zero;
            }

            return new GeoPoint2d(
                location.Geometry.Coordinates[0],
                location.Geometry.Coordinates[1]
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[GetLatLng] Error getting coordinates for address: {address}. Exception: {ex.Message}, {ex.StackTrace}"
            );
            return GeoPoint2d.Zero;
        }
    }

    private async Task<SnappedLocation?> SnapCoordinatesToRoad(double longitude, double latitude)
    {
        var request = new OpenRouteSnappingRequest
        {
            Locations =
            [
                [longitude, latitude],
            ],
            Radius = 350,
        };

        var response = await openRouteClient.SnapToRoads(request, Profile.DrivingCar);
        if (response == null || response.Locations.Count == 0)
            return null;

        var snapPoint = response.Locations.OrderBy(snap => snap?.SnappedDistance).LastOrDefault();
        return snapPoint;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        censusGeocoder.Dispose();
    }
}
