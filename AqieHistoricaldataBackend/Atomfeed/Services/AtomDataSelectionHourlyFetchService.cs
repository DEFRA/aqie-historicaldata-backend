using Hangfire;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Xml;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionHourlyFetchService(ILogger<HistoryexceedenceService> Logger,
    IHttpClientFactory httpClientFactory) : IAtomDataSelectionHourlyFetchService
    {
        public async Task<List<FinalData>> GetAtomDataSelectionHourlyFetchService(List<SiteInfo> filteredstationpollutant, string pollutantName, string filteryear, QueryStringData data)
        {
            try
            {
                
                List<FinalData> Final_list1 = new List<FinalData>();
                var years = filteryear.Split(',');
                var stopwatch = Stopwatch.StartNew();
                Logger.LogInformation("Fetch and processing started in {ElapsedSeconds} seconds.", stopwatch.Elapsed.TotalSeconds);
                var pollutantsToDisplay = GetPollutantsToDisplay(pollutantName);
                var resultsBag = new ConcurrentBag<FinalData>();

                var siteYearPairs = filteredstationpollutant
                                    .SelectMany(siteinfo => years.Select(year => new { siteinfo, year }));
                await Parallel.ForEachAsync(siteYearPairs, new ParallelOptions { MaxDegreeOfParallelism = 3 }, async (pair, ct) =>
                {
                    try
                    {
                        var atomJsonCollection = await FetchAtomFeedAsync(pair.siteinfo.LocalSiteId, pair.year, data.dataSource);
                        var result = ProcessAtomData(atomJsonCollection, pollutantsToDisplay, pair.siteinfo);
                        foreach (var item in result)
                            resultsBag.Add(item);
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = $"Error processing site {pair.siteinfo.LocalSiteId} for year {pair.year}: {ex.Message}";
                        Console.WriteLine(errorMessage);
                        Logger.LogError(ex,"Error processing site {SiteID} for year {Year}: {Error}", pair.siteinfo.LocalSiteId, pair.year, ex.Message);
                        await File.AppendAllTextAsync("error_log.txt", $"{DateTime.Now}: {errorMessage}{Environment.NewLine}", ct);
                    }
                });
                Final_list1.AddRange(resultsBag);
                stopwatch.Stop();
                Console.WriteLine($"Fetch and processing completed in {stopwatch.Elapsed.TotalSeconds} seconds.");
                Logger.LogInformation("Fetch and processing completed in {ElapsedSeconds} seconds.", stopwatch.Elapsed.TotalSeconds);
                var formattedDuration = stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
                await File.AppendAllTextAsync("fetch_duration_log.txt", $"Fetch completed at {DateTime.Now} - Duration: {formattedDuration}{Environment.NewLine}");

                return Final_list1;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in GetAtomDataSelectionHourlyFetchService");
                return new List<FinalData>();
            }
        }

        private async Task<JArray> FetchAtomFeedAsync(string? siteID, string year,string? dataSource)
        {
            if (string.IsNullOrWhiteSpace(siteID) || string.IsNullOrWhiteSpace(year))
            {
                Logger.LogWarning("Invalid FetchAtomFeedAsync siteID or year: siteID='{SiteID}', year='{Year}'", siteID, year);
                return new JArray();
            }
            var client = httpClientFactory.CreateClient("Atomfeed");
            string url;
            if (dataSource == "AURN") 
            { 
                url = $"data/atom-dls/observations/auto/GB_FixedObservations_{year}_{siteID}.xml";
            }
            else
            {
                url = $"data/atom-dls/observations/non-auto/GB_FixedObservations_{year}_{siteID}.xml";
            }
            try
            {
                var response = await client.GetAsync(url);
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
                var featureCollection = JObject.Parse(json)["gml:FeatureCollection"];
                return featureCollection?["gml:featureMember"] as JArray ?? new JArray();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Logger.LogWarning(ex,"Atom feed not found (404) for URL: {Url} (siteID: {SiteID}, year: {Year})", url, siteID, year);
                return new JArray();
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex,"HTTP error FetchAtomFeedAsync fetching Atom feed for URL: {Url} (siteID: {SiteID}, year: {Year}): {Error}", url, siteID, year, ex.Message);
                return new JArray();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,"Error FetchAtomFeedAsync fetching Atom feed for URL: {Url} (siteID: {SiteID}, year: {Year}): {Error}", url, siteID, year, ex.Message);
                return new JArray();
            }
        }

        private static List<PollutantDetails> GetPollutantsToDisplay(string? filter)
        {
            var allPollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "Nitrogen dioxide", PollutantMasterUrl = "8" },
                new PollutantDetails { PollutantName = "Particulate matter", PollutantMasterUrl = "5" },
                new PollutantDetails { PollutantName = "Fine particulate matter", PollutantMasterUrl = "6001" },
                new PollutantDetails { PollutantName = "Ozone", PollutantMasterUrl = "7" },
                new PollutantDetails { PollutantName = "Sulphur dioxide", PollutantMasterUrl = "1" },
                new PollutantDetails { PollutantName = "Nitrogen oxides as nitrogen dioxide", PollutantMasterUrl = "9" },
                new PollutantDetails { PollutantName = "Carbon monoxide", PollutantMasterUrl = "10" },
                new PollutantDetails { PollutantName = "Nitric oxide", PollutantMasterUrl = "38" },
                new PollutantDetails { PollutantName = "Particulate calcium", PollutantMasterUrl = "629" },
                new PollutantDetails { PollutantName = "Particulate chloride", PollutantMasterUrl = "631" },
                new PollutantDetails { PollutantName = "Particulate magnesium", PollutantMasterUrl = "659" },
                new PollutantDetails { PollutantName = "Particulate sodium", PollutantMasterUrl = "668" },
                new PollutantDetails { PollutantName = "Particulate nitrite", PollutantMasterUrl = "999004" },
                new PollutantDetails { PollutantName = "Particulate nitrate", PollutantMasterUrl = "46" },
                new PollutantDetails { PollutantName = "Particulate sulphate", PollutantMasterUrl = "47" },
                new PollutantDetails { PollutantName = "Gaseous hydrochloric acid", PollutantMasterUrl = "39" },
                new PollutantDetails { PollutantName = "Gaseous nitric acid", PollutantMasterUrl = "50" },
                new PollutantDetails { PollutantName = "Gaseous nitrous acid", PollutantMasterUrl = "999005" },
                //new PollutantDetails { PollutantName = "Ammonium in PM2.5", PollutantMasterUrl = "1045" },
                //new PollutantDetails { PollutantName = "Calcium in PM2.5", PollutantMasterUrl = "1629" },
                //new PollutantDetails { PollutantName = "Chloride in PM2.5", PollutantMasterUrl = "1631" },
                //new PollutantDetails { PollutantName = "Gaseous ammonia", PollutantMasterUrl = "35" },
                //new PollutantDetails { PollutantName = "Magnesium in PM2.5", PollutantMasterUrl = "1659" },
                //new PollutantDetails { PollutantName = "Nitrate in PM2.5", PollutantMasterUrl = "1046" },
                //new PollutantDetails { PollutantName = "Potassium in PM2.5", PollutantMasterUrl = "1657" },
                //new PollutantDetails { PollutantName = "Sodium in PM2.5", PollutantMasterUrl = "1668" },
                //new PollutantDetails { PollutantName = "Sulphate in PM2.5", PollutantMasterUrl = "1047" }
                new PollutantDetails { PollutantName = "Calcium in precipitation", PollutantMasterUrl = "630" },
                new PollutantDetails { PollutantName = "Chloride in precipitation", PollutantMasterUrl = "632" },
                new PollutantDetails { PollutantName = "Potassium in precipitation", PollutantMasterUrl = "658" },
                new PollutantDetails { PollutantName = "Magnesium in precipitation", PollutantMasterUrl = "660" },
                new PollutantDetails { PollutantName = "Sodium in precipitation", PollutantMasterUrl = "669" },
                new PollutantDetails { PollutantName = "Phosphate as P in precipitation", PollutantMasterUrl = "2770" },
                new PollutantDetails { PollutantName = "Nitrate as N in precipitation", PollutantMasterUrl = "666" },
                new PollutantDetails { PollutantName = "Ammonium as N in precipitation", PollutantMasterUrl = "664" },
                new PollutantDetails { PollutantName = "Sulphate as S in precipitation", PollutantMasterUrl = "719" },
                new PollutantDetails { PollutantName = "Non-marine sulphate as S in precipitation", PollutantMasterUrl = "720" },
                new PollutantDetails { PollutantName = "Acidity in precipitation", PollutantMasterUrl = "648" },
                new PollutantDetails { PollutantName = "Conductivity", PollutantMasterUrl = "412" },
                new PollutantDetails { PollutantName = "pH in precipitation", PollutantMasterUrl = "753" },
                new PollutantDetails { PollutantName = "Rainfall", PollutantMasterUrl = "2076" },
                new PollutantDetails { PollutantName = "Fluoride", PollutantMasterUrl = "999006" },
                new PollutantDetails { PollutantName = "1,3-butadiene", PollutantMasterUrl = "24" },
                new PollutantDetails { PollutantName = "Benzene", PollutantMasterUrl = "20" },
                new PollutantDetails { PollutantName = "Gaseous ammonia (passive)", PollutantMasterUrl = "35" },
                new PollutantDetails { PollutantName = "Gaseous ammonia (active)", PollutantMasterUrl = "35" },
                new PollutantDetails { PollutantName = "Gaseous ammonia (diffusion tube)", PollutantMasterUrl = "35" },
                new PollutantDetails { PollutantName = "Particulate ammonium", PollutantMasterUrl = "45" },
                new PollutantDetails { PollutantName = "Biphenyl", PollutantMasterUrl = "7390" },
                new PollutantDetails { PollutantName = "Cholanthrene", PollutantMasterUrl = "5526" },
                new PollutantDetails { PollutantName = "Chrysene", PollutantMasterUrl = "5406" },
                new PollutantDetails { PollutantName = "Coronene", PollutantMasterUrl = "5415" },
                new PollutantDetails { PollutantName = "Cyclopenta(c,d)pyrene", PollutantMasterUrl = "5417" },
                new PollutantDetails { PollutantName = "Dibenzo(al)pyrene", PollutantMasterUrl = "5528" },
                new PollutantDetails { PollutantName = "Dibenzo(ae)pyrene", PollutantMasterUrl = "5636" },
                new PollutantDetails { PollutantName = "Dibenzo(ai)pyrene", PollutantMasterUrl = "5639" },
                new PollutantDetails { PollutantName = "Dibenzo(ah)pyrene", PollutantMasterUrl = "5637" },
                new PollutantDetails { PollutantName = "Fluoranthene", PollutantMasterUrl = "5643" },
                new PollutantDetails { PollutantName = "Fluorene", PollutantMasterUrl = "7435" },
                new PollutantDetails { PollutantName = "Perylene", PollutantMasterUrl = "5488" },
                new PollutantDetails { PollutantName = "Phenanthrene", PollutantMasterUrl = "5712" },
                new PollutantDetails { PollutantName = "Pyrene", PollutantMasterUrl = "5715" },
                new PollutantDetails { PollutantName = "Retene", PollutantMasterUrl = "999011" },
                new PollutantDetails { PollutantName = "Benzo(a)pyrene", PollutantMasterUrl = "5029" },
                new PollutantDetails { PollutantName = "Benzo(a)anthracene", PollutantMasterUrl = "5610" },
                new PollutantDetails { PollutantName = "Benzo(b)fluoranthene", PollutantMasterUrl = "5617" },
                new PollutantDetails { PollutantName = "Benzo(j)fluoranthene", PollutantMasterUrl = "5759" },
                new PollutantDetails { PollutantName = "Benzo(b+j)fluoranthene", PollutantMasterUrl = "7480" },
                new PollutantDetails { PollutantName = "Benzo(k)fluoranthene", PollutantMasterUrl = "5626" },
                new PollutantDetails { PollutantName = "Indeno(1,2,3-cd)pyrene", PollutantMasterUrl = "5655" },
                new PollutantDetails { PollutantName = "Dibenzo(ac)anthracene", PollutantMasterUrl = "5527" },
                new PollutantDetails { PollutantName = "Dibenzo(ah)anthracene", PollutantMasterUrl = "5419" },
                new PollutantDetails { PollutantName = "Dibenzo(ah+ac)anthracene", PollutantMasterUrl = "7418" },
                new PollutantDetails { PollutantName = "1-Methyl anthracene", PollutantMasterUrl = "7301" },
                new PollutantDetails { PollutantName = "1-Methyl Naphthalene", PollutantMasterUrl = "7309" },
                new PollutantDetails { PollutantName = "1-Methyl phenanthrene", PollutantMasterUrl = "7310" },
                new PollutantDetails { PollutantName = "2-Methyl anthracene", PollutantMasterUrl = "7313" },
                new PollutantDetails { PollutantName = "2-Methyl Naphthalene", PollutantMasterUrl = "7315" },
                new PollutantDetails { PollutantName = "2-Methyl phenanthrene", PollutantMasterUrl = "7317" },
                new PollutantDetails { PollutantName = "4.5-Methylene phenanthrene", PollutantMasterUrl = "7521" },
                new PollutantDetails { PollutantName = "5-Methyl Chrysene", PollutantMasterUrl = "5522" },
                new PollutantDetails { PollutantName = "9-Methyl anthracene", PollutantMasterUrl = "7523" },
                new PollutantDetails { PollutantName = "Acenaphthene", PollutantMasterUrl = "7351" },
                new PollutantDetails { PollutantName = "Acenaphthylene", PollutantMasterUrl = "7352" },
                new PollutantDetails { PollutantName = "Anthanthrene", PollutantMasterUrl = "5364" },
                new PollutantDetails { PollutantName = "Anthracene", PollutantMasterUrl = "5606" },
                new PollutantDetails { PollutantName = "Benzo(b)naphtho(2,1-d)thiophene", PollutantMasterUrl = "5524" },
                new PollutantDetails { PollutantName = "Benzo(c)phenanthrene", PollutantMasterUrl = "5525" },
                new PollutantDetails { PollutantName = "Benzo(e)pyrene", PollutantMasterUrl = "5381" },
                new PollutantDetails { PollutantName = "Benzo(ghi)perylene", PollutantMasterUrl = "5623" },
                new PollutantDetails { PollutantName = "Naphthalene", PollutantMasterUrl = "7465" }

            };
            // Split and normalize the filter string
            var filterList = (filter ?? string.Empty)
                                   .Split(',')
                                   .Select(f => f.Trim())
                                   .ToList();
            // Filter using case-insensitive comparison
            var filtered = allPollutants
                .Where(p => filterList.Contains(p.PollutantName, StringComparer.OrdinalIgnoreCase))
                .ToList();
            return filtered.Count > 0 ? filtered : allPollutants;
        }

        private List<FinalData> ProcessAtomData(JArray features, List<PollutantDetails> pollutants, SiteInfo siteinfo)
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
                    string? cleanedUrl = href is not null
                                        ? href[(href.LastIndexOf('/') + 1)..]
                                        : null;
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
                    Logger.LogError(ex, "Error processing ProcessAtomData feature member");
                }
            }
            return finalList;
        }
        private static List<FinalData> ExtractFinalData(string values, string pollutantName, SiteInfo siteinfo)
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
