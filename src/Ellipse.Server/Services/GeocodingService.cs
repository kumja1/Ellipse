using Ellipse.Common.Enums.Geocoding;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Geocoding.CensusGeocoder;
using Ellipse.Common.Models.Geocoding.OpenRoute;
using Ellipse.Common.Models.Matrix.OpenRoute;
using Ellipse.Common.Models.Snapping.OpenRoute;
using Ellipse.Server.Utils.Clients;
using Ellipse.Server.Utils.Clients.Mapping;
using Ellipse.Server.Utils.Clients.Mapping.Geocoding;
using Osrm.HttpApiClient;
using Serilog;

namespace Ellipse.Server.Services;

public class GeocodingService(
    CensusGeocoderClient censusGeocoder,
    OpenRouteClient openRouteClient,
    OsrmHttpApiClient osrmClient,
    SupabaseCache storageClient
) : IDisposable
{
    private const string FolderName = "geocoding_cache";

    public async Task<string> GetAddressCached(double longitude, double latitude)
    {
        GeoPoint2d latLng = new(longitude, latitude);
        Log.Information(
            "[GetAddressCached] Searching cache for coordinates: {Longitude}, {Latitude}",
            longitude,
            latitude
        );

        string? cachedAddress = await storageClient.Get(latLng, FolderName);
        if (!string.IsNullOrEmpty(cachedAddress))
        {
            Log.Information(
                "[GetAddressCached] Cache hit for {Longitude}, {Latitude} - Address: {CachedAddress}",
                longitude,
                latitude,
                cachedAddress
            );
            return cachedAddress;
        }

        Log.Information(
            "[GetAddressCached] Cache miss for {Longitude}, {Latitude}. Invoking GetAddress.",
            longitude,
            latitude
        );

        string address = await GetAddressWithCensus(longitude, latitude);
        if (string.IsNullOrEmpty(address))
            address = await GetAddressWithOpenRoute(longitude, latitude);

        Log.Information(
            "[GetAddressCached] Caching address for {Longitude}, {Latitude}: {Address}",
            longitude,
            latitude,
            address
        );

        if (string.IsNullOrWhiteSpace(address))
        {
            Log.Information(
                "[GetAddressCached] No address found for coordinates: {Longitude}, {Latitude}",
                longitude,
                latitude
            );
            return string.Empty;
        }

        await storageClient.Set(latLng, address, FolderName);
        return address;
    }

    private async Task<string> GetAddressWithCensus(double longitude, double latitude)
    {
        try
        {
            Log.Information(
                "[GetAddress] Initiating reverse geocoding for coordinates: Longitude={Longitude}, Latitude={Latitude}",
                longitude,
                latitude
            );

            CensusReverseGeocodingRequest request = new()
            {
                X = longitude,
                Y = latitude,
                Benchmark = "4",
                Vintage = "4",
            };

            Log.Information("[GetAddress] ReverseGeocodeRequest created: {@Request}", request);
            CensusGeocodingResponse? response = await censusGeocoder.ReverseGeocode(request);
            Log.Information("[GetAddress] Received response: {@Response}", response);

            if (response == null)
                return string.Empty;

            AddressMatch? addressMatch = response.Result.AddressMatches.FirstOrDefault();
            string address = addressMatch != null ? addressMatch.MatchedAddress : string.Empty;

            return address;
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "[GetAddress] Error getting address for coordinates: {Longitude}, {Latitude}",
                longitude,
                latitude
            );
            return string.Empty;
        }
    }

    private async Task<string> GetAddressWithOpenRoute(double longitude, double latitude)
    {
        Log.Information(
            "[GetLatLng] No address found for coordinates: {Longitude}, {Latitude}",
            longitude,
            latitude
        );
        Log.Information("[GetLatLng] Switching to Mapbox geocoder");

        SnappedLocation? snapped = await SnapCoordinatesToRoad(longitude, latitude);
        if (snapped == null)
        {
            Log.Information(
                "[GetLatLng] No address found for coordinates: {Longitude}, {Latitude}",
                longitude,
                latitude
            );
            return string.Empty;
        }

        OpenRouteGeocodingResponse? response = await openRouteClient.ReverseGeocode(
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
        Log.Information("[GetLatLng] Received response: {@Response}", response);
        Feature? props = response
            .Features.OrderByDescending(f => f.Properties.Confidence)
            .FirstOrDefault();

        if (props == null)
        {
            Log.Information(
                "[GetLatLng] No address found for coordinates: {Longitude}, {Latitude}",
                longitude,
                latitude
            );
            return string.Empty;
        }

        return props.Properties.Label;
    }

    public async Task<GeoPoint2d> GetLatLngCached(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            Log.Information(
                "[GetLatLngCached] Provided address is empty or null. Returning GeoPoint2d.Zero."
            );
            return GeoPoint2d.Zero;
        }

        Log.Information("[GetLatLngCached] Searching cache for address: {Address}", address);
        _ = GeoPoint2d.TryParse(await storageClient.Get(address, FolderName), out GeoPoint2d latLng)
            ? latLng
            : latLng = await GetLatLngWithCensus(address);

        if (latLng == GeoPoint2d.Zero)
            latLng = await GetLatLngWithOpenRoute(address);

        if (latLng == GeoPoint2d.Zero)
        {
            Log.Information(
                "[GetLatLngCached] No coordinates found for address: {Address}. Returning GeoPoint2d.Zero.",
                address
            );
            return GeoPoint2d.Zero;
        }
        Log.Information(
            "[GetLatLngCached] Caching coordinates for address: {Address} as: {LatLng}",
            address,
            latLng
        );
        await storageClient.Set(address, latLng.ToString(), FolderName);
        return latLng;
    }

    private async Task<GeoPoint2d> GetLatLngWithCensus(string address)
    {
        try
        {
            Log.Information(
                "[GetLatLng] Initiating forward geocoding for address: {Address}",
                address
            );
            CensusGeocodingRequest request = new()
            {
                Address = address,
                SearchType = SearchType.OnelineAddress,
                ReturnType = ReturnType.Locations,
                Benchmark = "4",
                Vintage = "4",
            };

            Log.Information("[GetLatLng] ForwardGeocodeRequest created: {@Request}", request);
            CensusGeocodingResponse? response = await censusGeocoder.Geocode(request);
            Log.Information("[GetLatLng] Received response: {@Response}", response);

            if (response == null || response.Result.AddressMatches.Count == 0)
                return GeoPoint2d.Zero;

            AddressMatch? firstResult = response.Result.AddressMatches.FirstOrDefault();
            Log.Information("[GetLatLng] First geocoding result: {@FirstResult}", firstResult);
            GeoPoint2d resultPoint =
                firstResult != null
                    ? new GeoPoint2d(firstResult.Coordinates.X, firstResult.Coordinates.Y)
                    : GeoPoint2d.Zero;

            Log.Information("[GetLatLng] Returning coordinates: {ResultPoint}", resultPoint);
            return resultPoint;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GetLatLng] Error getting coordinates for address: {Address}", address);
            return GeoPoint2d.Zero;
        }
    }

    private async Task<GeoPoint2d> GetLatLngWithOpenRoute(string address)
    {
        try
        {
            Log.Information("[GetLatLng] No coordinates found for address: {Address}", address);
            Log.Information("[GetLatLng] Switching to Mapbox geocoder");

            OpenRouteGeocodingResponse? geocodeResponse = await openRouteClient.Geocode(
                new OpenRouteGeocodingRequest { Query = address, Size = 10 }
            );

            if (geocodeResponse == null)
                return GeoPoint2d.Zero;

            Feature? location = geocodeResponse
                .Features.OrderByDescending(f => f.Properties.Confidence)
                .FirstOrDefault();

            if (location == null)
            {
                Log.Information("[GetLatLng] No coordinates found for address: {Address}", address);
                return GeoPoint2d.Zero;
            }

            return new GeoPoint2d(
                location.Geometry.Coordinates[0],
                location.Geometry.Coordinates[1]
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GetLatLng] Error getting coordinates for address: {Address}", address);
            return GeoPoint2d.Zero;
        }
    }

    public async Task<TableResponse?> GetMatrixWithOSRM(
        GeoPoint2d source,
        GeoPoint2d[] destinations
    )
    {
        Log.Information(
            "Called for source: {Source} with {Count} destinations.",
            source,
            destinations.Length
        );

        if (destinations.All(dest => dest == GeoPoint2d.Zero))
            return null;

        var request = OsrmServices
            .Table(
                PredefinedProfiles.Car,
                GeographicalCoordinates.Create(
                    [
                        Coordinate.Create(source.Lon, source.Lat),
                        .. destinations.Select(dest => Coordinate.Create(dest.Lon, dest.Lat)),
                    ]
                )
            )
            .Destinations([.. Enumerable.Range(1, destinations.Length)])
            .Sources(0)
            .Annotations(TableAnnotations.DurationAndDistance)
            .Build();

        Log.Information("Request prepared. Calling MapboxClient.GetMatrixAsync...");
        TableResponse? response = await osrmClient.GetTableAsync(request);
        Log.Information("{Response}", response);

        if (response == null || response?.Durations == null || response?.Distances == null)
        {
            Log.Error("Invalid matrix response received.");
            throw new InvalidOperationException("Invalid matrix response");
        }

        Log.Information("Matrix response successfully received.");
        Log.Information("Matrix Response: {Response}", response);
        return response;
    }

    public async Task<OpenRouteMatrixResponse?> GetMatrixWithOpenRoute(
        GeoPoint2d source,
        GeoPoint2d[] destinations
    )
    {
        Log.Information(
            "Called for source: {Source} with {Count} destinations.",
            source,
            destinations.Length
        );

        if (destinations.Contains(GeoPoint2d.Zero))
            return null;

        OpenRouteMatrixRequest request = new()
        {
            Locations =
            [
                [source.Lon, source.Lat],
                .. destinations.Select(dest => new double[] { dest.Lon, dest.Lat }),
            ],
            Sources = [0],
            Destinations = [.. Enumerable.Range(1, destinations.Length)],
            Metrics = ["distance", "duration"],
            Units = "m",
            Profile = Profile.DrivingCar,
        };

        Log.Information("Request prepared. Calling OpenRouteClient.GetMatrixAsync...");
        OpenRouteMatrixResponse? response = await openRouteClient.GetMatrix(request);
        Log.Information("{Response}", response);

        if (response == null || response?.Durations == null || response?.Distances == null)
        {
            Log.Error("Invalid matrix response received.");
            throw new InvalidOperationException("Invalid matrix response");
        }

        Log.Information("Matrix response successfully received.");
        return response;
    }

    private async Task<SnappedLocation?> SnapCoordinatesToRoad(double longitude, double latitude)
    {
        OpenRouteSnappingRequest request = new()
        {
            Locations =
            [
                [longitude, latitude],
            ],
            Radius = 350,
        };

        OpenRouteSnappingResponse? response = await openRouteClient.SnapToRoads(
            request,
            Profile.DrivingCar
        );

        if (response == null || response.Locations.Count == 0)
            return null;

        SnappedLocation? snapPoint = response
            .Locations.OrderBy(snap => snap?.SnappedDistance)
            .FirstOrDefault();
        return snapPoint;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        censusGeocoder.Dispose();
        openRouteClient.Dispose();
    }
}
