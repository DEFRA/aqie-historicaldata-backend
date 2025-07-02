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
        IAtomHourlyFetchService atomHourlyFetchService, IAtomDailyFetchService AtomDailyFetchService, IAtomAnnualFetchService AtomAnnualFetchService,
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
                    var dailyAverage =  AtomDailyFetchService.GetAtomDailydatafetch(finalhourlypollutantresult, data);

                    PresignedUrl =  AWSS3BucketService.writecsvtoawss3bucket(dailyAverage, data, downloadtype);                    
                }
                else if (downloadtype == "Annual")
                {
                    var annualAverage = await AtomAnnualFetchService.GetAtomAnnualdatafetch(finalhourlypollutantresult, data);

                    PresignedUrl =  AWSS3BucketService.writecsvtoawss3bucket(annualAverage, data, downloadtype);
                }
                else
                {
                    //To get the Hourly data
                    PresignedUrl =  AWSS3BucketService.writecsvtoawss3bucket(finalhourlypollutantresult, data, downloadtype);
                }
            }
                catch (Exception ex) {
                logger.LogError("Error in Atom feed fetch {Error}", ex.Message);
                logger.LogError("Error in Atom feed fetch {Error}", ex.StackTrace);
            }
            return PresignedUrl;
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
                        logger.LogError("Error AtomHealthcheck message {response}", response.ToString() + DateTime.Now);
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine("HTTP error occurred: " + httpEx.Message);
            }
            catch (Exception ex)
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

