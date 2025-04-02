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
using Hangfire.MemoryStorage.Database;
using static AqieHistoricaldataBackend.Atomfeed.Services.AtomHistoryService;


namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomHistoryService(ILogger<AtomHistoryService> logger, IHttpClientFactory httpClientFactory, 
        IAtomHourlyFetchService atomHourlyFetchService, IAWSS3BucketService AWSS3BucketService,
        IHistoryexceedenceService HistoryexceedenceService) : IAtomHistoryService //MongoService<AtomHistoryModel>, 
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
            //getpollutantcount();
            string siteId = data.siteId;
            string year = data.year;
            string PresignedUrl = string.Empty;
            string downloadfilter = data.downloadpollutant;
            string downloadtype = data.downloadpollutanttype;
            try
            {
                var finalhourlypollutantresult = await atomHourlyFetchService.GetAtomHourlydatafetch(siteId, year, downloadfilter);         

                if (downloadtype == "Daily")
                {
                    //To get the daily average 
                    var Daily_Average = finalhourlypollutantresult.GroupBy(x => new { ReportDate = Convert.ToDateTime(x.StartTime).Date.ToString(), x.Pollutantname, x.Verification })
    .Select(x => new Finaldata { ReportDate = x.Key.ReportDate, DailyPollutantname = x.Key.Pollutantname, DailyVerification = x.Key.Verification, 
                                 Total = x.Average(y => Convert.ToDecimal(y.Value == "-99" ? "0" : y.Value)) }).ToList();
                    //PresignedUrl = await writecsvtoawss3bucket(Final_list, data);
                    PresignedUrl = await AWSS3BucketService.writecsvtoawss3bucket(Daily_Average, data, downloadtype);                    
                }
                else if (downloadtype == "Annual")
                {
                    //To get the yearly average 
                    var Annual_Average = finalhourlypollutantresult.GroupBy(x => new { ReportDate = Convert.ToDateTime(x.StartTime).Year.ToString(), x.Pollutantname })
.Select(x => new DailyAverage { ReportDate = x.Key.ReportDate, Pollutantname = x.Key.Pollutantname, 
                                Total = x.Average(y => Convert.ToDecimal(y.Value == "-99" ? "" : y.Value )) }).ToList();
                    //PresignedUrl = await writecsvtoawss3bucket(Final_list, data);
                    PresignedUrl = await AWSS3BucketService.writecsvtoawss3bucket(finalhourlypollutantresult, data, downloadtype);
                }
                else
                {
                    //PresignedUrl = await writecsvtoawss3bucket(Final_list, data);
                    PresignedUrl = await AWSS3BucketService.writecsvtoawss3bucket(finalhourlypollutantresult, data, downloadtype);
                }
            }
                catch (Exception ex) {
                logger.LogError("Error in Atom feed fetch {Error}", ex.Message);
                logger.LogError("Error in Atom feed fetch {Error}", ex.StackTrace);
            }
            return PresignedUrl;//PresignedUrl;//"S3 Bucket loaded Successfully";
        }

        //public async Task<string> writecsvtoawss3bucket(dynamic Final_list, querystringdata data)
        //public async Task<string> writecsvtoawss3bucket(List<Finaldata> Final_list, querystringdata data, string downloadtype)
        //{

        //    string siteId = data.siteId;
        //    string year = data.year;
        //    string PresignedUrl = string.Empty;
        //    try
        //    {
        //        var csvbyte = atomfeedexport_csv(Final_list, data);
        //        string Region = Environment.GetEnvironmentVariable("AWS_REGION") ?? throw new ArgumentNullException("AWS_REGION");
        //        string s3BucketName = "dev-aqie-historicaldata-backend-c63f2";
        //        string s3Key = "measurement_data_" + siteId + "_" + year + ".csv";
        //        var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(Region);
        //        logger.LogInformation("S3 bucket region {regionEndpoint}", regionEndpoint);

        //        using (var s3Client = new Amazon.S3.AmazonS3Client())
        //        {
        //            using (var transferUtility = new TransferUtility(s3Client))
        //            {
        //                using (var stream = new MemoryStream(csvbyte))
        //                {
        //                    logger.LogInformation("S3 bucket upload start", DateTime.Now);
        //                    await transferUtility.UploadAsync(stream, s3BucketName, s3Key);
        //                    logger.LogInformation("S3 bucket upload end", DateTime.Now);
        //                }
        //            }
        //        }
        //        logger.LogInformation("S3 bucket PresignedUrl start", DateTime.Now);
        //        PresignedUrl = GeneratePreSignedURL(s3BucketName, s3Key, 604800);
        //        logger.LogInformation("S3 bucket PresignedUrl final URL {PresignedUrl}", PresignedUrl);
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.LogError("Error AWS S3 bucket Info message {Error}", ex.Message);
        //        logger.LogError("Error AWS S3 bucket Info stacktrace {Error}", ex.StackTrace);
        //    }
        //    return PresignedUrl;
        //}
        //public string GeneratePreSignedURL(string bucketName, string keyName, double duration)
        //{
        //    try
        //    {
        //        var s3Client = new AmazonS3Client();
        //        var request = new GetPreSignedUrlRequest
        //        {
        //            BucketName = bucketName,
        //            Key = keyName,
        //            Expires = DateTime.UtcNow.AddSeconds(duration)
        //        };

        //        string url = s3Client.GetPreSignedURL(request);
        //        return url;
        //    }
        //    catch (AmazonS3Exception ex)
        //    {               
        //        logger.LogError("AmazonS3Exception Error:{Error}", ex.Message);
        //        return "error";
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.LogError("Error in GeneratePreSignedURL Info message {Error}", ex.Message);
        //        logger.LogError("Error in GeneratePreSignedURL {Error}", ex.StackTrace);
        //        return "error";
        //    }
        //}

        //public byte[] atomfeedexport_csv(List<Finaldata> Final_list, querystringdata data)
        //{
        //    try
        //    {
        //        string pollutantnameheaderchange = string.Empty;
        //        string stationfetchdate = data.stationreaddate;
        //        string region = data.region;       
        //        string siteType = data.siteType;   
        //        string sitename = data.sitename;   
        //        string latitude = data.latitude;   
        //        string longitude = data.longitude;
        //        var groupedData = Final_list.GroupBy(x => new { Convert.ToDateTime(x.StartTime).Date, Convert.ToDateTime(x.StartTime).TimeOfDay })
        //                    .Select(y => new pivotpollutant
        //                    {
        //                        date = y.Key.Date.ToString("yyyy-MM-dd"),
        //                        time = y.Key.TimeOfDay.ToString("hh\\:mm"),
        //                        Subpollutant = y.Select(x => new SubpollutantItem
        //                        {
        //                            pollutantname = x.Pollutantname,
        //                            pollutantvalue = x.Value == "-99" ? "no data" : x.Value,
        //                            verification = x.Verification == "1" ? "V" :
        //                                           x.Verification == "2" ? "P" :
        //                                           x.Verification == "3" ? "N" : "others"
        //                        }).ToList()
        //                    }).ToList();

        //        var distinctpollutant = Final_list.Select(s => s.Pollutantname).Distinct().OrderBy(m => m).ToList();
        //        // Write to MemoryStream
        //        //using (var memoryStream = new MemoryStream())
        //        //{
        //        //    using (var writer = new StreamWriter(memoryStream))
        //        //    {
        //        using (var writer = new StreamWriter("PivotData.csv"))
        //        {
        //            writer.WriteLine(string.Format("Hourly measurement data from Defra on,{0}", stationfetchdate));
        //                writer.WriteLine(string.Format("Site Name,{0}", sitename));
        //                writer.WriteLine(string.Format("Site Type, {0}", siteType));
        //                writer.WriteLine(string.Format("Region, {0}", region));
        //                writer.WriteLine(string.Format("Latitude, {0}", latitude));
        //                writer.WriteLine(string.Format("Longitude, {0}", longitude));
        //                writer.WriteLine(string.Format("Notes:, {0}", "[1] All Data GMT hour ending;  [2] Some shorthand is used, V = Verified, P = Provisionally Verified, N = Not Verified, S = Suspect, [3] Unit of measurement (for pollutants) = ugm-3"));
        //                // Write headers
        //                writer.Write("Date,Time");
        //                foreach (var pollutantname in distinctpollutant)
        //                {
        //                    if(pollutantname == "PM10")
        //                    {
        //                        pollutantnameheaderchange = "PM10 particulate matter (Hourly measured)";
        //                        writer.Write($",{pollutantnameheaderchange},{"Status"}");
        //                    }
        //                    else if (pollutantname == "PM2.5")
        //                    {
        //                        pollutantnameheaderchange = "PM2.5 particulate matter (Hourly measured)";
        //                        writer.Write($",{pollutantnameheaderchange},{"Status"}");
        //                    }
        //                    else
        //                    {
        //                        writer.Write($",{pollutantname},{"Status"}");
        //                    }                                
        //                }
        //                writer.WriteLine();
        //                // Write data
        //                foreach (var item in groupedData)
        //                {
        //                    writer.Write($"{item.date},{item.time}");

        //                    foreach (var pollutant in distinctpollutant)
        //                    {
        //                        var subpollutantvalue = item.Subpollutant.FirstOrDefault(s => s.pollutantname == pollutant);
        //                        writer.Write($",{subpollutantvalue?.pollutantvalue ?? ""},{subpollutantvalue?.verification ?? ""}");
        //                    }
        //                    writer.WriteLine();
        //                }

        //                writer.Flush(); // Ensure all data is written to the MemoryStream

        //                // Convert MemoryStream to byte array
        //                //byte[] byteArray = memoryStream.ToArray();
        //                byte[] byteArray = [];

        //                // Output the byte array (for demonstration purposes)
        //                //Console.WriteLine(BitConverter.ToString(byteArray));
        //                return byteArray;
        //            }
        //        //}
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.LogError("Error csv Info message {Error}", ex.Message);
        //        logger.LogError("Error csv Info stacktrace {Error}", ex.StackTrace);
        //        return new byte[] { 0x20};
        //    }
        //}

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

        //public class Pollutant
        //{
        //    public string Name { get; set; }
        //    public double Value { get; set; }
        //}
        //public void getpollutantcount()
        //{
        //    //    List<Pollutant> pollutants = new List<Pollutant>
        //    //{
        //    //    new Pollutant { Name = "PM10", Value = 999.99 },
        //    //    new Pollutant { Name = "PM2.5", Value = 799.99 },
        //    //    new Pollutant { Name = "Ozone", Value = 199.99 },
        //    //    new Pollutant { Name = "PM10", Value = 210.99 },
        //    //    new Pollutant { Name = "PM2.5", Value = 450.99 }
        //    //};

        //    //    var filteredPollutants = pollutants.Where(p => (p.Name == "PM10" && p.Value > 200) || (p.Name == "PM2.5" && p.Value > 350))
        //    //                                       .GroupBy(p => p.Name)
        //    //                                       .Select(g => new { Name = g.Key, Count = g.Count() }).OrderBy(p => p.Name);

        //    //    foreach (var pollutant in filteredPollutants)
        //    //    {
        //    //        Console.WriteLine($"Pollutant: {pollutant.Name}, Count: {pollutant.Count}");
        //    //    }

        //    List<Pollutant> pollutants = new List<Pollutant>
        //{
        //    new Pollutant { Name = "PM10", Value = 999.99 },
        //    new Pollutant { Name = "PM2.5", Value = 799.99 },
        //    new Pollutant { Name = "Ozone", Value = 199.99 },
        //    new Pollutant { Name = "PM10", Value = 210.99 },
        //    new Pollutant { Name = "PM2.5", Value = 50.99 }
        //};

        //    var filteredPollutants = pollutants.Where(p =>
        //        (p.Name == "PM10" && p.Value > 200) ||
        //        (p.Name == "PM2.5" && p.Value > 350))
        //        .GroupBy(p => p.Name)
        //        .Select(g => new { Name = g.Key, Count = g.Count() })
        //        .ToList();

        //    var result = new List<string> { "PM2.5", "PM10", "Nitrogen dioxide", "Ozone", "Sulphur dioxide" }
        //        .Select(name => new
        //        {
        //            Name = name,
        //            Count = filteredPollutants.FirstOrDefault(p => p.Name == name)?.Count ?? 0
        //        });

        //    foreach (var pollutant in result)
        //    {
        //        Console.WriteLine($"Pollutant: {pollutant.Name}, Count: {pollutant.Count}");
        //    }
        //}


        public async Task<dynamic> GetHistoryexceedencedata(querystringdata data)
        {
            try
            {

                var exceedancesresult = await HistoryexceedenceService.GetHistoryexceedencedata(data);

                //string siteId = data.siteId;
                //string year = data.year;
                //string downloadfilter = "All";

                //var finalhourlypollutantresult = await atomHourlyFetchService.GetAtomHourlydatafetch(siteId, year, downloadfilter);
                ////To get the number of hourly exceedances for a selected year and selected monitoring station
                //var filteredPollutants = finalhourlypollutantresult.Where(p =>
                //                                        (p.Pollutantname == "PM10" && Convert.ToDouble(p.Value) > 200.5) || //200
                //                                        (p.Pollutantname == "PM2.5" && Convert.ToDouble(p.Value) > 350.5))  //350
                //                                        .GroupBy(p => p.Pollutantname)
                //                                        .Select(g => new { PollutantName = g.Key, Count = g.Count() })
                //                                        .ToList();

                //var hourlyexceedances = new List<string> { "PM2.5", "PM10", "Nitrogen dioxide", "Ozone", "Sulphur dioxide" }
                //    .Select(name => new
                //    {
                //        PollutantName = name,
                //        HourlyexceedancesCount = filteredPollutants.FirstOrDefault(p => p.PollutantName == name)?.Count.ToString() ?? "n/a"
                //    }).ToList();
                return exceedancesresult;
            }
            catch(Exception ex)
            {
                logger.LogError("Error in Atom Historyexceedencedata {Error}", ex.Message);
                logger.LogError("Error in Atom Historyexceedencedata {Error}", ex.StackTrace);
                return "Failure";
            }
            
        }
        }
}

