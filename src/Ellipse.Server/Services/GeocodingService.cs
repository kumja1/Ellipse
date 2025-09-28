using Ellipse.Common.Enums.Geocoding;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Geocoding.CensusGeocoder;
using Ellipse.Common.Models.Geocoding.OpenRoute;
using Ellipse.Common.Models.Matrix.OpenRoute;
using Ellipse.Common.Models.Snapping.OpenRoute;
using Ellipse.Server.Utils.Clients;
using Ellipse.Server.Utils.Clients.Mapping;
using Ellipse.Server.Utils.Clients.Mapping.Geocoding;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Postgres;
using Osrm.HttpApiClient;
using Serilog;

namespace Ellipse.Server.Services;

public class GeocodingService(
    CensusGeocoderClient censusGeocoder,
    OpenRouteClient openRouteClient,
    OsrmHttpApiClient osrmClient,
    PostgresCache cache
) : IDisposable
{
    private const string CacheFolderName = "geocoding";

    public async Task<string> GetAddressCached(
        double longitude,
        double latitude,
        bool snapToRoad = true
    )
    {
        string cacheKey = $"address_{longitude}_{latitude}";
        Log.Information(
            "[GetAddressCached] Searching cache for coordinates: {Longitude}, {Latitude}",
            longitude,
            latitude
        );

        string? cachedAddress = await cache.GetStringAsync(cacheKey);
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

        string address = await GetAddressWithCensus(longitude, latitude, snapToRoad);
        if (string.IsNullOrEmpty(address))
            address = await GetAddressWithOpenRoute(longitude, latitude, snapToRoad);

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

        await cache.SetStringAsync(cacheKey, address);
        return address;
    }

    private async Task<string> GetAddressWithCensus(
        double longitude,
        double latitude,
        bool snapToRoad
    )
    {
        try
        {
            Log.Information(
                "[GetAddress] Initiating reverse geocoding for coordinates: Longitude={Longitude}, Latitude={Latitude}",
                longitude,
                latitude
            );

            if (snapToRoad)
            {
                SnappedLocation? snapped = await SnapCoordinatesToRoad(longitude, latitude);
                if (snapped != null)
                {
                    longitude = snapped.Location[0];
                    latitude = snapped.Location[1];
                    Log.Information(
                        "[GetAddress] Snapped coordinates to road: Longitude={Longitude}, Latitude={Latitude}",
                        longitude,
                        latitude
                    );
                }
            }

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

    private async Task<string> GetAddressWithOpenRoute(
        double longitude,
        double latitude,
        bool snapToRoad
    )
    {
        Log.Information(
            "[GetLatLng] No address found for coordinates: {Longitude}, {Latitude}",
            longitude,
            latitude
        );
        Log.Information("[GetLatLng] Switching to Mapbox geocoder");
        if (snapToRoad)
        {
            SnappedLocation? snapped = await SnapCoordinatesToRoad(longitude, latitude);
            if (snapped != null)
            {
                longitude = snapped.Location[0];
                latitude = snapped.Location[1];
                Log.Information(
                    "[GetLatLng] Snapped coordinates to road: Longitude={Longitude}, Latitude={Latitude}",
                    longitude,
                    latitude
                );
            }
        }

        OpenRouteGeocodingResponse? response = await openRouteClient.ReverseGeocode(
            new OpenRouteReverseGeocodingRequest
            {
                Longitude = longitude,
                Latitude = latitude,
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

        string cacheKey = $"latlng_{address.ToLower().Replace(" ", "_")}";
        string? cachedLatLng = await cache.GetStringAsync(cacheKey);
        Log.Information("[GetLatLngCached] Searching cache for address: {Address}", address);

        _ =
            GeoPoint2d.TryParse(cachedLatLng, out GeoPoint2d latLng) ? latLng
            : (latLng = await GetLatLngWithCensus(address)) == GeoPoint2d.Zero
                ? latLng = await GetLatLngWithOpenRoute(address)
            : GeoPoint2d.Zero;

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

        await cache.SetStringAsync(cacheKey, latLng.ToString());
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
                    ? new(firstResult.Coordinates.X, firstResult.Coordinates.Y)
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

    public async Task<(double[] Destinations, double[] Durations)> GetMatrixCached(
        GeoPoint2d source,
        GeoPoint2d[] destinations
    )
    {
        Log.Information(
            "[GetMatrixCached] Searching cache for source: {Source} with {Count} destinations",
            source,
            destinations.Length
        );

        HashCode hash = new();
        hash.Add(source);
        foreach (var dest in destinations)
            hash.Add(dest);

        string cacheKey = $"matrix_{hash.ToHashCode()}";
        string? cachedMatrix = await cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedMatrix))
        {
            Log.Information(
                "[GetMatrixCached] Cache hit for source: {Source} with {Count} destinations",
                source,
                destinations.Length
            );
            string[] parts = cachedMatrix.Split(';');
            double[] cachedDistances = Array.ConvertAll(
                parts[0].Split(','),
                s => double.TryParse(s, out double d) ? d : 0
            );
            double[] cachedDurations = Array.ConvertAll(
                parts[1].Split(','),
                s => double.TryParse(s, out double d) ? d : 0
            );

            return (cachedDistances, cachedDurations);
        }

        TableResponse? response = await GetMatrixWithOSRM(source, destinations)
            .ConfigureAwait(false);

        double[]? distances = response?.Distances[0].Select(x => (double)(x ?? 0)).ToArray();
        double[]? durations = response?.Durations[0].Select(x => (double)(x ?? 0)).ToArray();

        if (response is not TableResponse { Distances: not null, Durations: not null })
        {
            OpenRouteMatrixResponse? openRouteResponse = await GetMatrixWithOpenRoute(
                    source,
                    destinations
                )
                .ConfigureAwait(false);

            if (
                openRouteResponse
                is not OpenRouteMatrixResponse { Distances: not null, Durations: not null }
            )
            {
                Log.Warning("Both OSRM and OpenRouteMatrix responses are null or invalid.");
                return default;
            }

            distances = openRouteResponse.Distances[0];
            durations = openRouteResponse.Durations[0];
        }

        await cache.SetStringAsync(
            cacheKey,
            $"{string.Join(',', distances!)};{string.Join(',', durations!)}"
        );

        return (distances, durations);
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

        if (destinations.All(dest => dest == GeoPoint2d.Zero))
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
