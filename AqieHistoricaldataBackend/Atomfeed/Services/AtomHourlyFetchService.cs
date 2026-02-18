using Newtonsoft.Json.Linq;
using System.Xml;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using System.Text.RegularExpressions;


namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomHourlyFetchService(ILogger<AtomHourlyFetchService> logger, IHttpClientFactory httpClientFactory) : IAtomHourlyFetchService
    {
        public async Task<List<FinalData>> GetAtomHourlydatafetch(string siteID, string year, string downloadfilter)
        {
            var pollutantsToDisplay = GetPollutantsToDisplay(downloadfilter);
            var atomJsonCollection = await FetchAtomFeedAsync(siteID, year);

            return ProcessAtomData(atomJsonCollection, pollutantsToDisplay);
        }


        private List<PollutantDetails> GetPollutantsToDisplay(string filter)
        {
            var allPollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "Nitrogen dioxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/8" },
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" },
                new PollutantDetails { PollutantName = "PM2.5", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/6001" },
                new PollutantDetails { PollutantName = "Ozone", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/7" },
                new PollutantDetails { PollutantName = "Sulphur dioxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/1" }
            };

            var filtered = allPollutants.Where(p => p.PollutantName == filter);
            return filtered.Any() ? filtered.ToList() : allPollutants;
        }

        private async Task<JArray> FetchAtomFeedAsync(string siteID, string year)
        {
            try
            {
                logger.LogInformation("using proxy fetch{datetim}", DateTime.Now);
                var client = httpClientFactory.CreateClient("Atomfeed");
                var url = $"data/atom-dls/observations/auto/GB_FixedObservations_{year}_{siteID}.xml";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.IfModifiedSince = DateTime.UnixEpoch; // Jan 1, 1970 - ensures we always get data
                request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
                request.Headers.TryAddWithoutValidation("Pragma", "no-cache");

                var response = await client.SendAsync(request);
                logger.LogInformation("using proxy fetch response{datetim}", DateTime.Now);

                if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    logger.LogWarning("Server returned 304 Not Modified for site {SiteID} year {Year}", siteID, year);
                    return new JArray();
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    logger.LogWarning("HTTP {StatusCode} when fetching Atom feed for site {SiteID} year {Year}. Response: {Response}", 
                        (int)response.StatusCode, siteID, year, errorContent);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.PreconditionRequired)
                    {
                        logger.LogError("Server returned 428 Precondition Required. Check if User-Agent, cookies, or other headers are needed.");
                    }
                    
                    return new JArray();
                }

                var stream = await response.Content.ReadAsStreamAsync();
                var xml = new XmlDocument();
                xml.Load(stream);
                var json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xml);
                return JObject.Parse(json)["gml:FeatureCollection"]["gml:featureMember"] as JArray;
            }
            catch (Exception ex)
            {
                logger.LogError("Error fetching Atom feed: {Error}", ex);
                return new JArray();
            }
        }

        private List<FinalData> ProcessAtomData(JArray features, List<PollutantDetails> pollutants)
        {
            var finalList = new List<FinalData>();

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
                            finalList.AddRange(ExtractFinalData(values, match.PollutantName));
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("Error processing ProcessAtomData feature member: {Error}", ex);
                }
            }

            return finalList;
        }

        private List<FinalData> ExtractFinalData(string values, string pollutantName)
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
                    PollutantName = pollutantName
                }).ToList();
        }
    }
}