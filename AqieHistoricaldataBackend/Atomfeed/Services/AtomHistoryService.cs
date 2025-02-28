using AqieHistoricaldataBackend.Example.Models;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using System.Xml.Linq;
using System.Xml;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using Newtonsoft.Json;
using System.Formats.Asn1;
using System.Globalization;
using CsvHelper;
using AqieHistoricaldataBackend.Utils.Mongo;
using AqieHistoricaldataBackend.Atomfeed.Models;
using Microsoft.Extensions.Logging;
using CsvHelper.Configuration;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomHistoryService(ILogger<AtomHistoryService> logger, IHttpClientFactory httpClientFactory) : IAtomHistoryService //MongoService<AtomHistoryModel>, 
    {
        private readonly IAtomHistoryService _client;
     
        public async Task<string> AtomHealthcheck()
        {

            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync("https://uk-air.defra.gov.uk/data/atom-dls/observations/auto/GB_FixedObservations_2019_CLL2.xml");
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsStringAsync();
            return data;
            //exporttocsv_nextrecord();
            var pollutant_history_url = new List<pollutanturl>
                {
                    new pollutanturl {year = "2019", stationname = "London Bloomsbury", atom_url = "https://uk-air.defra.gov.uk/data/atom-dls/observations/auto/GB_FixedObservations_2019_CLL2.xml", end_date = "2019-11-11T14:00:13Z"},
                    new pollutanturl { year = "2020", stationname = "London Bloomsbury", atom_url = "https://uk-air.defra.gov.uk/data/atom-dls/observations/auto/GB_FixedObservations_2020_CLL2.xml", end_date = "2019-11-07T07:12:09Z"},
                    new pollutanturl { year = "2021", stationname = "London Bloomsbury", atom_url = "https://uk-air.defra.gov.uk/data/atom-dls/observations/auto/GB_FixedObservations_2021_CLL2.xml", end_date = "2019-11-07T07:12:09Z"},
                    new pollutanturl { year = "2022", stationname = "London Bloomsbury", atom_url = "https://uk-air.defra.gov.uk/data/atom-dls/observations/auto/GB_FixedObservations_2022_CLL2.xml", end_date = "2019-11-07T07:12:09Z"},
                    new pollutanturl { year = "2023", stationname = "London Bloomsbury", atom_url = "https://uk-air.defra.gov.uk/data/atom-dls/observations/auto/GB_FixedObservations_2023_CLL2.xml", end_date = "2019-11-07T07:12:09Z"},
                    new pollutanturl { year = "2022", atom_url = "https://uk-air.defra.gov.uk/data/atom-dls/observations/auto/GB_FixedObservations_2022_ACTH.xml", end_date = "2019-11-07T07:12:09Z"},
                    new pollutanturl { year = "2023", stationname = "London Westminster", atom_url = "https://uk-air.defra.gov.uk/data/atom-dls/observations/auto/GB_FixedObservations_2023_HORS.xml", end_date = "2019-11-07T07:12:09Z"}

                };
            foreach (var atomurl in pollutant_history_url)
            {
                try
                {                    
                    logger.LogInformation("Before Fetching URL {atomurl}", atomurl.atom_url);
                    XmlTextReader reader = new XmlTextReader(atomurl.atom_url);
                    logger.LogInformation("After Fetching URL {atomurl}", atomurl.atom_url);
                    XDocument doc = XDocument.Load(atomurl.atom_url);
                    logger.LogInformation("Load Document {atomurl}", atomurl.atom_url);
                    XmlDocument doc1 = new XmlDocument();

                    doc1.LoadXml(doc.ToString());
                    logger.LogInformation("Load Document completed {atomurl}", atomurl.atom_url);
                    string jsonresult = Newtonsoft.Json.JsonConvert.SerializeXmlNode(doc1);
                    if(jsonresult is not null)
                    {
                        return "Success";
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("Error Info message {Error}", ex.Message);
                    logger.LogError("Error Info stacktrace {Error}", ex.StackTrace);
                }
                }
                    return "Success";
        }

        public async Task<string> GetAtomHourlydata(string name)
        {
            var pollutant_history_url = new List<pollutanturl>
                {
                    new pollutanturl {year = "2019", stationname = "London Bloomsbury", atom_url = "https://uk-air.defra.gov.uk/data/atom-dls/observations/auto/GB_FixedObservations_2019_CLL2.xml", end_date = "2019-11-11T14:00:13Z"}

                };
            var pollutant_url = new List<pollutantdetails>
                {
                    new pollutantdetails { polluntantname = "Nitrogen dioxide",pollutant_master_url = "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/8" },
                    new pollutantdetails { polluntantname = "PM10",pollutant_master_url = "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/5"  },
                    new pollutantdetails { polluntantname = "PM2.5",pollutant_master_url = "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/6001"  },
                    new pollutantdetails { polluntantname = "Ozone",pollutant_master_url = "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/7"  },
                    new pollutantdetails { polluntantname = "Sulphur dioxide",pollutant_master_url = "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/1"  }
                };
            List<Finaldata> Final_list = new List<Finaldata>();
            List<DailyAverage> DailyAverages = new List<DailyAverage>();
            List<dailyannualaverage> dailyannualaverages = new List<dailyannualaverage>();
            foreach (var atomurl in pollutant_history_url)
            {
                try
                {
                    XmlTextReader reader = new XmlTextReader(atomurl.atom_url);
                    XDocument doc = XDocument.Load(atomurl.atom_url);
                    XmlDocument doc1 = new XmlDocument();

                    doc1.LoadXml(doc.ToString());

                    string jsonresult = Newtonsoft.Json.JsonConvert.SerializeXmlNode(doc1);

                    var xml_collection = Newtonsoft.Json.Linq.JObject.Parse(jsonresult)["gml:FeatureCollection"]["gml:featureMember"].ToList();

                    int pollutant_count = xml_collection.Count();
                    List<string[]> pollutant_data_List = new List<string[]>();
                    //ArrayList pollutant_data_List = new ArrayList();
                    for (int totalindex = 0; totalindex < pollutant_count - 1; totalindex++)
                    {
                        try
                        {
                            string check_observedProperty_href = Newtonsoft.Json.Linq.JObject.Parse(jsonresult)["gml:FeatureCollection"]["gml:featureMember"].ToList()[totalindex + 1]["om:OM_Observation"]["om:observedProperty"].First.ToString();
                            if (check_observedProperty_href.Contains("xlink:href"))
                            {
                                string poolutant_API_url = Newtonsoft.Json.Linq.JObject.Parse(jsonresult)["gml:FeatureCollection"]["gml:featureMember"].ToList()[totalindex + 1]["om:OM_Observation"]["om:observedProperty"]["@xlink:href"].ToString();

                                if (poolutant_API_url != null)
                                {
                                    foreach (var url_pollutant in pollutant_url)
                                    {
                                        try
                                        {
                                            if (url_pollutant.pollutant_master_url == poolutant_API_url)
                                            {
                                                string pollutant_result_data = Newtonsoft.Json.Linq.JObject.Parse(jsonresult)["gml:FeatureCollection"]["gml:featureMember"].ToList()[totalindex + 1]["om:OM_Observation"]["om:result"]["swe:DataArray"]["swe:values"].ToString();
                                                var pollutant_split_data = pollutant_result_data.Replace("\r\n", "").Trim().Split("@@");
                                                if (pollutant_split_data != null)
                                                {

                                                    foreach (var item in pollutant_split_data)
                                                    {

                                                        //var value = item.Split(",");
                                                        //List<string> pollutant_value_split_list = new List<String>(item.Split(','));
                                                        List<string> pollutant_value_split_list = new List<String>(item.Split(','));

                                                        Finaldata finaldata = new Finaldata();
                                                        finaldata.StartTime = pollutant_value_split_list[0];
                                                        finaldata.EndTime = pollutant_value_split_list[1];
                                                        if (pollutant_value_split_list[2] == "1")
                                                        {
                                                            finaldata.Verification = "Verified";
                                                        }
                                                        else if (pollutant_value_split_list[2] == "2")
                                                        {
                                                            finaldata.Verification = "Preliminary verified";
                                                        }
                                                        else
                                                        {
                                                            finaldata.Verification = "Not verified";
                                                        }
                                                        finaldata.Validity = "ugm-3";
                                                        //finaldata.Validity = pollutant_value_split_list[3];
                                                        finaldata.Value = pollutant_value_split_list[4];
                                                        finaldata.DataCapture = pollutant_value_split_list[5];
                                                        finaldata.Pollutantname = url_pollutant.polluntantname;
                                                        finaldata.Stationname = atomurl.stationname;
                                                        Final_list.Add(finaldata);

                                                    }

                                                }
                                            }
                                        }
                                        catch (Exception ex) { }
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { }
                    }
                    dailyannualaverage dailyannualaverage = new dailyannualaverage();

                    //To get the daily average 
                    var Daily_Total = Final_list.GroupBy(x => new { ReportDate = Convert.ToDateTime(x.StartTime).Date.ToString(), x.Pollutantname, x.Stationname })
.Select(x => new DailyAverage { ReportDate = x.Key.ReportDate, Pollutantname = x.Key.Pollutantname, Stationname = x.Key.Stationname, Total = x.Average(y => Convert.ToDecimal(y.Value)) }).ToList();

                    //To get the yearly average 
                    var Daily_Average = Final_list.GroupBy(x => new { ReportDate = Convert.ToDateTime(x.StartTime).Year.ToString(), x.Pollutantname, x.Stationname })
.Select(x => new DailyAverage { ReportDate = x.Key.ReportDate, Pollutantname = x.Key.Pollutantname, Stationname = x.Key.Stationname, Total = x.Average(y => Convert.ToDecimal(y.Value)) }).ToList();

                    dailyannualaverages.Add(dailyannualaverage);
                    //xml_each_pollutant_reading_time.Sort();


                }
                catch (Exception e) { }
            }

            if (Final_list != null)
            {
                ExporttoCSV(Final_list);
            }



            return "Success";
        }
        public void ExporttoCSV(List<Finaldata> Final_list)
        {
            try
            {
                var csvpath = Path.Combine(Environment.CurrentDirectory, $"Atomfiledownload-{DateTime.Now.ToFileTime()}.csv");
                using (var writer = new StreamWriter("file.csv"))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(Final_list);
                }
            }
            catch (Exception ex) { }
        }

        //public class Foo
        //{
        //    public int Id { get; set; }
        //    public string Name { get; set; }
        //}
        //public class FooMap : ClassMap<Foo>
        //{
        //    public FooMap()
        //    {
        //        Map(m => m.Id).Index(6).Name("id");
        //        Map(m => m.Name).Index(7).Name("name");
        //    }
        //}
        public void exporttocsv_nextrecord()
        {
            //using (var writer = new StreamWriter("test_nextrecord.csv"))
            //using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            //{
            //    // Write header
            //    csv.WriteField("Id");
            //    csv.WriteField("Name");
            //    csv.WriteField("Email");
            //    csv.NextRecord();

            //    // Write first record
            //    csv.WriteField(1);
            //    csv.WriteField("John Doe");
            //    csv.WriteField("john.doe@example.com");
            //    csv.NextRecord();

            //    // Write second record
            //    csv.WriteField(2);
            //    csv.WriteField("Jane Smith");
            //    csv.WriteField("jane.smith@example.com");
            //    csv.NextRecord();
            //}
            //    var records = new List<Foo>
            //    {
            //        new Foo { Id = 1, Name = "one" },
            //        new Foo { Id = 2, Name = "two" },
            //    };
            //using (var writer = new StreamWriter("test_nextrecord.csv"))
            //using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            //{
            //    csv.WriteHeader<Foo>();
            //    csv.NextRecord();
            //    foreach (var record in records)
            //    {
            //        csv.WriteRecord(record);
            //        csv.NextRecord();
            //    }

          
        }
    }
}
