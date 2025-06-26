using Newtonsoft.Json.Linq;
using System.Xml;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomHourlyFetchService(ILogger<AtomHourlyFetchService> logger, IHttpClientFactory httpClientFactory) : IAtomHourlyFetchService
    {
        public async Task<List<Finaldata>> GetAtomHourlydatafetch(string siteID, string year, string downloadfilter)
        {
            var pollutant_url = new List<pollutantdetails>
                            {
                                new pollutantdetails { polluntantname = "Nitrogen dioxide",pollutant_master_url = "https://dd.eionet.europa.eu/vocabulary/aq/pollutant/8" },
                                new pollutantdetails { polluntantname = "PM10",pollutant_master_url = "https://dd.eionet.europa.eu/vocabulary/aq/pollutant/5"  },
                                new pollutantdetails { polluntantname = "PM2.5",pollutant_master_url = "https://dd.eionet.europa.eu/vocabulary/aq/pollutant/6001"  },
                                new pollutantdetails { polluntantname = "Ozone",pollutant_master_url = "https://dd.eionet.europa.eu/vocabulary/aq/pollutant/7"  },
                                new pollutantdetails { polluntantname = "Sulphur dioxide",pollutant_master_url = "https://dd.eionet.europa.eu/vocabulary/aq/pollutant/1"  }
                            };
            var filterpollutant = pollutant_url.Where(P => P.polluntantname == downloadfilter);

            // If filterpollutant is empty, use pollutant_url
            var pollutantsToDisplay = filterpollutant.Any() ? filterpollutant : pollutant_url;

            List<Finaldata> Final_list = new List<Finaldata>();
            try
            {
                var client = httpClientFactory.CreateClient("Atomfeed");
                string Atomurl = "data/atom-dls/observations/auto/GB_FixedObservations_" + year + "_" + siteID + ".xml";
                //var Atomresponse = await client.GetAsync("data/atom-dls/observations/auto/GB_FixedObservations_2019_BEX.xml");
                var Atomresponse = await client.GetAsync(Atomurl);
                Atomresponse.EnsureSuccessStatusCode();

                var AtomresponseStream = await Atomresponse.Content.ReadAsStreamAsync();
                var AtomresponseString = new StreamReader(AtomresponseStream).ReadToEnd();
                var AtomresponseXml = new XmlDocument();
                AtomresponseXml.LoadXml(AtomresponseString);
                var AtomresponseJson = Newtonsoft.Json.JsonConvert.SerializeXmlNode(AtomresponseXml);
                var AtomresponseJsonCollection = JObject.Parse(AtomresponseJson)["gml:FeatureCollection"]["gml:featureMember"].ToList();
                var AtomresponseJsonCollectionString = Newtonsoft.Json.JsonConvert.SerializeObject(AtomresponseJsonCollection);

                int pollutant_count = AtomresponseJsonCollection.Count();
                for (int totalindex = 0; totalindex < pollutant_count - 1; totalindex++)
                {
                    try
                    {
                        var featureMember = Newtonsoft.Json.Linq.JObject.Parse(AtomresponseJson)["gml:FeatureCollection"]["gml:featureMember"].ToList()[totalindex + 1];
                        var observedProperty = featureMember["om:OM_Observation"]["om:observedProperty"];
                        var check_observedProperty_href = observedProperty.First.ToString();

                        if (check_observedProperty_href.Contains("xlink:href"))
                        {
                            var poolutant_API_url = observedProperty["@xlink:href"].ToString();
                            if (!string.IsNullOrEmpty(poolutant_API_url))
                            {
                                foreach (var url_pollutant in pollutantsToDisplay)
                                {
                                    if (url_pollutant.pollutant_master_url == poolutant_API_url)
                                    {
                                        var pollutant_result_data = featureMember["om:OM_Observation"]["om:result"]["swe:DataArray"]["swe:values"].ToString();
                                        var pollutant_split_data = pollutant_result_data.Replace("\r\n", "").Trim().Split("@@");
                                        foreach (var item in pollutant_split_data)
                                        {
                                            var pollutant_value_split_list = item.Split(',').ToList();
                                            Finaldata finaldata = new Finaldata
                                            {
                                                StartTime = pollutant_value_split_list[0],
                                                EndTime = pollutant_value_split_list[1],
                                                Verification = pollutant_value_split_list[2],
                                                Validity = pollutant_value_split_list[3],
                                                Value = pollutant_value_split_list[4],
                                                Pollutantname = url_pollutant.polluntantname
                                            };
                                            Final_list.Add(finaldata);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("Error in atom hourly feed data processing {Error}", ex.Message);
                        logger.LogError("Error in atom hourly feed data processing {Error}", ex.StackTrace);
                    }
                }                
            }
            catch (Exception ex)
            {
                logger.LogError("Error in Atom feed fetch {Error}", ex.Message);
                logger.LogError("Error in Atom feed fetch {Error}", ex.StackTrace);
            }
            return Final_list;
        }
       
    }
}
