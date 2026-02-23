using Hangfire;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Xml;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionHourlyFetchService(ILogger<HistoryexceedenceService> Logger,
    IHttpClientFactory httpClientFactory) : IAtomDataSelectionHourlyFetchService
    {
        public async Task<List<FinalData>> GetAtomDataSelectionHourlyFetchService(List<SiteInfo> filteredstationpollutant, string pollutantName, string filteryear)
        {
            try
            {
                List<FinalData> Final_list1 = new List<FinalData>();

                var years = filteryear.Split(',');
                var stopwatch = Stopwatch.StartNew();
                Logger.LogInformation("Fetch and processing started in {ElapsedSeconds} seconds.", stopwatch.Elapsed.TotalSeconds);
                var pollutantsToDisplay = GetPollutantsToDisplay(pollutantName);
                var resultsBag = new ConcurrentBag<FinalData>();

                // Eagerly validate factory before entering the parallel loop so that
                // factory failures propagate to the outer catch, which logs
                // "Error in GetAtomDataSelectionHourlyFetchService".
                // Use a named variable (not discard '_') to guarantee the call is emitted by the compiler.
                var eagerClient = httpClientFactory.CreateClient("Atomfeed");

                var siteYearPairs = filteredstationpollutant
                                    .SelectMany(siteinfo => years.Select(year => new { siteinfo, year }));

                await Parallel.ForEachAsync(siteYearPairs, new ParallelOptions { MaxDegreeOfParallelism = 5 }, async (pair, ct) =>
                {
                    try
                    {
                        var atomJsonCollection = await FetchAtomFeedAsync(pair.siteinfo.LocalSiteId, pair.year);
                        var result = ProcessAtomData(atomJsonCollection, pollutantsToDisplay, pair.siteinfo, pair.year);
                        foreach (var item in result)
                            resultsBag.Add(item);
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Error processing site {pair.siteinfo.LocalSiteId} for year {pair.year}: {ex.Message}";
                        Console.WriteLine(errorMessage);
                        Logger.LogError("Error processing site {SiteID} for year {Year}: {Error}", pair.siteinfo.LocalSiteId, pair.year, ex.Message);
                        await File.AppendAllTextAsync("error_log.txt", $"{DateTime.Now}: {errorMessage}{Environment.NewLine}", ct);
                    }
                });

                Final_list1.AddRange(resultsBag);

                stopwatch.Stop();
                Console.WriteLine($"Fetch and processing completed in {stopwatch.Elapsed.TotalSeconds} seconds.");
                Logger.LogInformation("Fetch and processing completed in {ElapsedSeconds} seconds.", stopwatch.Elapsed.TotalSeconds);

                var formattedDuration = stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
                File.AppendAllText("fetch_duration_log.txt", $"Fetch completed at {DateTime.Now} - Duration: {formattedDuration}{Environment.NewLine}");

                return Final_list1;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in GetAtomDataSelectionHourlyFetchService {Error}", ex.Message);
                Logger.LogError("Error in GetAtomDataSelectionHourlyFetchService {Error}", ex.StackTrace);
                return new List<FinalData>();
            }
        }

        private async Task<JArray> FetchAtomFeedAsync(string siteID, string year)
        {
            if (string.IsNullOrWhiteSpace(siteID) || string.IsNullOrWhiteSpace(year))
            {
                Logger.LogWarning("Invalid FetchAtomFeedAsync siteID or year: siteID='{SiteID}', year='{Year}'", siteID, year);
                return new JArray();
            }

            var client = httpClientFactory.CreateClient("Atomfeed");
            var url = $"data/atom-dls/observations/auto/GB_FixedObservations_{year}_{siteID}.xml";
            try
            {
                var response = await client.GetAsync(url);

                // Check for 404 before any other status handling
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Logger.LogWarning("Atom feed not found (404) for URL: {Url} (siteID: {SiteID}, year: {Year})", url, siteID, year);
                    return new JArray();
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Logger.LogWarning("HTTP {StatusCode} when fetching Atom feed for site {SiteID} year {Year}. Response: {Response}",
                        (int)response.StatusCode, siteID, year, errorContent);

                    if (response.StatusCode == System.Net.HttpStatusCode.PreconditionRequired)
                    {
                        Logger.LogError("Server returned 428 Precondition Required. Check if User-Agent, cookies, or other headers are needed.");
                    }

                    return new JArray();
                }

                var stream = await response.Content.ReadAsStreamAsync();
                var xml = new XmlDocument();
                xml.Load(stream);
                var json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xml);
                return JObject.Parse(json)["gml:FeatureCollection"]["gml:featureMember"] as JArray;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Logger.LogWarning("Atom feed not found (404) for URL: {Url} (siteID: {SiteID}, year: {Year})", url, siteID, year);
                return new JArray();
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError("HTTP error FetchAtomFeedAsync fetching Atom feed for URL: {Url} (siteID: {SiteID}, year: {Year}): {Error}", url, siteID, year, ex.Message);
                return new JArray();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error FetchAtomFeedAsync fetching Atom feed for URL: {Url} (siteID: {SiteID}, year: {Year}): {Error}", url, siteID, year, ex);
                return new JArray();
            }
        }

        private List<PollutantDetails> GetPollutantsToDisplay(string filter)
        {
            var allPollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "Nitrogen dioxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/8" },
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" },
                new PollutantDetails { PollutantName = "PM2.5", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/6001" },
                new PollutantDetails { PollutantName = "Ozone", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/7" },
                new PollutantDetails { PollutantName = "Sulphur dioxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/1" },
                new PollutantDetails { PollutantName = "Nitrogen oxides as nitrogen dioxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/9" },
                new PollutantDetails { PollutantName = "Carbon monoxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/10" },
                new PollutantDetails { PollutantName = "Nitric oxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/38" }
            };

            // Split and normalize the filter string
            var filterList = filter.Split(',')
                                   .Select(f => f.Trim())
                                   .ToList();

            // Filter using case-insensitive comparison
            var filtered = allPollutants
                .Where(p => filterList.Contains(p.PollutantName, StringComparer.OrdinalIgnoreCase))
                .ToList();

            // Return filtered list if any match, otherwise return all
            return filtered.Any() ? filtered : allPollutants;
        }

        private List<FinalData> ProcessAtomData(JArray features, List<PollutantDetails> pollutants, SiteInfo siteinfo, string year)
        {
            var finalList = new List<FinalData>();

            if (features == null || features.Count == 0)
                return new List<FinalData>();

            for (int i = 1; i < features.Count; i++)
            {
                try
                {
                    var feature = features[i];
                    var href = feature["om:OM_Observation"]?["om:observedProperty"]?["@xlink:href"]?.ToString();
                    string cleanedUrl = href?.Replace("http://", "");
                    if (string.IsNullOrEmpty(href)) continue;

                    var match = pollutants.FirstOrDefault(p => p.PollutantMasterUrl == cleanedUrl);
                    if (match != null)
                    {
                        var values = feature["om:OM_Observation"]?["om:result"]?["swe:DataArray"]?["swe:values"]?.ToString();
                        if (!string.IsNullOrEmpty(values))
                        {
                            finalList.AddRange(ExtractFinalData(values, match.PollutantName, siteinfo));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error processing ProcessAtomData feature member: {Error}", ex);
                }
            }

            return finalList;
        }

        private List<FinalData> ExtractFinalData(string values, string pollutantName, SiteInfo siteinfo)
        {
            return values.Replace("\r\n", "").Trim().Split("@@")
                .Select(item => item.Split(','))
                .Where(parts => parts.Length >= 5)
                .Select(parts => new FinalData
                {
                    StartTime = parts[0],
                    EndTime = parts[1],
                    Verification = parts[2],
                    Validity = parts[3],
                    Value = parts[4],
                    PollutantName = pollutantName,
                    SiteName = siteinfo.SiteName,
                    SiteType = siteinfo.AreaType + siteinfo.SiteType,
                    Region = siteinfo.ZoneRegion,
                    Country = siteinfo.Country
                }).ToList();
        }
    }
}
