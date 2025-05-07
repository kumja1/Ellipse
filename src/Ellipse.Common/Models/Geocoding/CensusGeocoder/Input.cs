using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder;

public class Input
{
    /// <summary>
    /// For onelineaddress searches, the address is nested inside this object.
    /// </summary>
    [JsonPropertyName("address")]
    public AddressContainer Address { get; set; }

    [JsonPropertyName("benchmark")]
    public Benchmark Benchmark { get; set; }
}
