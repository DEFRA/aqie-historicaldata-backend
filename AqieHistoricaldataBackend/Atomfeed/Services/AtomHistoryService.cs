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
        IAtomHourlyFetchService atomHourlyFetchService, IAtomDailyFetchService AtomDailyFetchService,
        IAWSS3BucketService AWSS3BucketService,
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
                    var dailyAverage = await AtomDailyFetchService.GetAtomDailydatafetch(finalhourlypollutantresult, data);
                    //var Daily_Average1 = finalhourlypollutantresult.GroupBy(x => new
                    //                                                {
                    //                                                    ReportDate = Convert.ToDateTime(x.StartTime).Date.ToString(),
                    //                                                    x.Pollutantname,
                    //                                                    x.Verification,
                    //                                                    x.Validity
                    //                                                })
                    //                                              .Select(x => new Finaldata
                    //                                              {
                    //                                                  ReportDate = x.Key.ReportDate,
                    //                                                  DailyPollutantname = x.Key.Pollutantname,
                    //                                                  DailyVerification = x.Key.Verification,
                    //                                                  Validity = x.Key.Validity,
                    //                                                  Total = x.Average(y => Convert.ToDecimal(y.Value == "-99" ? "0" : y.Value))
                    //                                              }).ToList();
                    //var Daily_Average = finalhourlypollutantresult                                                                    
                    //                                                .GroupBy(x => new
                    //                                                {
                    //                                                    ReportDate = Convert.ToDateTime(x.StartTime).Date.ToString(),
                    //                                                    x.Pollutantname,
                    //                                                    x.Verification
                    //                                                })
                    //                                                .Select(x => new
                    //                                                {
                    //                                                    x.Key.ReportDate,
                    //                                                    x.Key.Pollutantname,
                    //                                                    x.Key.Verification,
                    //                                                    Values = x.Where(y => y.Value != "-99").Select(y => Convert.ToDecimal(y.Value)).ToList(),
                    //                                                    Count = x.Count()
                    //                                                })
                    //                                                .Select(x => new Finaldata
                    //                                                {
                    //                                                    ReportDate = x.ReportDate,
                    //                                                    DailyPollutantname = x.Pollutantname,
                    //                                                    DailyVerification = x.Verification,
                    //                                                    Total = (x.Values.Count() >= 0.75 * x.Count) ? x.Values.Average() : 0
                    //                                                }).ToList();
                    //var Daily_Average = finalhourlypollutantresult
                    //                                                    .Where(x => new[] { "1", "2", "3", "4" }.Contains(x.Validity)) // Filter by validation
                    //                                                    .GroupBy(x => new
                    //                                                    {
                    //                                                        ReportDate = Convert.ToDateTime(x.StartTime).Date.ToString(),
                    //                                                        x.Pollutantname,
                    //                                                        x.Verification
                    //                                                    })
                    //                                                    .Select(x => new
                    //                                                    {
                    //                                                        x.Key.ReportDate,
                    //                                                        x.Key.Pollutantname,
                    //                                                        x.Key.Verification,
                    //                                                        Values = x.Select(y => Convert.ToDecimal(y.Value)).ToList(), // Include all values
                    //                                                        Count = x.Count()
                    //                                                    })
                    //                                                    .Select(x => new Finaldata
                    //                                                    {
                    //                                                        ReportDate = x.ReportDate,
                    //                                                        DailyPollutantname = x.Pollutantname,
                    //                                                        DailyVerification = x.Verification,
                    //                                                        Total = (x.Values.Count() >= 0.75 * x.Count) ? x.Values.Average() : 0
                    //                                                    })
                    //                                                    .ToList();
                    PresignedUrl = await AWSS3BucketService.writecsvtoawss3bucket(dailyAverage, data, downloadtype);                    
                }
                else if (downloadtype == "Annual")
                {
                    //To get the yearly average 
                    var Annual_Average = finalhourlypollutantresult.GroupBy(x => new 
                                                                    { 
                                                                        ReportDate = Convert.ToDateTime(x.StartTime).Year.ToString(),
                                                                        x.Pollutantname 
                                                                     })
                                                                    .Select(x => new DailyAverage 
                                                                    { 
                                                                        ReportDate = x.Key.ReportDate, 
                                                                        Pollutantname = x.Key.Pollutantname, 
                                                                        Total = x.Average(y => Convert.ToDecimal(y.Value == "-99" ? "" : y.Value )) 
                                                                    }).ToList();
                    PresignedUrl = await AWSS3BucketService.writecsvtoawss3bucket(finalhourlypollutantresult, data, downloadtype);
                }
                else
                {
                    //To get the Hourly data
                    PresignedUrl = await AWSS3BucketService.writecsvtoawss3bucket(finalhourlypollutantresult, data, downloadtype);
                }
            }
                catch (Exception ex) {
                logger.LogError("Error in Atom feed fetch {Error}", ex.Message);
                logger.LogError("Error in Atom feed fetch {Error}", ex.StackTrace);
            }
            return PresignedUrl;//PresignedUrl;//"S3 Bucket loaded Successfully";
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
      
        public async Task<dynamic> GetHistoryexceedencedata(querystringdata data)
        {
            try
            {
                var exceedancesresult = await HistoryexceedenceService.GetHistoryexceedencedata(data);
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

