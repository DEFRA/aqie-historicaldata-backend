using CsvHelper.Configuration;
using NetTopologySuite.Geometries;
using System.Collections.Generic;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Services.AtomDataSelectionStationBoundryService;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionStationBoundryService(ILogger<HistoryexceedenceService> Logger,
    IAtomDataSelectionLocalAuthoritiesService AtomDataSelectionLocalAuthoritiesService,
    IHttpClientFactory httpClientFactory) : IAtomDataSelectionStationBoundryService
    {
        public async Task<List<SiteInfo>> GetAtomDataSelectionStationBoundryService(List<SiteInfo> filtered_station_pollutant, string region, string regiontype)
        {
            try
            {
                if(regiontype == "Country")
                {

                    //boundary box check for location
                    var englandBoundary = new Polygon(new LinearRing(new[]
                    {
                    new Coordinate(-6.0, 49.9),  // Southwest corner
                    new Coordinate(2.1, 49.9),   // Southeast corner
                    new Coordinate(2.1, 55.8),   // Northeast corner
                    new Coordinate(-6.0, 55.8),  // Northwest corner
                    new Coordinate(-6.0, 49.9)   // Closing the ring
                }));

                    var walesBoundary = new Polygon(new LinearRing(new[]
                    {
                    new Coordinate(-5.5, 51.3),  // Southwest
                    new Coordinate(-2.6, 51.3),  // Southeast
                    new Coordinate(-2.6, 53.5),  // Northeast
                    new Coordinate(-5.5, 53.5),  // Northwest
                    new Coordinate(-5.5, 51.3)   // Closing the ring
                }));

                    var scotlandBoundary = new Polygon(new LinearRing(new[]
                    {
                    new Coordinate(-7.5, 54.5),  // Southwest
                    new Coordinate(-0.8, 54.5),  // Southeast
                    new Coordinate(-0.8, 60.9),  // Northeast
                    new Coordinate(-7.5, 60.9),  // Northwest
                    new Coordinate(-7.5, 54.5)   // Closing the ring
                }));

                    var northernIrelandBoundary = new Polygon(new LinearRing(new[]
                    {
                    new Coordinate(-8.2, 54.0),  // Southwest
                    new Coordinate(-5.3, 54.0),  // Southeast
                    new Coordinate(-5.3, 55.4),  // Northeast
                    new Coordinate(-8.2, 55.4),  // Northwest
                    new Coordinate(-8.2, 54.0)   // Closing the ring
                }));

                    // Dictionary to map country names to their envelopes and boundaries
                    var countryBoundaries = new Dictionary<string, (Envelope Envelope, Polygon Polygon)>
                {
                    { "England", (englandBoundary.EnvelopeInternal, englandBoundary) },
                    { "Wales", (walesBoundary.EnvelopeInternal, walesBoundary) },
                    { "Scotland", (scotlandBoundary.EnvelopeInternal, scotlandBoundary) },
                    { "Northern Ireland", (northernIrelandBoundary.EnvelopeInternal, northernIrelandBoundary) }
                };

                    var selectedCountries = region.Split(',')
                                            .Select(r => r.Trim())
                                            .ToList();

                    // Combine envelopes for quick filtering
                    Envelope combinedEnvelope = null;
                    foreach (var country in selectedCountries)
                    {
                        if (countryBoundaries.TryGetValue(country, out var tuple))
                        {
                            if (combinedEnvelope == null)
                            {
                                combinedEnvelope = new Envelope(tuple.Envelope);
                            }
                            else
                            {
                                combinedEnvelope.ExpandToInclude(tuple.Envelope);
                            }
                        }
                    }

                    // Filter points within the combined envelope and assign country
                    var filteredPoints = new List<SiteInfo>();
                    foreach (var site in filtered_station_pollutant)
                    {
                        if (string.IsNullOrEmpty(site.Latitude) || string.IsNullOrEmpty(site.Longitude))
                            continue;

                        if (double.TryParse(site.Latitude, out double lat) && double.TryParse(site.Longitude, out double lon))
                        {
                            var coordinate = new Coordinate(lon, lat);
                            if (combinedEnvelope != null && combinedEnvelope.Contains(coordinate))
                            {
                                // Determine which country polygon contains the point
                                string matchedCountry = null;
                                foreach (var country in selectedCountries.OrderBy(c =>
                                        c == "Northern Ireland" ? 0 :
                                        c == "Wales" ? 1 :
                                        c == "Scotland" ? 2 :
                                        c == "England" ? 3 : 4))
                                {
                                    if (countryBoundaries.TryGetValue(country, out var tuple))
                                    {
                                        if (tuple.Polygon.Contains(new Point(coordinate)))
                                        {
                                            matchedCountry = country;
                                            break;
                                        }
                                    }
                                }
                                if (matchedCountry != null)
                                {
                                    site.Country = matchedCountry;
                                    filteredPoints.Add(site);
                                }
                            }
                        }
                    }

                    return filteredPoints;
                }
                else if (regiontype == "LocalAuthority")
                {
                    //call local authority service
                    var localAuthoritiesresult = await AtomDataSelectionLocalAuthoritiesService.GetAtomDataSelectionLocalAuthoritiesService(region);
                    if (localAuthoritiesresult != null && localAuthoritiesresult.Any())
                    {
                        var geometryFactory = new NetTopologySuite.Geometries.GeometryFactory();

                        // Create points for local authorities
                        var localAuthorityPoints = localAuthoritiesresult
                            .Where(la => la.Latitude != 0 && la.Longitude != 0)
                            .Select(la => new
                            {
                                Point = geometryFactory.CreatePoint(new Coordinate(la.Longitude, la.Latitude)),
                                Data = la
                            })
                            .ToList();

                        Logger.LogInformation($"Processing {localAuthorityPoints.Count} local authority points for region: {region}");

                        // Filter stations by proximity to local authorities (e.g., within 30km)
                        var filteredPoints = new List<SiteInfo>();
                        const double maxDistanceMeters = 30000; // 30km radius

                        foreach (var site in filtered_station_pollutant)
                        {
                            if (string.IsNullOrEmpty(site.Latitude) || string.IsNullOrEmpty(site.Longitude))
                                continue;

                            if (double.TryParse(site.Latitude, out double lat) && double.TryParse(site.Longitude, out double lon))
                            {
                                var sitePoint = geometryFactory.CreatePoint(new Coordinate(lon, lat));

                                // Check if station is within distance threshold of any local authority point
                                bool isNearLocalAuthority = localAuthorityPoints.Any(la =>
                                    sitePoint.Distance(la.Point) < maxDistanceMeters / 111320.0); // Approximate degrees conversion

                                if (isNearLocalAuthority)
                                {
                                    filteredPoints.Add(site);
                                }
                            }
                        }

                        Logger.LogInformation($"Filtered {filteredPoints.Count} stations near local authorities for region: {region}");
                        return filteredPoints;
                    }

                    return new List<SiteInfo>();
                }
                return new List<SiteInfo>();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in Atom GetAtomDataSelectionStationBoundryService {Error}", ex.Message);
                Logger.LogError("Error in Atom GetAtomDataSelectionStationBoundryService {Error}", ex.StackTrace);
                List<SiteInfo> Final_list = new List<SiteInfo>();
                return Final_list;
            }
        }
    }
}
