using System.Text.Json;
using Ellipse.Common.Enums.Geocoding;
using Ellipse.Common.Models;
using Ellipse.Common.Models.Geocoding.CensusGeocoder;
using Ellipse.Common.Models.Geocoding.OpenRoute;
using Ellipse.Common.Models.Matrix.OpenRoute;
using Ellipse.Utils;
using Ellipse.Utils.Clients.Mapping;
using Ellipse.Utils.Clients.Mapping.Geocoding;
using Microsoft.Extensions.Caching.Distributed;
using Osrm.HttpApiClient;
using Serilog;

namespace Ellipse.Services;

public class GeocodingService(
    CensusGeocoderClient censusGeocoder,
    OpenRouteClient openRouteClient,
    OsrmHttpApiClient osrmClient,
    IDistributedCache cache
)
{
    public async Task<string> GetAddressCached(
        double longitude,
        double latitude,
        bool overwriteCache = false
    )
    {
        string cacheKey = CacheHelper.CreateCacheKey("address", longitude, latitude);
        Log.Information(
            "[GetAddressCached] Searching cache for coordinates: {Longitude}, {Latitude}",
            longitude,
            latitude
        );

        if (!overwriteCache)
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
                BoundaryCircleRadius = 1,
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

    public async Task<LngLat> GetLatLngCached(string address, bool overwriteCache = false)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            Log.Information(
                "[GetLatLngCached] Provided address is empty or null. Returning LngLat.Zero."
            );
            return LngLat.Zero;
        }

        string cacheKey = CacheHelper.CreateCacheKey("latLng", address.ToLower().Replace(" ", "_"));
        if (!overwriteCache)
        {
            string? cachedLatLng = await cache.GetStringAsync(cacheKey);
            Log.Information("[GetLatLngCached] Searching cache for address: {Address}", address);

            if (!string.IsNullOrEmpty(cachedLatLng))
                return LngLat.Parse(cachedLatLng);
        }

        LngLat censusLngLat = await GetLatLngWithCensus(address);
        LngLat latLng = censusLngLat == LngLat.Zero
            ? await GetLatLngWithOpenRoute(address)
            : censusLngLat;

        if (latLng == LngLat.Zero)
        {
            Log.Information(
                "[GetLatLngCached] No coordinates found for address: {Address}. Returning LngLat.Zero.",
                address
            );
            return LngLat.Zero;
        }

        Log.Information(
            "[GetLatLngCached] Caching coordinates for address: {Address} as: {LngLat}",
            address,
            latLng
        );

        await cache.SetStringAsync(cacheKey, latLng.ToString());
        return latLng;
    }

    private async Task<LngLat> GetLatLngWithCensus(string address)
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
                return LngLat.Zero;

            AddressMatch? firstResult = response.Result.AddressMatches.FirstOrDefault();
            Log.Information("[GetLatLng] First geocoding result: {@FirstResult}", firstResult);
            LngLat resultPoint =
                firstResult != null
                    ? new LngLat(firstResult.Coordinates.X, firstResult.Coordinates.Y)
                    : LngLat.Zero;

            Log.Information("[GetLatLng] Returning coordinates: {ResultPoint}", resultPoint);
            return resultPoint;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GetLatLng] Error getting coordinates for address: {Address}", address);
            return LngLat.Zero;
        }
    }

    private async Task<LngLat> GetLatLngWithOpenRoute(string address)
    {
        try
        {
            Log.Information("[GetLatLng] No coordinates found for address: {Address}", address);
            Log.Information("[GetLatLng] Switching to Mapbox geocoder");

            OpenRouteGeocodingResponse geocodeResponse = await openRouteClient.Geocode(
                new OpenRouteGeocodingRequest { Query = address, Size = 10 }
            );

            IEnumerable<Feature>? features = geocodeResponse
                .Features.OrderByDescending(f => f.Properties.Confidence);

            Log.Information("[GetLatLng] Response Features: {@Features}", features);
            Feature location = features?.FirstOrDefault(f => f.Properties.RegionA == "VA");

            if (location != null)
                return new LngLat(
                    location.Geometry.Coordinates[0],
                    location.Geometry.Coordinates[1]
                );

            Log.Information("[GetLatLng] No coordinates found for address: {Address}", address);
            return LngLat.Zero;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[GetLatLng] Error getting coordinates for address: {Address}", address);
            return LngLat.Zero;
        }
    }

    public async Task<(float?[][] Destinations, float?[][] Durations)> GetMatrixCached(
        LngLat[] sources,
        LngLat[] destinations,
        bool overwriteCache = false
    )
    {
        Log.Information(
            "[GetMatrixCached] Searching cache for {SourceCount} sources with {DestCount} destinations",
            sources.Length,
            destinations.Length
        );

        string cacheKey = CacheHelper.CreateCacheKey("matrix", sources, destinations);
        if (!overwriteCache)
        {
            string? cachedMatrix = await cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedMatrix))
            {
                Log.Information(
                    "[GetMatrixCached] Cache hit for {SourceCount} sources. Key: {CacheKey}",
                    sources.Length,
                    cacheKey
                );

                return JsonSerializer.Deserialize<(float?[][], float?[][])>(CacheHelper.DecompressData(cachedMatrix));
            }
        }

        float?[][] resultDistances = [];
        float?[][] resultDurations = [];

        try
        {
            TableResponse? response = await GetMatrixWithOsrm(sources, destinations)
                .ConfigureAwait(false);

            if (response is not null)
            {
                resultDistances = response.Distances;
                resultDurations = response.Durations;
            }
            else
            {
                throw new Exception("OSRM response is null");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OSRM Matrix failed.");
            // Log.Warning(ex, "OSRM Matrix failed, falling back to OpenRoute");
            // try
            // {
            //     OpenRouteMatrixResponse? openRouteResponse = await GetMatrixWithOpenRoute(
            //             sources,
            //             destinations
            //         )
            //         .ConfigureAwait(false);
            //
            //     if (openRouteResponse is null)
            //     {
            //         Log.Warning("Both OSRM and OpenRouteMatrix responses are null or invalid.");
            //         return ([], []);
            //     }
            //
            //     resultDistances = openRouteResponse.Distances?.Select(row => row.Select(d => (double)d).ToArray() ?? [])
            //         .ToArray() ?? [];
            //     resultDurations = openRouteResponse.Durations?.Select(row => row.Select(d => (double)d).ToArray() ?? [])
            //         .ToArray() ?? [];
            // }
            // catch (Exception ex2)
            // {
            //     Log.Error(ex2, "Both OSRM and OpenRouteMatrix failed.");
            //     return ([], []);
            // }
        }

        await cache.SetStringAsync(
            cacheKey,
            CacheHelper.CompressData(JsonSerializer.Serialize((resultDistances, resultDurations)))
        );

        return (resultDistances, resultDurations);
    }

    private async Task<TableResponse?> GetMatrixWithOsrm(
        LngLat[] sources,
        LngLat[] destinations
    )
    {
        if (destinations.All(dest => dest == LngLat.Zero))
        {
            Log.Information("All destinations are Zero. Returning null.");
            return null;
        }

        TableRequest<JsonFormat> request = OsrmServices
            .Table(
                PredefinedProfiles.Car,
                GeographicalCoordinates.Create(
                    [
                        .. sources.Select(source => Coordinate.Create(source.Lng, source.Lat)),
                        .. destinations.Select(dest => Coordinate.Create(dest.Lng, dest.Lat)),
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
        LngLat[] sources,
        LngLat[] destinations
    )
    {
        Log.Information(
            "Called for {SourceCount} sources with {DestCount} destinations.",
            sources.Length,
            destinations.Length
        );

        if (destinations.All(dest => dest == LngLat.Zero))
            return null;

        OpenRouteMatrixRequest request = new()
        {
            Locations =
            [
                .. sources.Select(source => new[] { source.Lng, source.Lat }),
                .. destinations.Select(dest => new[] { dest.Lng, dest.Lat }),
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

}