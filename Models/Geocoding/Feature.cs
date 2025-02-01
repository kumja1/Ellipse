using Ellipse.Enums;

namespace Ellipse.Models.Geocoding;


public class Feature
{
    public string Id { get; set; }
    public string Type { get; set; }
    public List<PlaceType> PlaceType { get; set; }
    public double Relevance { get; set; }
    public string Address { get; set; }
    public FeatureProperties Properties { get; set; }
    public string Text { get; set; }
    public string PlaceName { get; set; }
    public string MatchingText { get; set; }
    public string MatchingPlaceName { get; set; }
    public Dictionary<string, string> TranslatedText { get; set; }
    public List<double> Bbox { get; set; }
    public List<double> Center { get; set; }
    public Geometry Geometry { get; set; }
    public List<Context> Context { get; set; }
    public RoutablePoints RoutablePoints { get; set; }
}



public class FeatureProperties
{
    public Accuracy? Accuracy { get; set; }
    public string Wikidata { get; set; }
    public string ShortCode { get; set; }
    public bool? Landmark { get; set; }
    public string Tel { get; set; }
}


