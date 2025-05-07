using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder.PhotonGeocoder;

public class PhotonGeocodeRequest
{
    public string Query { get; set; }
    public int Limit { get; set; } = 10;
    public string Lang { get; set; }
    public string[] Layers { get; set; }
}