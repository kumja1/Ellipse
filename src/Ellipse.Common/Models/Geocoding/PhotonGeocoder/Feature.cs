using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ellipse.Common.Models.Geocoding.CensusGeocoder.PhotonGeocoder;


public class Feature
{
    public Geometry Geometry { get; set; }
    public Properties Properties { get; set; }
}
