using Newtonsoft.Json.Linq;
using System.Xml;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionHourlyFetchService(ILogger<HistoryexceedenceService> Logger,
    IHttpClientFactory httpClientFactory) : IAtomDataSelectionHourlyFetchService
    {
            public async Task<List<FinalData>> GetAtomDataSelectionHourlyFetchService(List<SiteInfo> filtered_station_pollutant, string pollutantName, string filteryear)
            {
                try
                {
                    List<PollutantDetails> Final_list = new List<PollutantDetails>();
                    List<FinalData> Final_list1 = new List<FinalData>();
                //foreach (var pollutant in filtered_station_pollutant)
                //{
                //var siteID = "AH";//pollutant.stationCode;//AH
                //var year = "2024";//pollutant.year;
                //var pollutantsToDisplay = GetPollutantsToDisplay(pollutantName);
                //var atomJsonCollection = await FetchAtomFeedAsync("AH", "2024");//(siteID, year)
                //var finalhourlypollutantresult = ProcessAtomData(atomJsonCollection, pollutantsToDisplay);
                //Final_list1.AddRange(finalhourlypollutantresult);
                //}
                //var atomJsonCollection = await FetchAtomFeedAsync(siteID, year);

                foreach (var siteinfo in filtered_station_pollutant)
                {
                    var siteID = siteinfo.LocalSiteId;
                    var year = filteryear;
                    var pollutantsToDisplay = GetPollutantsToDisplay(pollutantName);
                    var atomJsonCollection = await FetchAtomFeedAsync(siteID, year);
                    var finalhourlypollutantresult = ProcessAtomData(atomJsonCollection, pollutantsToDisplay, siteinfo);
                    Final_list1.AddRange(finalhourlypollutantresult);
                }
                return Final_list1;

                }
                catch (Exception ex)
                {
                    Logger.LogError("Error in GetAtomDataSelectionHourlyFetchService {Error}", ex.Message);
                    Logger.LogError("Error in GetAtomDataSelectionHourlyFetchService {Error}", ex.StackTrace);
                List<FinalData> Final_list1 = new List<FinalData>();
                return Final_list1;
                }
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
                    Logger.LogError("Error fetching Atom feed: {Error}", ex);
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
                    new PollutantDetails { PollutantName = "Sulphur dioxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/1" }
                };

                var filtered = allPollutants.Where(p => p.PollutantName == filter);
                return filtered.Any() ? filtered.ToList() : allPollutants;
            }
            private List<FinalData> ProcessAtomData(JArray features, List<PollutantDetails> pollutants, SiteInfo siteinfo)
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
                        Region = "Greater London",
                        Country = "England"
                    }).ToList();
            }
    }
}
