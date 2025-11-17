using CsvHelper.Configuration;
using NetTopologySuite.Geometries;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Services.AtomDataSelectionStationBoundryService;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionStationBoundryService(ILogger<HistoryexceedenceService> Logger,
    IHttpClientFactory httpClientFactory) : IAtomDataSelectionStationBoundryService
    {
        public async Task<List<SiteInfo>> GetAtomDataSelectionStationBoundryService(List<SiteInfo> filtered_station_pollutant, string region)
        {
            try
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

                //Envelope englandEnvelope = englandBoundary.EnvelopeInternal;

                // Dictionary to map country names to their envelopes
                var countryEnvelopes = new Dictionary<string, Envelope>
                {
                    { "England", englandBoundary.EnvelopeInternal }
                    //{ "Wales", walesBoundary.EnvelopeInternal },
                    //{ "Scotland", scotlandBoundary.EnvelopeInternal },
                    //{ "Northern Ireland", northernIrelandBoundary.EnvelopeInternal }
                };

                // Example: selectedCountries could come from user input
                //var selectedCountries = new List<string> { "England", "Wales", "Scotland", "Northern Ireland" };
                //var selectedCountries1 = new List<string> { "England" };
                var selectedCountries = region.Split(',')
                                        .Select(r => r.Trim())
                                        .ToList();

                // Combine envelopes
                Envelope combinedEnvelope = null;
                foreach (var country in selectedCountries)
                {
                    if (countryEnvelopes.TryGetValue(country, out var envelope))
                    {
                        //combinedEnvelope = combinedEnvelope == null ? envelope : combinedEnvelope.ExpandToInclude(envelope);
                        if (combinedEnvelope == null)
                        {
                            combinedEnvelope = envelope;
                        }
                        else
                        {
                            combinedEnvelope.ExpandToInclude(envelope);
                        }
                    }
                }

                // Filter points within the combined envelope
                var filteredPoints = filtered_station_pollutant
                    .Where(p =>
                    {
                        if (string.IsNullOrEmpty(p.Latitude) || string.IsNullOrEmpty(p.Longitude))
                            return false;

                        if (double.TryParse(p.Latitude, out double lat) && double.TryParse(p.Longitude, out double lon))
                        {
                            var coordinate = new Coordinate(lon, lat);
                            return combinedEnvelope != null && combinedEnvelope.Contains(coordinate);
                        }
                        return false;
                    })
                    .ToList();

                return filteredPoints;
                ////boundary box check for location
                //var englandBoundary = new Polygon(new LinearRing(new[]
                //{
                //    new Coordinate(-6.0, 49.9),  // Southwest corner
                //    new Coordinate(2.1, 49.9),   // Southeast corner
                //    new Coordinate(2.1, 55.8),   // Northeast corner
                //    new Coordinate(-6.0, 55.8),  // Northwest corner
                //    new Coordinate(-6.0, 49.9)   // Closing the ring

                //}));

                //var walesBoundary = new Polygon(new LinearRing(new[]
                //{
                //    new Coordinate(-5.5, 51.3),  // Southwest
                //    new Coordinate(-2.6, 51.3),  // Southeast
                //    new Coordinate(-2.6, 53.5),  // Northeast
                //    new Coordinate(-5.5, 53.5),  // Northwest
                //    new Coordinate(-5.5, 51.3)   // Closing the ring
                //}));


                //var scotlandBoundary = new Polygon(new LinearRing(new[]
                //{
                //    new Coordinate(-7.5, 54.5),  // Southwest
                //    new Coordinate(-0.8, 54.5),  // Southeast
                //    new Coordinate(-0.8, 60.9),  // Northeast
                //    new Coordinate(-7.5, 60.9),  // Northwest
                //    new Coordinate(-7.5, 54.5)   // Closing the ring
                //}));


                //var northernIrelandBoundary = new Polygon(new LinearRing(new[]
                //{
                //    new Coordinate(-8.2, 54.0),  // Southwest
                //    new Coordinate(-5.3, 54.0),  // Southeast
                //    new Coordinate(-5.3, 55.4),  // Northeast
                //    new Coordinate(-8.2, 55.4),  // Northwest
                //    new Coordinate(-8.2, 54.0)   // Closing the ring
                //}));

                //Envelope englandEnvelope = englandBoundary.EnvelopeInternal;

                //var filteredPoints = filtered_station_pollutant
                //                    //.Where(p => englandEnvelope.Contains(p.polygon))
                //                    //.ToList();
                //                    .Where(p =>
                //                    {
                //                        if (string.IsNullOrEmpty(p.Latitude) || string.IsNullOrEmpty(p.Longitude))
                //                            return false;

                //                        if (double.TryParse(p.Latitude, out double lat) && double.TryParse(p.Longitude, out double lon))
                //                        {
                //                            var coordinate = new Coordinate(lon, lat); // Note: Coordinate(x, y) => (longitude, latitude)
                //                            return englandEnvelope.Contains(coordinate);
                //                        }
                //                        return false;
                //                    });
                //return filteredPoints.ToList();
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
