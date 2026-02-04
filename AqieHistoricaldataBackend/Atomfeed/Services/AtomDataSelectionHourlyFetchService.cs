using Hangfire;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
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
                //one by one order for multiple year and multiple site
                //var result = new List<string>();
                //var yearssplit = filteryear.Split(',');
                //var stopwatch = Stopwatch.StartNew(); // Start timing
                //foreach (var singleyear in yearssplit)
                //{
                //    foreach (var siteinfo in filtered_station_pollutant)
                //    {
                //        var siteID = siteinfo.LocalSiteId;
                //        var year = singleyear;//filteryear;
                //        var pollutantsToDisplay = GetPollutantsToDisplay(pollutantName);
                //        var atomJsonCollection = await FetchAtomFeedAsync(siteID, year);
                //        var finalhourlypollutantresult = ProcessAtomData(atomJsonCollection, pollutantsToDisplay, siteinfo, year);
                //        Final_list1.AddRange(finalhourlypollutantresult);
                //    }
                //}
                ////stopwatch.Stop(); // Stop timing
                //Console.WriteLine($"Fetch and processing completed in {stopwatch.Elapsed.TotalSeconds} seconds.");

                ////// Format duration as hh:mm:ss
                //var formattedDuration = stopwatch.Elapsed.ToString(@"hh\:mm\:ss");

                ////// Log to file
                //var logFilePath = "fetch_duration_log.txt";
                //File.AppendAllText(logFilePath, $"Fetch completed at {DateTime.Now} - Duration: {formattedDuration}{Environment.NewLine}");

                //return Final_list1;

                //single year and multiple site with limit 20 concurrent requests
                //var yearssplit = filteryear.Split(',');
                //var stopwatch = Stopwatch.StartNew(); // Start timing
                //var semaphore = new SemaphoreSlim(20); // Limit to 20 concurrent tasks
                //var tasks = filtered_station_pollutant.Select(async siteinfo =>
                //{
                //    await semaphore.WaitAsync();
                //    try
                //    {
                //        var siteID = siteinfo.LocalSiteId;
                //        var year = yearssplit;
                //        var pollutantsToDisplay = GetPollutantsToDisplay(pollutantName);
                //        var atomJsonCollection = await FetchAtomFeedAsync(siteID, year);
                //        var finalhourlypollutantresult = ProcessAtomData(atomJsonCollection, pollutantsToDisplay, siteinfo, year);
                //        return finalhourlypollutantresult;
                //    }
                //    finally
                //    {
                //        semaphore.Release();
                //    }
                //});

                //var results = await Task.WhenAll(tasks);
                //foreach (var result in results)
                //{
                //    Final_list1.AddRange(result);
                //}

                //stopwatch.Stop(); // Stop timing
                //Console.WriteLine($"Fetch and processing completed in {stopwatch.Elapsed.TotalSeconds} seconds.");

                //// Format duration as hh:mm:ss
                //var formattedDuration = stopwatch.Elapsed.ToString(@"hh\:mm\:ss");

                //// Log to file
                //var logFilePath = "fetch_duration_log.txt";
                //File.AppendAllText(logFilePath, $"Fetch completed at {DateTime.Now} - Duration: {formattedDuration}{Environment.NewLine}");

                //mutliple year and multiple site with limit 20 concurrent requests with foreach
                //var result = new List<string>();
                //var yearssplit = filteryear.Split(',');
                //var stopwatch = Stopwatch.StartNew(); // Start timing
                //var semaphore = new SemaphoreSlim(20); // Limit to 20 concurrent tasks

                //var tasks = new List<Task<List<FinalData>>>();
                //var pollutantsToDisplay = GetPollutantsToDisplay(pollutantName);
                //foreach (var singleyear in yearssplit)
                //{
                //    foreach (var siteinfo in filtered_station_pollutant)
                //    {
                //        tasks.Add(Task.Run(async () =>
                //        {
                //            await semaphore.WaitAsync();
                //            try
                //            {
                //                var siteID = siteinfo.LocalSiteId;
                //                var year = singleyear;
                //                var atomJsonCollection = await FetchAtomFeedAsync(siteID, year);
                //                var finalhourlypollutantresult = ProcessAtomData(atomJsonCollection, pollutantsToDisplay, siteinfo, year);
                //                return finalhourlypollutantresult;
                //            }
                //            finally
                //            {
                //                semaphore.Release();
                //            }
                //        }));
                //    }
                //}

                //var results = await Task.WhenAll(tasks);
                //foreach (var result in results)
                //{
                //    Final_list1.AddRange(result);
                //}

                //stopwatch.Stop(); // Stop timing
                //Console.WriteLine($"Fetch and processing completed in {stopwatch.Elapsed.TotalSeconds} seconds.");

                //// Format duration as hh:mm:ss
                //var formattedDuration = stopwatch.Elapsed.ToString(@"hh\:mm\:ss");

                //// Log to file
                //var logFilePath = "fetch_duration_log.txt";
                //File.AppendAllText(logFilePath, $"Fetch completed at {DateTime.Now} - Duration: {formattedDuration}{Environment.NewLine}");

                //mulitple year and multiple site with limit 20 concurrent requests with linq
                //var years = filteryear.Split(',');
                //var stopwatch = Stopwatch.StartNew();
                //var semaphore = new SemaphoreSlim(20);
                //var pollutantsToDisplay = GetPollutantsToDisplay(pollutantName);

                //var tasks = filtered_station_pollutant.SelectMany(siteinfo =>
                //    years.Select(async year =>
                //    {
                //        await semaphore.WaitAsync();
                //        try
                //        {
                //            var siteID = siteinfo.LocalSiteId;
                //            var atomJsonCollection = await FetchAtomFeedAsync(siteID, year);
                //            var finalhourlypollutantresult = ProcessAtomData(atomJsonCollection, pollutantsToDisplay, siteinfo, year);
                //            return finalhourlypollutantresult;
                //        }
                //        catch (Exception ex)
                //        {
                //            // Log the error with context
                //            var errorMessage = $"Error processing site {siteinfo.LocalSiteId} for year {year}: {ex.Message}";
                //            Console.WriteLine(errorMessage);
                //            File.AppendAllText("error_log.txt", $"{DateTime.Now}: {errorMessage}{Environment.NewLine}");
                //            return new List<FinalData>(); // Return empty result to avoid breaking the flow
                //        }
                //        finally
                //        {
                //            semaphore.Release();
                //        }
                //    })
                //);

                //var results = await Task.WhenAll(tasks);
                //foreach (var result in results)
                //{
                //    Final_list1.AddRange(result);
                //}

                //stopwatch.Stop();
                //Console.WriteLine($"Fetch and processing completed in {stopwatch.Elapsed.TotalSeconds} seconds.");

                //var formattedDuration = stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
                //File.AppendAllText("fetch_duration_log.txt", $"Fetch completed at {DateTime.Now} - Duration: {formattedDuration}{Environment.NewLine}");

                //mulitple year and multiple site with limit 20 concurrent requests with parllel foreach
                //var years = filteryear.Split(',');
                //var stopwatch = Stopwatch.StartNew();
                //var pollutantsToDisplay = GetPollutantsToDisplay(pollutantName);
                //var resultsBag = new ConcurrentBag<FinalData>();
                //await Parallel.ForEachAsync(filtered_station_pollutant, new ParallelOptions { MaxDegreeOfParallelism = 20 }, async (siteinfo, _) =>
                //{
                //    foreach (var year in years)
                //    {
                //        try
                //        {
                //            var atomJsonCollection = await FetchAtomFeedAsync(siteinfo.LocalSiteId, year);
                //            var result = ProcessAtomData(atomJsonCollection, pollutantsToDisplay, siteinfo, year);
                //            foreach (var item in result)
                //                resultsBag.Add(item);
                //        }
                //        catch (Exception ex)
                //        {
                //            var errorMessage = $"Error processing site {siteinfo.LocalSiteId} for year {year}: {ex.Message}";
                //            Console.WriteLine(errorMessage);
                //            File.AppendAllTextAsync("error_log.txt", $"{DateTime.Now}: {errorMessage}{Environment.NewLine}");
                //        }
                //    }
                //});
                //Final_list1.AddRange(resultsBag);

                //stopwatch.Stop();
                //Console.WriteLine($"Fetch and processing completed in {stopwatch.Elapsed.TotalSeconds} seconds.");

                //var formattedDuration = stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
                //File.AppendAllText("fetch_duration_log.txt", $"Fetch completed at {DateTime.Now} - Duration: {formattedDuration}{Environment.NewLine}");

                //mulitple year and multiple site with limit 20 concurrent requests with parllel foreach more optimised code
                var years = filteryear.Split(',');
                var stopwatch = Stopwatch.StartNew();
                Logger.LogInformation("Fetch and processing started in {ElapsedSeconds} seconds.", stopwatch.Elapsed.TotalSeconds);
                var pollutantsToDisplay = GetPollutantsToDisplay(pollutantName);
                var resultsBag = new ConcurrentBag<FinalData>();
                var siteYearPairs = filtered_station_pollutant
                                    .SelectMany(siteinfo => years.Select(year => new { siteinfo, year }));
                await Parallel.ForEachAsync(siteYearPairs, new ParallelOptions { MaxDegreeOfParallelism = 5 }, async (pair, _) =>
                {
                        try
                        {
                            var atomJsonCollection = await FetchAtomFeedAsync(pair.siteinfo.LocalSiteId, pair.year);
                            var result = ProcessAtomData(atomJsonCollection, pollutantsToDisplay, pair.siteinfo, pair.year);
                            foreach (var item in result)
                                resultsBag.Add(item);
                        }
                        catch (Exception ex)
                        {
                            var errorMessage = $"Error processing site {pair.siteinfo.LocalSiteId} for year {pair.year}: {ex.Message}";
                            Console.WriteLine(errorMessage);
                            Logger.LogError("Error processing site {SiteID} for year {Year}: {Error}", pair.siteinfo.LocalSiteId, pair.year, ex.Message);
                            File.AppendAllTextAsync("error_log.txt", $"{DateTime.Now}: {errorMessage}{Environment.NewLine}");
                        }                    
                });
                Final_list1.AddRange(resultsBag);


                stopwatch.Stop();
                Console.WriteLine($"Fetch and processing completed in {stopwatch.Elapsed.TotalSeconds} seconds.");
                Logger.LogInformation("Fetch and processing completed in {ElapsedSeconds} seconds.", stopwatch.Elapsed.TotalSeconds);

                var formattedDuration = stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
                File.AppendAllText("fetch_duration_log.txt", $"Fetch completed at {DateTime.Now} - Duration: {formattedDuration}{Environment.NewLine}");


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
            // 1. Null/empty check
            if (string.IsNullOrWhiteSpace(siteID) || string.IsNullOrWhiteSpace(year))
            {
                Logger.LogWarning("Invalid FetchAtomFeedAsync siteID or year: siteID='{SiteID}', year='{Year}'", siteID, year);
                return new JArray();
            }
            var client = httpClientFactory.CreateClient("Atomfeed");
            var url = $"data/atom-dls/observations/auto/GB_FixedObservations_{year}_{siteID}.xml";
            try
            {               
                var response = await client.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Logger.LogWarning("Atom feed not found (404) for URL: {Url} (siteID: {SiteID}, year: {Year})", url, siteID, year);
                    return new JArray();
                }
                response.EnsureSuccessStatusCode();
                var stream = await response.Content.ReadAsStreamAsync();
                var xml = new XmlDocument();
                xml.Load(stream);
                var json = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xml);
                return JObject.Parse(json)["gml:FeatureCollection"]["gml:featureMember"] as JArray;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError("HTTP error FetchAtomFeedAsync fetching Atom feed for URL: {Url} (siteID: {SiteID}, year: {Year}): {Error}", url, siteID, year, ex.Message);
                return new JArray();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error FetchAtomFeedAsync fetching Atom feed for URL: {Url} (siteID: {SiteID}, year: {Year}): {Error}", url, siteID, year, ex);
                return new JArray();
            }
        }

        //private List<PollutantDetails> GetPollutantsToDisplay(string filter)
        //{
        //    var allPollutants = new List<PollutantDetails>
        //    {
        //        new PollutantDetails { PollutantName = "Nitrogen dioxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/8" },
        //        new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" },
        //        new PollutantDetails { PollutantName = "PM2.5", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/6001" },
        //        new PollutantDetails { PollutantName = "Ozone", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/7" },
        //        new PollutantDetails { PollutantName = "Sulphur dioxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/1" }
        //    };

        //    var filtered = allPollutants.Where(p => p.PollutantName == filter);
        //    return filtered.Any() ? filtered.ToList() : allPollutants;
        //}

        private List<PollutantDetails> GetPollutantsToDisplay(string filter)
        {
            var allPollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "Nitrogen dioxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/8" },
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" },
                new PollutantDetails { PollutantName = "PM2.5", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/6001" },
                new PollutantDetails { PollutantName = "Ozone", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/7" },
                new PollutantDetails { PollutantName = "Sulphur dioxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/1" },
                new PollutantDetails { PollutantName = "Nitrogen oxides as nitrogen dioxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/9" },
                new PollutantDetails { PollutantName = "Carbon monoxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/10" },
                new PollutantDetails { PollutantName = "Nitric oxide", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/38" }

            };

                    // Split and normalize the filter string
                    var filterList = filter.Split(',')
                                           .Select(f => f.Trim())
                                           .ToList();

                    // Filter using case-insensitive comparison
                    var filtered = allPollutants
                        .Where(p => filterList.Contains(p.PollutantName, StringComparer.OrdinalIgnoreCase))
                        .ToList();

                    // Return filtered list if any match, otherwise return all
                    return filtered.Any() ? filtered : allPollutants;
        }

        private List<FinalData> ProcessAtomData(JArray features, List<PollutantDetails> pollutants, SiteInfo siteinfo, string year)
            {
                var finalList = new List<FinalData>();

                if (features == null || features.Count == 0)
                    return new List<FinalData>();

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
                        Region = siteinfo.ZoneRegion,
                        Country = siteinfo.Country
                    }).ToList();
            }
    }
}
