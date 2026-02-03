using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Ellipse.Common.Enums.Geocoding;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Geocoding.CensusGeocoder;
using Ellipse.Common.Models.Geocoding.OpenRoute;
using Ellipse.Common.Models.Matrix.OpenRoute;
using Ellipse.Server.Utils;
using Ellipse.Server.Utils.Clients.Mapping;
using Ellipse.Server.Utils.Clients.Mapping.Geocoding;
using Microsoft.Extensions.Caching.Distributed;
using Osrm.HttpApiClient;
using Serilog;

namespace Ellipse.Server.Services;

public class GeocodingService(
    CensusGeocoderClient censusGeocoder,
    OpenRouteClient openRouteClient,
    OsrmHttpApiClient osrmClient,
    IDistributedCache cache
)
{
    private bool _overwriteCache;

    public async Task<string> GetAddressCached(
        double longitude,
        double latitude
    )
    {
        string cacheKey = CacheHelper.CreateCacheKey("address", longitude, latitude);
        Log.Information(
            "[GetAddressCached] Searching cache for coordinates: {Longitude}, {Latitude}",
            longitude,
            latitude
        );

        if (!_overwriteCache)
        {
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
        }

        Log.Information(
            "[GetAddressCached] Cache miss for {Longitude}, {Latitude}. Invoking GetAddressCached.",
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

        await cache.SetStringAsync(cacheKey, address);
        return address;
    }

    private async Task<string> GetAddressWithCensus(
        double longitude,
        double latitude
    )
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
                State = "VA"
            };

            Log.Information("[GetAddress] ReverseGeocodeRequest created: {@Request}", request);
            CensusGeocodingResponse response = await censusGeocoder.ReverseGeocode(request);
            Log.Information("[GetAddress] Received response: {@Response}", response);

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
        double latitude
    )
    {
        Log.Information(
            "[GetLatLng] No address found for coordinates: {Longitude}, {Latitude}",
            longitude,
            latitude
        );
        Log.Information("[GetLatLng] Switching to Mapbox geocoder");

        OpenRouteGeocodingResponse response = await openRouteClient.ReverseGeocode(
            new OpenRouteReverseGeocodingRequest
            {
                Longitude = longitude,
                Latitude = latitude,
                BoundaryCircleRadius = .45,
                BoundaryCountry = ["US"],
                Size = 10,
            }
        );


        Log.Information("[GetLatLng] Received response: {@Response}", response);
        Feature? props = response
            .Features.Where(feature => feature.Properties.RegionA == "VA")
            .OrderByDescending(f => f.Properties.Confidence)
            .FirstOrDefault();

        if (props != null)
            return props.Properties.Label;

        Log.Information(
            "[GetLatLng] No address found for coordinates: {Longitude}, {Latitude}",
            longitude,
            latitude
        );

        return string.Empty;
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

        string cacheKey = CacheHelper.CreateCacheKey("latLng", address.ToLower().Replace(" ", "_"));
        if (!_overwriteCache)
        {
            string? cachedLatLng = await cache.GetStringAsync(cacheKey);
            Log.Information("[GetLatLngCached] Searching cache for address: {Address}", address);

            if (!string.IsNullOrEmpty(cachedLatLng))
                return GeoPoint2d.Parse(cachedLatLng);
        }

        GeoPoint2d censusGeoPoint2d = await GetLatLngWithCensus(address);
        GeoPoint2d latLng = censusGeoPoint2d == GeoPoint2d.Zero
            ? await GetLatLngWithOpenRoute(address)
            : censusGeoPoint2d;

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
            CensusGeocodingResponse response = await censusGeocoder.Geocode(request);
            Log.Information("[GetLatLng] Received response: {@Response}", response);

            if (response.Result.AddressMatches.Count == 0)
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

            OpenRouteGeocodingResponse geocodeResponse = await openRouteClient.Geocode(
                new OpenRouteGeocodingRequest { Query = address, Size = 10 }
            );

            Feature? location = geocodeResponse
                .Features.OrderByDescending(f => f.Properties.Confidence)
                .FirstOrDefault();

            if (location != null)
                return new GeoPoint2d(
                    location.Geometry.Coordinates[0],
                    location.Geometry.Coordinates[1]
                );

            Log.Information("[GetLatLng] No coordinates found for address: {Address}", address);
            return GeoPoint2d.Zero;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GetLatLng] Error getting coordinates for address: {Address}", address);
            return GeoPoint2d.Zero;
        }
    }

    public async Task<(double[][] Destinations, double[][] Durations)> GetMatrixCached(
        GeoPoint2d[] sources,
        GeoPoint2d[] destinations
    )
    {
        Log.Information(
            "[GetMatrixCached] Searching cache for {SourceCount} sources with {DestCount} destinations",
            sources.Length,
            destinations.Length
        );

        string cacheKey = CacheHelper.CreateCacheKey("matrix", sources, destinations);

        if (!_overwriteCache)
        {
            string? cachedMatrix = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedMatrix))
            {
                Log.Information(
                    "[GetMatrixCached] Cache hit for {SourceCount} sources",
                    sources.Length
                );

                return JsonSerializer.Deserialize<(double[][], double[][])>(CacheHelper.DecompressData(cachedMatrix));
            }
        }

        TableResponse? response = await GetMatrixWithOsrm(sources, destinations)
            .ConfigureAwait(false);

        double[][] resultDistances;
        double[][] resultDurations;

        if (response is not null)
        {
            resultDistances = response.Distances.Select(row => row?.Select(d => (double)(d ?? 0)).ToArray() ?? [])
                .ToArray();
            resultDurations = response.Durations.Select(row => row?.Select(d => (double)(d ?? 0)).ToArray() ?? [])
                .ToArray();
        }
        else
        {
            OpenRouteMatrixResponse? openRouteResponse = await GetMatrixWithOpenRoute(
                    sources,
                    destinations
                )
                .ConfigureAwait(false);

            if (openRouteResponse is null)
            {
                Log.Warning("Both OSRM and OpenRouteMatrix responses are null or invalid.");
                return ([], []);
            }

            resultDistances = openRouteResponse.Distances?.Select(row => row.Select(d => (double)d).ToArray() ?? [])
                .ToArray() ?? [];
            resultDurations = openRouteResponse.Durations?.Select(row => row.Select(d => (double)d).ToArray() ?? [])
                .ToArray() ?? [];
        }

        await cache.SetStringAsync(
            cacheKey,
            CacheHelper.CompressData(JsonSerializer.Serialize((resultDistances, resultDurations)))
        );

        return (resultDistances, resultDurations);
    }

    private async Task<TableResponse?> GetMatrixWithOsrm(
        GeoPoint2d[] sources,
        GeoPoint2d[] destinations
    )
    {
        if (destinations.Any(dest => dest == GeoPoint2d.Zero))
        {
            Log.Information("No destinations provided. Returning null.");
            return null;
        }

        TableRequest<JsonFormat> request = OsrmServices
            .Table(
                PredefinedProfiles.Car,
                GeographicalCoordinates.Create(
                    [
                        .. sources.Select(source => Coordinate.Create(source.Lon, source.Lat)),
                        .. destinations.Select(dest => Coordinate.Create(dest.Lon, dest.Lat)),
                    ]
                )
            )
            .Destinations([.. Enumerable.Range(sources.Length, destinations.Length)])
            .Sources([.. Enumerable.Range(0, sources.Length)])
            .Annotations(TableAnnotations.DurationAndDistance)
            .Build();

        Log.Information("Request prepared. Calling MapboxClient.GetMatrixAsync...");
        OsrmHttpApiResponse<TableResponse> response = await osrmClient.GetTableAsync(request);
        Log.Information("{Response}", response);

        if (!response.IsSuccess)
        {
            Log.Error("Invalid matrix response received.");
            throw new InvalidDataException("Invalid matrix response");
        }

        Log.Information("Matrix response successfully received.");
        Log.Information("Matrix Response: {Response}", response.Result);
        return response.Result;
    }

    private async Task<OpenRouteMatrixResponse?> GetMatrixWithOpenRoute(
        GeoPoint2d[] sources,
        GeoPoint2d[] destinations
    )
    {
        Log.Information(
            "Called for {SourceCount} sources with {DestCount} destinations.",
            sources.Length,
            destinations.Length
        );

        if (destinations.All(dest => dest == GeoPoint2d.Zero))
            return null;

        OpenRouteMatrixRequest request = new()
        {
            Locations =
            [
                .. sources.Select(source => new[] { source.Lon, source.Lat }),
                .. destinations.Select(dest => new[] { dest.Lon, dest.Lat }),
            ],
            Sources = [.. Enumerable.Range(0, sources.Length)],
            Destinations = [.. Enumerable.Range(sources.Length, destinations.Length)],
            Metrics = ["distance", "duration"],
            Units = "m",
            Profile = Profile.DrivingCar,
        };

        Log.Information("Request prepared. Calling OpenRouteClient.GetMatrixAsync...");
        OpenRouteMatrixResponse response = await openRouteClient.GetMatrix(request);
        Log.Information("{Response}", response);

        if (response.Durations == null || response.Distances == null)
        {
            Log.Error("Invalid matrix response received.");
            throw new InvalidOperationException("Invalid matrix response");
        }

        Log.Information("Matrix response successfully received.");
        return response;
    }

    public void EnableCacheOverwrite(bool overwriteCache)
    {
        _overwriteCache = overwriteCache;
    }
}