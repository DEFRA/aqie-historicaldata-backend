using Newtonsoft.Json.Linq;
using System.Xml;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using System.Text.RegularExpressions;


namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomHourlyFetchService(ILogger<AtomHourlyFetchService> logger, IHttpClientFactory httpClientFactory) : IAtomHourlyFetchService
    {
        public async Task<List<Finaldata>> GetAtomHourlydatafetch(string siteID, string year, string downloadfilter)
        {
            var pollutantsToDisplay = GetPollutantsToDisplay(downloadfilter);
            var atomJsonCollection = await FetchAtomFeedAsync(siteID, year);

            return ProcessAtomData(atomJsonCollection, pollutantsToDisplay);
        }

        private List<pollutantdetails> GetPollutantsToDisplay(string filter)
        {
            var allPollutants = new List<pollutantdetails>
            {
                new pollutantdetails { polluntantname = "Nitrogen dioxide", pollutant_master_url = "dd.eionet.europa.eu/vocabulary/aq/pollutant/8" },
                new pollutantdetails { polluntantname = "PM10", pollutant_master_url = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" },
                new pollutantdetails { polluntantname = "PM2.5", pollutant_master_url = "dd.eionet.europa.eu/vocabulary/aq/pollutant/6001" },
                new pollutantdetails { polluntantname = "Ozone", pollutant_master_url = "dd.eionet.europa.eu/vocabulary/aq/pollutant/7" },
                new pollutantdetails { polluntantname = "Sulphur dioxide", pollutant_master_url = "dd.eionet.europa.eu/vocabulary/aq/pollutant/1" }
            };

            var filtered = allPollutants.Where(p => p.polluntantname == filter);
            return filtered.Any() ? filtered.ToList() : allPollutants;
        }

        private async Task<JArray> FetchAtomFeedAsync(string siteID, string year)
        {
            try
            {
                var client = httpClientFactory.CreateClient("Atomfeed");
                var url = $"data/atom-dls/observations/auto/GB_FixedObservations_{year}_{siteID}.xml";
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

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

        private List<Finaldata> ProcessAtomData(JArray features, List<pollutantdetails> pollutants)
        {
            var finalList = new List<Finaldata>();

            for (int i = 1; i < features.Count; i++)
            {
                try
                {
                    var feature = features[i];
                    var href = feature["om:OM_Observation"]?["om:observedProperty"]?["@xlink:href"]?.ToString();
                    string cleanedUrl = Regex.Replace(href ?? "", @"^https?://", "");
                    if (string.IsNullOrEmpty(href)) continue;

                    var match = pollutants.FirstOrDefault(p => p.pollutant_master_url == cleanedUrl);
                    if (match != null)
                    {
                        var values = feature["om:OM_Observation"]?["om:result"]?["swe:DataArray"]?["swe:values"]?.ToString();
                        if (!string.IsNullOrEmpty(values))
                        {
                            finalList.AddRange(ExtractFinalData(values, match.polluntantname));
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

        private List<Finaldata> ExtractFinalData(string values, string pollutantName)
        {
            return values.Replace("\r\n", "").Trim().Split("@@")
                .Select(item => item.Split(','))
                .Where(parts => parts.Length >= 5)
                .Select(parts => new Finaldata
                {
                    StartTime = parts[0],
                    EndTime = parts[1],
                    Verification = parts[2],
                    Validity = parts[3],
                    Value = parts[4],
                    Pollutantname = pollutantName
                }).ToList();
        }
    }
}
