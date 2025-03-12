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
using System.Collections.Generic;
using System.IO;
using SharpCompress.Writers;
using System.Text;
using AqieHistoricaldataBackend.Utils.Http;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Newtonsoft.Json.Linq;
using SharpCompress.Common;
using Amazon.S3.Model;
using Amazon.S3;
using Amazon.S3.Transfer;
using Amazon.Util;
using static System.Net.Mime.MediaTypeNames;
using Amazon;
using Elastic.CommonSchema;
using System.Net.Sockets;
using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Amazon.Runtime.Internal;
using static Amazon.Internal.RegionEndpointProviderV2;


namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomHistoryService(ILogger<AtomHistoryService> logger, IHttpClientFactory httpClientFactory) : IAtomHistoryService //MongoService<AtomHistoryModel>, 
    {
    
        public async Task<string> AtomHealthcheck()
        {
            try
            {
                var client = httpClientFactory.CreateClient("Atomfeed");
                //var response = await client.GetAsync("https://uk-air.defra.gov.uk/data/atom-dls/observations/auto/GB_FixedObservations_2019_CLL2.xml");
                logger.LogInformation("client base address {client}", client.ToString());
                logger.LogInformation("Before Fetching citizen URL {starttime}", DateTime.Now);
                var response = await client.GetAsync("https://aqie-back-end.dev.cdp-int.defra.cloud/measurements");
                logger.LogInformation("After Fetching response citizen URL {endtime}", DateTime.Now);

                logger.LogInformation("Before Fetching Atom URL {atomurl}", DateTime.Now);
                //var Atomresponse = await client.GetAsync("search/places/v1/postcode?postcode=bt666ru&key=&maxresults=1");
                var Atomresponse = await client.GetAsync("data/atom-dls/observations/auto/GB_FixedObservations_2019_CLL2.xml");
                logger.LogInformation("After Fetching response Atom URL {atomurl}", DateTime.Now);
                Atomresponse.EnsureSuccessStatusCode();

                logger.LogInformation("Before data Reading the Atom URL {atomurl}", DateTime.Now);
                var data = await Atomresponse.Content.ReadAsStringAsync();
                logger.LogInformation("After data Fetching the Atom URL {atomurl}", DateTime.Now);

                //                    return "Success";
                return data;
            }
            catch (Exception ex)
            {
                logger.LogError("Error AtomHealthcheck Info message {Error}", ex.Message);
                logger.LogError("Error AtomHealthcheck Info stacktrace {Error}", ex.StackTrace);
                return "Error";
            }
        }
        public async Task<string> GetAtomHourlydata(querystringdata data)
        {
            //logger.LogInformation("Frontend API object region {data[0]}", data[0]);
            //logger.LogInformation("Frontend API object siteType {data[1]}", data[1]);
            //logger.LogInformation("Frontend API object sitename {data[2]}", data[2]);
            //logger.LogInformation("Frontend API object siteId {data[3]}", data[3]);
            //logger.LogInformation("Frontend API object latitude {data[4]}", data[4]);
            //logger.LogInformation("Frontend API object longitude {data[5]}", data[5]);
            //logger.LogInformation("Frontend API object year {data[6]}", data[6]);

            //string siteId = "BEX";//data[3];
            //string year = "2019";//data[6];
            //string s3Key1 = "measurement_data_" + siteId + "_" + year + ".csv";
            string siteId = data.siteId;
            string year = data.year;

            //string[] apiparams = {
            //  region: stndetails.region,
            //  siteType: stndetails.siteType,
            //  sitename: stndetails.name,
            //  siteId: stndetails.localSiteID,
            //  latitude: stndetails.location.coordinates[0],
            //  longitude: stndetails.location.coordinates[1],
            // year: request.yar.get('yearselected')
            //};
            string PresignedUrl = string.Empty;
             atomfeedexport_csv();
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
                try
                {
                var client = httpClientFactory.CreateClient("Atomfeed");
                string Atomurl = "data/atom-dls/observations/auto/GB_FixedObservations_" + year + "_" + siteId + ".xml";
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
                    List<string[]> pollutant_data_List = new List<string[]>();
                    //ArrayList pollutant_data_List = new ArrayList();
                    for (int totalindex = 0; totalindex < pollutant_count - 1; totalindex++)
                    {
                        try
                        {
                            string check_observedProperty_href = Newtonsoft.Json.Linq.JObject.Parse(AtomresponseJson)["gml:FeatureCollection"]["gml:featureMember"].ToList()[totalindex + 1]["om:OM_Observation"]["om:observedProperty"].First.ToString();
                            if (check_observedProperty_href.Contains("xlink:href"))
                            {
                                string poolutant_API_url = Newtonsoft.Json.Linq.JObject.Parse(AtomresponseJson)["gml:FeatureCollection"]["gml:featureMember"].ToList()[totalindex + 1]["om:OM_Observation"]["om:observedProperty"]["@xlink:href"].ToString();

                                if (poolutant_API_url != null)
                                {
                                    foreach (var url_pollutant in pollutant_url)
                                    {
                                        try
                                        {
                                            if (url_pollutant.pollutant_master_url == poolutant_API_url)
                                            {
                                                string pollutant_result_data = Newtonsoft.Json.Linq.JObject.Parse(AtomresponseJson)["gml:FeatureCollection"]["gml:featureMember"].ToList()[totalindex + 1]["om:OM_Observation"]["om:result"]["swe:DataArray"]["swe:values"].ToString();
                                                var pollutant_split_data = pollutant_result_data.Replace("\r\n", "").Trim().Split("@@");
                                                if (pollutant_split_data != null)
                                                {

                                                    foreach (var item in pollutant_split_data)
                                                    {

                                                        //var value = item.Split(",");
                                                        //List<string> pollutant_value_split_list = new List<String>(item.Split(','));
                                                        List<string> pollutant_value_split_list = new List<System.String>(item.Split(','));

                                                        Finaldata finaldata = new Finaldata();
                                                        finaldata.StartTime = pollutant_value_split_list[0];
                                                        finaldata.EndTime = pollutant_value_split_list[1];
                                                        finaldata.Verification = pollutant_value_split_list[2];
                                                    if (pollutant_value_split_list[2] == "1")
                                                    {
                                                        //finaldata.Verification = "Verified";
                                                        finaldata.Verification = "V";
                                                    }
                                                    else if (pollutant_value_split_list[2] == "2")
                                                    {
                                                        //finaldata.Verification = "Preliminary verified";
                                                        finaldata.Verification = "P";
                                                    }
                                                    else
                                                    {
                                                        //finaldata.Verification = "Not verified";
                                                        finaldata.Verification = "N";
                                                    }
                                                    //finaldata.Validity = "ugm-3";
                                                    //finaldata.Validity = pollutant_value_split_list[3];
                                                    finaldata.Value = pollutant_value_split_list[4];
                                                        //finaldata.DataCapture = pollutant_value_split_list[5];
                                                        finaldata.Pollutantname = url_pollutant.polluntantname;
                                                        //finaldata.Stationname = "London Bloomsbury";//atomurl.stationname;
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
                        catch (Exception ex) {

                    }
                    }
                    dailyannualaverage dailyannualaverage = new dailyannualaverage();

                try
                {
                    
                    var csvbyte = atomfeedexport_csv(Final_list, data);
                    //return csvbyte;
                    string Region = Environment.GetEnvironmentVariable("AWS_REGION") ?? throw new ArgumentNullException("AWS_REGION");
                    string s3BucketName = "dev-aqie-historicaldata-backend-c63f2";
                    string s3Key = "measurement_data_" + siteId + "_" + year + ".csv";   
                    var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(Region);
                    logger.LogInformation("S3 region {regionEndpoint}", regionEndpoint);

                    using (var s3Client = new Amazon.S3.AmazonS3Client())
                    {
                        using (var transferUtility = new TransferUtility(s3Client))
                        {
                            using (var stream = new MemoryStream(csvbyte))
                            {
                                logger.LogInformation("S3 upload start", DateTime.Now);
                                await transferUtility.UploadAsync(stream, s3BucketName, s3Key);
                                logger.LogInformation("S3 upload end", DateTime.Now);                                
                            }
                        }
                    }
                    logger.LogInformation("S3 PresignedUrl start", DateTime.Now);
                    PresignedUrl = GeneratePreSignedURL(s3BucketName, s3Key, 604800);
                    logger.LogInformation("S3 PresignedUrl final URL {PresignedUrl}", PresignedUrl);
                }
                catch (Exception ex)
                {
                    logger.LogError("Error S3 Info message {Error}", ex.Message);
                    logger.LogError("Error S3 Info stacktrace {Error}", ex.StackTrace);
                }  

                //To get the daily average 
                var Daily_Total = Final_list.GroupBy(x => new { ReportDate = Convert.ToDateTime(x.StartTime).Date.ToString(), x.Pollutantname,x.Verification })
.Select(x => new DailyAverage { ReportDate = x.Key.ReportDate, Pollutantname = x.Key.Pollutantname, Verification = x.Key.Verification, Total = x.Average(y => Convert.ToDecimal(y.Value)) }).ToList();

                    //To get the yearly average 
                    var Daily_Average = Final_list.GroupBy(x => new { ReportDate = Convert.ToDateTime(x.StartTime).Year.ToString(), x.Pollutantname })
.Select(x => new DailyAverage { ReportDate = x.Key.ReportDate, Pollutantname = x.Key.Pollutantname, Total = x.Average(y => Convert.ToDecimal(y.Value)) }).ToList();

                    dailyannualaverages.Add(dailyannualaverage);
                    //xml_each_pollutant_reading_time.Sort();
                }
                catch (Exception ex) {
                logger.LogError("Error in Atom feed pull Info message {Error}", ex.Message);
                logger.LogError("Error in Atom feed pull {Error}", ex.StackTrace);
            }

            return PresignedUrl;//PresignedUrl;//"S3 Bucket loaded Successfully";
        }             

        public byte[] atomfeedexport_csv(List<Finaldata> Final_list, querystringdata data)
        {
            try
            {
                string region = data.region;       /*data[0];*/
                string siteType = data.siteType;    /*data[1];*/
                string sitename = data.sitename;     /*data[2];*/
                string latitude = data.latitude;     /*data[4];*/
                string longitude = data.longitude;    /*data[5];*/
                //string[] apiparams = {
                //  region: stndetails.region,
                //  siteType: stndetails.siteType,
                //  sitename: stndetails.name,
                //  siteId: stndetails.localSiteID,
                //  latitude: stndetails.location.coordinates[0],
                //  longitude: stndetails.location.coordinates[1],
                // year: request.yar.get('yearselected')
                //};

                //StringBuilder pollutantnameheaders = new StringBuilder();
                //StringBuilder pollutantdataheaders = new StringBuilder();
                //string Ozone = string.Empty;
                //string PM10 = string.Empty;
                //string PM25 = string.Empty;
                //string SulphurDioxide = string.Empty;
                //string NitrogenDioxide = string.Empty;
                //var distinctpollutantFilteredNames = Final_list
                //                                    .Select(o => o.Pollutantname)
                //                                    .Distinct()
                //                                    .ToList();
                //int pollutantfiltercount = distinctpollutantFilteredNames.Count();

                //foreach (var data in distinctpollutantFilteredNames)
                //{
                //    if(data == "Ozone")
                //    {
                //        Ozone = data;
                //    }
                //    else if (data == "Nitrogen dioxide")
                //    {
                //        NitrogenDioxide = data;
                //    }
                //    else if (data == "Sulphur dioxide")
                //    {
                //        SulphurDioxide = data;
                //    }
                //    else if (data == "PM10")
                //    {
                //        PM10 = data;
                //    }
                //    else if (data == "PM2.5")
                //    {
                //        PM25 = data;
                //    }
                //}
                
                //string sitename = "Birmingham A4540 Roadside";
                var csv = new StringBuilder();
                // Adding metadata
                csv.AppendLine(string.Format("Hourly measurement data supplied by UK-air on,{0}", sitename));
                csv.AppendLine(string.Format("Site Name,{0}", sitename));
                csv.AppendLine(string.Format("Site Type,{0}", siteType));
                csv.AppendLine(string.Format("Region,{0}", region));
                csv.AppendLine(string.Format("Latitude,{0}", latitude));
                csv.AppendLine(string.Format("Longitude,{0}", longitude));
                csv.AppendLine("Notes:	 [1] All Data GMT hour ending;  [2] Some shorthand is used, V = Verified, P = Provisionally Verified, N = Not Verified, S = Suspect, [3] Unit of measurement (for pollutants) = ugm-3, [4] Instrument type is included in 'Status' for PM10 and PM2.5");
                //pollutantnameheaders.Append("Date,Time");                
                //foreach (var pollutantname in distinctpollutantFilteredNames)
                //{
                //    if(pollutantname == "PM2.5" || pollutantname == "PM10")
                //    {
                //        pollutantnameheaders.Append("," + pollutantname + "particulate matter (Hourly measured),Status");
                //    }
                //    else
                //    {
                //        pollutantnameheaders.Append("," + pollutantname + ",Status");
                //    }                        
                //}
                csv.AppendLine("Date,Time,Ozone,Status,Nitrogen dioxide,Status,Sulphur dioxide,Status,PM10 particulate matter (Hourly measured),Status,PM2.5 particulate matter (Hourly measured),Status");
                //csv.AppendLine(pollutantnameheaders.ToString());
                var groupedData = Final_list.GroupBy(x => new { Convert.ToDateTime(x.StartTime).Date, Convert.ToDateTime(x.StartTime).TimeOfDay });
                //var groupedData1 = Final_list.GroupBy(x => new { Convert.ToDateTime(x.StartTime).Date, Convert.ToDateTime(x.StartTime).TimeOfDay })
                //                              .Select(g => new { Key = g.Key, Items = g.Where(x => x > 3 && x < 9) });
                string filePath = "measurement_data.csv";
                foreach (var group in groupedData)
                {
                    // Adding data rows
                    var date = group.Key.Date.ToString("yyyy-MM-dd");
                    var time = group.Key.TimeOfDay.ToString("hh\\:mm");
                    var ozone = group.FirstOrDefault(x => x.Pollutantname == "Ozone")?.Value ?? "";
                    var ozoneStatus = group.FirstOrDefault(x => x.Pollutantname == "Ozone")?.Verification ?? "";
                    var nitrogenDioxide = group.FirstOrDefault(x => x.Pollutantname == "Nitrogen dioxide")?.Value ?? "";
                    var nitrogenDioxideStatus = group.FirstOrDefault(x => x.Pollutantname == "Nitrogen dioxide")?.Verification ?? "";
                    var sulphurDioxide = group.FirstOrDefault(x => x.Pollutantname == "Sulphur dioxide")?.Value ?? "";
                    var sulphurDioxideStatus = group.FirstOrDefault(x => x.Pollutantname == "Sulphur dioxide")?.Verification ?? "";
                    var pm10 = group.FirstOrDefault(x => x.Pollutantname == "PM10")?.Value ?? "";
                    var pm10Status = group.FirstOrDefault(x => x.Pollutantname == "PM10")?.Verification ?? "";
                    var pm25 = group.FirstOrDefault(x => x.Pollutantname == "PM2.5")?.Value ?? "";
                    var pm25Status = group.FirstOrDefault(x => x.Pollutantname == "PM2.5")?.Verification ?? "";

                    // Adding headers
                    var newline = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}", date, time, ozone, ozoneStatus, nitrogenDioxide, nitrogenDioxideStatus, sulphurDioxide, sulphurDioxideStatus, pm10, pm10Status, pm25, pm25Status);
                    csv.AppendLine(newline);

                }
                // Writing to CSV file
                System.IO.File.WriteAllText(filePath, csv.ToString());
                var bytecontent = Encoding.UTF8.GetBytes(csv.ToString());
                return bytecontent;

            }
            catch (Exception ex)
            {
                logger.LogError("Error csv Info message {Error}", ex.Message);
                logger.LogError("Error csv Info stacktrace {Error}", ex.StackTrace);
                return new byte[] { 0x20};
            }

        }
        public string GeneratePreSignedURL(string bucketName, string keyName, double duration)
        {
            try
            {
                var s3Client = new AmazonS3Client();
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = keyName,
                    Expires = DateTime.UtcNow.AddSeconds(duration)
                };

                string url = s3Client.GetPreSignedURL(request);
                return url;
            }
            catch (AmazonS3Exception ex)
            {               
                logger.LogError("AmazonS3Exception Error:{Error}", ex.Message);
                return "error";
            }
            catch (Exception ex)
            {
                logger.LogError("Error in GeneratePreSignedURL Info message {Error}", ex.Message);
                logger.LogError("Error in GeneratePreSignedURL {Error}", ex.StackTrace);
                return "error";
            }
        }

        public class Sale
        {
            public string Product { get; set; }
            public string Month { get; set; }
            public int Sales { get; set; }
            public string Region { get; set; }
            public string Category { get; set; }
            public string Salesperson { get; set; }
            public double Discount { get; set; }
        }
        public void atomfeedexport_csv()
        {
            string sitename = "Birmingham A4540 Roadside";
            //string.Format("Site Name,{0}", sitename);
            var salesData = new List<Sale>
        {
            new Sale { Product = "ProductA", Month = "January", Sales = 100, Region = "North", Category = "Electronics", Salesperson = "Alice", Discount = 0.1 },
            new Sale { Product = "ProductA", Month = "February", Sales = 150, Region = "North", Category = "Electronics", Salesperson = "Alice", Discount = 0.15 },
            new Sale { Product = "ProductB", Month = "January", Sales = 200, Region = "South", Category = "Furniture", Salesperson = "Bob", Discount = 0.2 },
            new Sale { Product = "ProductB", Month = "February", Sales = 250, Region = "South", Category = "Furniture", Salesperson = "Bob", Discount = 0.25 },
            new Sale { Product = "ProductA", Month = "March", Sales = 120, Region = "North", Category = "Electronics", Salesperson = "Alice", Discount = 0.12 },
            new Sale { Product = "ProductB", Month = "March", Sales = 300, Region = "South", Category = "Furniture", Salesperson = "Bob", Discount = 0.3 },
        };

            var pivotData = salesData
                .GroupBy(s => s.Product)
                .Select(g => new
                {
                    Product = g.Key,
                    SalesByMonth = g.ToDictionary(s => s.Month, s => s.Sales),
                    Region = g.First().Region,
                    Category = g.First().Category,
                    Salesperson = g.First().Salesperson,
                    Discount = g.Average(s => s.Discount)
                }).ToList();

            var months = salesData.Select(s => s.Month).Distinct().OrderBy(m => m).ToList();

            //using (var writer = new StreamWriter("SalesData.csv"))
            //{
            //    // Write data without headers
            //    foreach (var sale in salesData)
            //    {
            //        writer.WriteLine($"{sale.Product},{sale.Month},{sale.Sales},{sale.Region},{sale.Category},{sale.Salesperson},{sale.Discount}");
            //    }
            //}

            using (var writer = new StreamWriter("PivotData.csv"))
            {
                writer.WriteLine(string.Format("Site Name,{0}", sitename));
                writer.WriteLine("Site Type,Urban Traffic");
                writer.WriteLine("Region,Birmingham");
                writer.WriteLine("Latitude,52.476145");
                writer.WriteLine("Longitude,-1.874978");
                // Write headers
                writer.Write("Product,Region,Category,Salesperson,Discount");
                foreach (var month in months)
                {
                    writer.Write($",{month}");
                }
                writer.WriteLine();

                // Write data
                foreach (var item in pivotData)
                {
                    writer.Write($"{item.Product},{item.Region},{item.Category},{item.Salesperson},{item.Discount}");
                    foreach (var month in months)
                    {
                        item.SalesByMonth.TryGetValue(month, out int sales);
                        writer.Write($",{sales}");
                    }
                    writer.WriteLine();
                }
            }

        }

    }
}

