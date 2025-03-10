using System.Text;
using System.Net;
using Ellipse.Common.Models;
using Ellipse.Extensions;
using System.Text.Json;
using Ellipse.Common.Enums.Directions;
using Ellipse.Common.Models.Directions;
using Ellipse.Common.Models.Matrix;


namespace Ellipse.Services;

public class MapboxClient(HttpClient httpClient)
{
    private const string MatrixApiUrl = "https://api.mapbox.com/directions-matrix/v1/mapbox/";

    private const string DirectionsApiUrl = "https://api.mapbox.com/directions/v5/mapbox/";

    private readonly HttpClient HttpService = httpClient;

    private readonly SemaphoreSlim _rateLimiter = new(10, 60);

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly int _delay = 1000;

    public async Task<MatrixResponse?> GetMatrixAsync(MatrixRequest request)
    {
        await _rateLimiter.WaitAsync(_delay);
        try
        {
            if (request.Sources.Count == 0 || request.Destinations.Count == 0)
            {
                throw new ArgumentException("Sources and Destinations must have at least one coordinate.");
            }

            var requestUrl = GetRequestUrl(request);
            var response = await HttpService.GetAsync(requestUrl);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var matrixResponse = JsonSerializer.Deserialize<MatrixResponse>(await response.Content.ReadAsStringAsync(), _jsonSerializerOptions);
                ArgumentNullException.ThrowIfNull(matrixResponse);
                return matrixResponse;
            }
            else
            {
                throw new Exception($"Error: {response.StatusCode}, {response.ReasonPhrase}");
            }
        }
        finally
        {
            _rateLimiter.Release();
        }
    }




    public async Task<DirectionsResponse> GetDirectionsAsync(DirectionsRequest request)
    {
        await _rateLimiter.WaitAsync(_delay);
        try
        {
            // Console.WriteLine($"Waypoints:{string.Join(";", request.Waypoints.Select(d => $"{d.Lon},{d.Lat}"))}");
            var requestUrl = GetRequestUrl(request);
            var response = await HttpService.GetAsync(requestUrl);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var directionsResponse = JsonSerializer.Deserialize<DirectionsResponse>(await response.Content.ReadAsStringAsync(), _jsonSerializerOptions);
                ArgumentNullException.ThrowIfNull(directionsResponse);
                return directionsResponse;
            }
            else
            {
                throw new Exception($"Error: {response.StatusCode}, {response.ReasonPhrase}");
            }
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private string GetRequestUrl(object request)
    {

        return request switch
        {
            DirectionsRequest directionsRequest => GetDirectionsRequestUrl(directionsRequest),
            MatrixRequest matrixRequest => GetMatrixRequestUrl(matrixRequest),
            _ => throw new NotImplementedException()
        };
    }

    private string GetDirectionsRequestUrl(DirectionsRequest request)
    {
        var stringBuilder = new StringBuilder($"{DirectionsApiUrl}{request.Profile.ToStringValue().ToLower()}/{string.Join(";", request.Waypoints.Select(d => $"{d.Lon},{d.Lat}"))}");
        stringBuilder.Append($"?access_token={request.AccessToken}");

        if (request.Annotations != null)
            stringBuilder.Append($"&annotations={string.Join(",", request.Annotations.Select(a => a.ToString().ToLower()))}");
        else if (request.BannerInstructions)
            stringBuilder.Append("&banner_instructions=true");
        else if (request.ContinueStraight.HasValue)
            stringBuilder.Append($"&continue_straight={request.ContinueStraight.Value.ToString().ToLower()}");
        else if (!string.IsNullOrEmpty(request.Exclude))
            stringBuilder.Append($"&exclude={request.Exclude}");
        else if (request.Geometries != GeometryType.Polyline)
            stringBuilder.Append($"&geometries={request.Geometries.ToString().ToLower()}");
        else if (!string.IsNullOrEmpty(request.Language))
            stringBuilder.Append($"&language={request.Language}");
        else if (request.Overview != OverviewType.Simplified)
            stringBuilder.Append($"&overview={request.Overview.ToString().ToLower()}");
        else if (request.RoundaboutExits)
            stringBuilder.Append("&roundabout_exits=true");
        else if (request.Steps)
            stringBuilder.Append("&steps=true");
        else if (request.VoiceInstructions)
            stringBuilder.Append("&voice_instructions=true");
        else if (request.VoiceUnits != VoiceUnitsType.Imperial)
            stringBuilder.Append($"&voice_units={request.VoiceUnits.ToString().ToLower()}");
        else if (request.Engine != EngineType.ElectricNoRecharge)
            stringBuilder.Append($"&engine={request.Engine.ToString().ToLower()}");
        else if (request.EvInitialCharge.HasValue)
            stringBuilder.Append($"&ev_initial_charge={request.EvInitialCharge.Value}");
        else if (request.EvMaxCharge.HasValue)
            stringBuilder.Append($"&ev_max_charge={request.EvMaxCharge.Value}");
        else if (request.EvConnectorTypes != null)
            stringBuilder.Append($"&ev_connector_types={string.Join(";", request.EvConnectorTypes.Select(e => e.ToString().ToLower()))}");
        else if (!string.IsNullOrEmpty(request.EnergyConsumptionCurve))
            stringBuilder.Append($"&energy_consumption_curve={request.EnergyConsumptionCurve}");
        else if (!string.IsNullOrEmpty(request.EvChargingCurve))
            stringBuilder.Append($"&ev_charging_curve={request.EvChargingCurve}");
        else if (!string.IsNullOrEmpty(request.EvUnconditionedChargingCurve))
            stringBuilder.Append($"&ev_unconditioned_charging_curve={request.EvUnconditionedChargingCurve}");
        else if (request.EvPreConditioningTime.HasValue)
            stringBuilder.Append($"&ev_pre_conditioning_time={request.EvPreConditioningTime.Value}");
        else if (request.EvMaxAcChargingPower.HasValue)
            stringBuilder.Append($"&ev_max_ac_charging_power={request.EvMaxAcChargingPower.Value}");
        else if (request.EvMinChargeAtDestination.HasValue)
            stringBuilder.Append($"&ev_min_charge_at_destination={request.EvMinChargeAtDestination.Value}");
        else if (request.EvMinChargeAtChargingStation.HasValue)
            stringBuilder.Append($"&ev_min_charge_at_charging_station={request.EvMinChargeAtChargingStation.Value}");
        else if (request.AuxiliaryConsumption.HasValue)
            stringBuilder.Append($"&auxiliary_consumption={request.AuxiliaryConsumption.Value}");
        else if (request.MaxHeight.HasValue)
            stringBuilder.Append($"&max_height={request.MaxHeight.Value}");
        else if (request.MaxWidth.HasValue)
            stringBuilder.Append($"&max_width={request.MaxWidth.Value}");
        else if (request.MaxWeight.HasValue)
            stringBuilder.Append($"&max_weight={request.MaxWeight.Value}");
        else if (!string.IsNullOrEmpty(request.DepartAt))
            stringBuilder.Append($"&depart_at={request.DepartAt}");
        else if (!string.IsNullOrEmpty(request.ArriveBy))
            stringBuilder.Append($"&arrive_by={request.ArriveBy}");
        else if (request.Notifications != NotificationsType.All)
            stringBuilder.Append($"&notifications={request.Notifications.ToString().ToLower()}");
        return stringBuilder.ToString();
    }

    private string GetMatrixRequestUrl(MatrixRequest request)
    {
        var stringBuilder = new StringBuilder($"{MatrixApiUrl}{request.Profile.ToStringValue().ToLower()}/{string.Join(";", request.Sources.Select(s => $"{s.Lon},{s.Lat}"))};{string.Join(";", request.Destinations.Select(d => $"{d.Lon},{d.Lat}"))}");
        stringBuilder.Append($"?access_token={request.AccessToken}");
        stringBuilder.Append($"&sources={string.Join(";", Enumerable.Range(0, request.Sources.Count))}&destinations={string.Join(";", Enumerable.Range(request.Sources.Count, request.Destinations.Count))}");

        if (request.Annotations != null)
            stringBuilder.Append($"&annotations={string.Join(",", request.Annotations.Select(a => a.ToStringValue().ToLower()))}");
        else if (request.Approaches != null)
            stringBuilder.Append($"&approaches={string.Join(";", request.Approaches.Select(a => a.ToString().ToLower()))}");
        else if (request.Bearings != null)
            stringBuilder.Append($"&bearings={string.Join(";", request.Bearings.Select(b => $"{b.Angle},{b.Deviation}"))}");

        return stringBuilder.ToString();

    }
}
