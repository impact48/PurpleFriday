﻿using Geocoding;
using Geocoding.Google;
using Geocoding.Microsoft;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using PurpleFridayTweetListener.Logger;

namespace PurpleFridayTweetListener.LocationFinder
{
    public class LocationFinder : ILocationFinder
    {
        private IGeocoder _geoCoder;
        private readonly LocationFinderConfiguration _configuration;
        private Dictionary<string, string> _locationOverrides;

        public LocationFinder(LocationFinderConfiguration configuration, IGeocoder geocoder)
        {
            _configuration = configuration;
            _geoCoder = geocoder;
            _locationOverrides = LoadLocationOverridesFromFile();
        }

        public Dictionary<string, string> LoadLocationOverridesFromFile()
        {
            var LOCATION_OVERRIDE_FILE="/app/overrides/overrides.txt";
            Logging.Information($"Loading config from file {LOCATION_OVERRIDE_FILE}.");
            var locOverrides = new Dictionary<string, string>();

            if  (!File.Exists(LOCATION_OVERRIDE_FILE))
            {
                Logging.Warning($"Location Override File {LOCATION_OVERRIDE_FILE} does not exist");
                return locOverrides; // Return empty dictionary
            }

            var lines = File.ReadAllLines(LOCATION_OVERRIDE_FILE);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains('|'))
                {
                    var splitArray = lines[i].Split('|');
                    if  (!locOverrides.ContainsKey(splitArray[0])) // Validate the key is unique already.
                    {
                        locOverrides.Add(splitArray[0],splitArray[1]);
                        Logging.Debug($"Loaded {splitArray[0]} = {splitArray[1]}");
                    }
                }
            }            
            return locOverrides;
        }

        public string InvokeLocationOverrideOrReturnOriginal(string location)
        {
            if  (_locationOverrides.Count() > 0)
                if  (_locationOverrides.ContainsKey(location))
                {
                    Logging.Information($"Location found in override. '{location}' replaced with '{_locationOverrides[location]}'");
                    return _locationOverrides[location];
                }
            
            return location;
            
        }

        public async Task<LocationFinderResult> GetLocationFromStringAsync(string locationText)
        {
            try
            {
                locationText = InvokeLocationOverrideOrReturnOriginal(locationText);
                if (!locationText.EndsWith(",scotland", StringComparison.InvariantCultureIgnoreCase))
                {
                    locationText = locationText + ",scotland"; //hack to restrict returned locations to scotland
                }
                IEnumerable<BingAddress> addresses = await _geoCoder.GeocodeAsync(locationText) as IEnumerable<BingAddress>;

                if(addresses == null || !addresses.Any())
                {
                    return null;
                }

                var matchedLocation = addresses.Where(x => x.Type == EntityType.PopulatedPlace 
                                            && x.Confidence <= ConfidenceLevel.Medium
                                            && x.CountryRegion == "United Kingdom"
                                            && x.AdminDistrict == "Scotland"
                                            ).FirstOrDefault();
                if(matchedLocation == null)
                {
                    return null;
                }

                Logging.Debug("Formatted: " + addresses.First().FormattedAddress);
                Logging.Debug("Coordinates: " + addresses.First().Coordinates.Latitude + ", " + addresses.First().Coordinates.Longitude);
                return new LocationFinderResult
                {
                    AdminDistrict2 = GetAreaName(matchedLocation.AdminDistrict2),
                    Coordinates = new Coordinates
                    {
                        Latitude = matchedLocation.Coordinates.Latitude,
                        Longitude = matchedLocation.Coordinates.Longitude
                    },
                    Confidence = matchedLocation.Confidence == ConfidenceLevel.High ? LocationConfidence.HIGH : LocationConfidence.MEDIUM
                };
            }
            catch (Exception e)
            {
                Logging.Error($"Exception caught in GetLocationFromStringAsync method, message: {e.Message}");
                Logging.Debug(JsonConvert.SerializeObject(e, Formatting.Indented,
                    new JsonSerializerSettings()
                    {
                        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
                    }));
                return null;
            }

        }

        public string GetAreaName(string bingName)
        {
            //we want to convert the area name into what is expected at map endpoint
            Dictionary<string, string[]> mapping = new Dictionary<string, string[]>();
            mapping["Na h-Eileanan Siar"] = new string[] { "Western Isles"};
            mapping["West Dunbartonshire"] = new string[]{};
            mapping["West Lothian"] = new string[] { };
            mapping["Clackmannanshire"] = new string[] { };
            mapping["Dumfries and Galloway"] = new string[] { };
            mapping["East Ayrshire"] = new string[] { };
            mapping["East Lothian"] = new string[] { };
            mapping["East Renfrewshire"] = new string[] { };
            mapping["Falkirk"] = new string[] { };
            mapping["Highland"] = new string[] { };
            mapping["Inverclyde"] = new string[] { };
            mapping["Midlothian"] = new string[] { };
            mapping["Moray"] = new string[] { };
            mapping["North Ayrshire"] = new string[] { };
            mapping["Orkney Islands"] = new string[] { };
            mapping["Scottish Borders"] = new string[] { };
            mapping["Shetland Islands"] = new string[] { };
            mapping["South Ayrshire"] = new string[] { };
            mapping["South Lanarkshire"] = new string[] { };
            mapping["Stirling"] = new string[] { };
            mapping["Aberdeen City"] = new string[] { };
            mapping["Aberdeenshire"] = new string[] { };
            mapping["Argyll and Bute"] = new string[] { };
            mapping["City of Edinburgh"] = new string[] { "Edinburgh City" };
            mapping["Renfrewshire"] = new string[] { };
            mapping["Angus"] = new string[] { };
            mapping["Dundee City"] = new string[] { "City of Dundee" };
            mapping["East Dunbartonshire"] = new string[] { };
            mapping["Fife"] = new string[] { };
            mapping["Perth and Kinross"] = new string[] { };
            mapping["Glasgow City"] = new string[] { "City of Glasgow" };
            mapping["North Lanarkshire"] = new string[] { };

            string match = mapping.FirstOrDefault(m => m.Value.Contains(bingName)).Key;
            return (String.IsNullOrEmpty(match)) ? bingName : match;
        }

        public Coordinates GetCentralGeoCoordinate(IList<Coordinates> geoCoordinates)
        {
            if (geoCoordinates.Count == 1)
            {
                return geoCoordinates.Single();
            }

            double x = 0;
            double y = 0;
            double z = 0;

            foreach (var geoCoordinate in geoCoordinates)
            {
                var latitude = geoCoordinate.Latitude * Math.PI / 180;
                var longitude = geoCoordinate.Longitude * Math.PI / 180;

                x += Math.Cos(latitude) * Math.Cos(longitude);
                y += Math.Cos(latitude) * Math.Sin(longitude);
                z += Math.Sin(latitude);
            }

            var total = geoCoordinates.Count;

            x = x / total;
            y = y / total;
            z = z / total;

            var centralLongitude = Math.Atan2(y, x);
            var centralSquareRoot = Math.Sqrt(x * x + y * y);
            var centralLatitude = Math.Atan2(z, centralSquareRoot);

            return new Coordinates
            {
                Latitude = centralLatitude * 180 / Math.PI,
                Longitude = centralLongitude * 180 / Math.PI
            };
        }

    }
}
