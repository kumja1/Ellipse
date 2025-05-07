using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder.PhotonGeocoder;

public class PhotonReverseGeocodeRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}