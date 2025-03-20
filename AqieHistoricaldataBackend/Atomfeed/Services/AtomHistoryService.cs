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
using Hangfire;


namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomHistoryService(ILogger<AtomHistoryService> logger, IHttpClientFactory httpClientFactory) : IAtomHistoryService //MongoService<AtomHistoryModel>, 
    {
    
        public async Task<string> AtomHealthcheck()
        {
            try
            {
                var client = httpClientFactory.CreateClient("Atomfeed");
                var Atomresponse = await client.GetAsync("data/atom-dls/observations/auto/GB_FixedObservations_2019_CLL2.xml");
                Atomresponse.EnsureSuccessStatusCode();
                var data = await Atomresponse.Content.ReadAsStringAsync();

                // Schedule a recurring job.
                RecurringJob.AddOrUpdate(
                    "call-api-job",
                    () => CallApi(),
                    Cron.Minutely); // Schedule to run daily

                return Atomresponse.ToString();
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
            string siteId = data.siteId;
            string year = data.year;
            string PresignedUrl = string.Empty;           

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
                                foreach (var url_pollutant in pollutant_url)
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
                        logger.LogError("Error in atom feed fetch {Error}", ex.Message);
                        logger.LogError("Error in atom feed fetch {Error}", ex.StackTrace);
                    }
                }
                dailyannualaverage dailyannualaverage = new dailyannualaverage();
                try
                {
                    var csvbyte = atomfeedexport_csv(Final_list, data);
                    //var csvbyte = atomfeedexport_csv_fixedheader(Final_list, data);
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
                logger.LogError("Error in Atom feed fetch {Error}", ex.Message);
                logger.LogError("Error in Atom feed fetch {Error}", ex.StackTrace);
            }

            return PresignedUrl;//PresignedUrl;//"S3 Bucket loaded Successfully";
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

        public byte[] atomfeedexport_csv(List<Finaldata> Final_list, querystringdata data)
        {
            try
            {
                string pollutantnameheaderchange = string.Empty;
                string region = data.region;       
                string siteType = data.siteType;   
                string sitename = data.sitename;   
                string latitude = data.latitude;   
                string longitude = data.longitude; 
                //string sitename = "Birmingham A4540 Roadside";
                //string.Format("Site Name,{0}", sitename);
                var groupedData = Final_list.GroupBy(x => new { Convert.ToDateTime(x.StartTime).Date, Convert.ToDateTime(x.StartTime).TimeOfDay })
                            .Select(y => new pivotpollutant
                            {
                                date = y.Key.Date.ToString("yyyy-MM-dd"),
                                time = y.Key.TimeOfDay.ToString("hh\\:mm"),
                                Subpollutant = y.Select(x => new SubpollutantItem
                                {
                                    pollutantname = x.Pollutantname,
                                    pollutantvalue = x.Value,
                                    verification = x.Verification == "1" ? "V" :
                                                   x.Verification == "2" ? "P" :
                                                   x.Verification == "3" ? "N" : "no data"
                                }).ToList()
                            }).ToList();

                var distinctpollutant = Final_list.Select(s => s.Pollutantname).Distinct().OrderBy(m => m).ToList();
                // Write to MemoryStream
                using (var memoryStream = new MemoryStream())
                {
                    using (var writer = new StreamWriter(memoryStream))
                    {
                //        using (var writer = new StreamWriter("PivotData.csv"))
                //{
                    writer.WriteLine(string.Format("Hourly measurement data supplied by UK-air on,{0}", DateTime.Today));
                        writer.WriteLine(string.Format("Site Name,{0}", sitename));
                        writer.WriteLine(string.Format("Site Type, {0}", siteType));
                        writer.WriteLine(string.Format("Region, {0}", region));
                        writer.WriteLine(string.Format("Latitude, {0}", latitude));
                        writer.WriteLine(string.Format("Longitude, {0}", longitude));
                        writer.WriteLine("Notes: [1] All Data GMT hour ending;  [2] Some shorthand is used, V = Verified, P = Provisionally Verified, N = Not Verified, S = Suspect, [3] Unit of measurement (for pollutants) = ugm-3, [4] Instrument type is included in 'Status' for PM10 and PM2.5");
                        // Write headers
                        writer.Write("Date,Time");
                        foreach (var pollutantname in distinctpollutant)
                        {
                            if(pollutantname == "PM10")
                            {
                                pollutantnameheaderchange = "PM10 particulate matter (Hourly measured)";
                                writer.Write($",{pollutantnameheaderchange},{"Status"}");
                            }
                            else if (pollutantname == "PM2.5")
                            {
                                pollutantnameheaderchange = "PM2.5 particulate matter (Hourly measured)";
                                writer.Write($",{pollutantnameheaderchange},{"Status"}");
                            }
                            else
                            {
                                writer.Write($",{pollutantname},{"Status"}");
                            }                                
                        }
                        writer.WriteLine();
                        // Write data
                        foreach (var item in groupedData)
                        {
                            writer.Write($"{item.date},{item.time}");

                            foreach (var pollutant in distinctpollutant)
                            {
                                var subpollutantvalue = item.Subpollutant.FirstOrDefault(s => s.pollutantname == pollutant);
                                writer.Write($",{subpollutantvalue?.pollutantvalue ?? ""},{subpollutantvalue?.verification ?? ""}");
                            }
                            writer.WriteLine();
                        }

                        writer.Flush(); // Ensure all data is written to the MemoryStream

                    // Convert MemoryStream to byte array
                    byte[] byteArray = memoryStream.ToArray();
                    //byte[] byteArray = [];

                    // Output the byte array (for demonstration purposes)
                    //Console.WriteLine(BitConverter.ToString(byteArray));
                    return byteArray;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error csv Info message {Error}", ex.Message);
                logger.LogError("Error csv Info stacktrace {Error}", ex.StackTrace);
                return new byte[] { 0x20};
            }
        }

        public void CallApi()
        {
            try
            {
                using (var client = httpClientFactory.CreateClient("Atomfeed"))
                {
                    var response = client.GetAsync("data/atom-dls/observations/auto/GB_FixedObservations_2019_CLL2.xml").Result;
                    if (response.IsSuccessStatusCode)
                    {
                        var data = response.Content.ReadAsStringAsync();
                        if(data is not null)
                        {
                            logger.LogInformation("Data Fetching health check atom feed API successful {response}", response.ToString() + DateTime.Now);                        
                        }
                    }
                    else
                    {
                        logger.LogInformation("Data Fetching health check atom feed API failed {response}", response.ToString() + DateTime.Now);
                    }
                }
            }
            catch(Exception ex)
            {
                logger.LogError("Error AtomHealthcheck message {Error}", ex.Message);
                logger.LogError("Error AtomHealthcheck stacktrace {Error}", ex.StackTrace);
            }

        }

    }
}

