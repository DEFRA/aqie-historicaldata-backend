using Amazon.S3;
using Amazon.S3.Model;
using AqieHistoricaldataBackend.Atomfeed.Models;
using AqieHistoricaldataBackend.Utils.Mongo;
using Hangfire.Common;
using Hangfire.MemoryStorage.Database;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Services.AWSS3BucketService;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAuthService
    {
        Task<string?> GetTokenAsync(string email, string password);
    }
    public class AuthService : IAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string?> GetTokenAsync(string email, string password)
        {
            var client = _httpClientFactory.CreateClient("RicardoNewAPI");

            var payload = new { email, password };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("api/login_check", content);

            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            // Check for plain string root BEFORE attempting any property access
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                return doc.RootElement.GetString();

            // Only access properties when root is an Object
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("token", out var token)) return token.GetString();
                if (doc.RootElement.TryGetProperty("access_token", out var accessToken)) return accessToken.GetString();
                if (doc.RootElement.TryGetProperty("jwt", out var jwt)) return jwt.GetString();
            }

            return null;
        }
    }
    public class AtomDataSelectionStationService(ILogger<HistoryexceedenceService> Logger,
        IHttpClientFactory httpClientFactory,
    IAtomDataSelectionStationBoundryService AtomDataSelectionStationBoundryService,
    IAtomDataSelectionLocalAuthoritiesService AtomDataSelectionLocalAuthoritiesService,
    IAtomDataSelectionHourlyFetchService AtomDataSelectionHourlyFetchService,
    IAWSS3BucketService AWSS3BucketService, IAuthService AuthService,
    IMongoDbClientFactory MongoDbClientFactory) : IAtomDataSelectionStationService
    {
        // MongoDB collection for job documents
        private IMongoCollection<JobDocument>? _jobCollection;

        // In-memory queue for work items (stores minimal in-memory data; persistent state is in MongoDB)
        private readonly Channel<JobItem> _jobChannel = Channel.CreateUnbounded<JobItem>();
        private Task? _processorTask;
        private readonly object _processorLock = new();

        // Primary constructor parameters are available as fields by the primary-constructor syntax used in this project.
        // (Logger, httpClientFactory, AtomDataSelectionStationBoundryService, AtomDataSelectionHourlyFetchService, AWSS3BucketService, AuthService)

        public async Task<string> GetAtomDataSelectionStation(string pollutantName, string datasource,
            string year, string region, string regiontype, string dataselectorfiltertype, string dataselectordownloadtype, string email)
            //string JobId)
        {

            try
            {
                var queryStringData = new AtomHistoryModel.QueryStringData
                {
                    pollutantName = pollutantName,
                    dataSource = datasource,
                    Year = year,
                    Region = region,
                    regiontype = regiontype,
                    dataselectorfiltertype = dataselectorfiltertype,
                    dataselectordownloadtype = dataselectordownloadtype,
                    email = email
                    //jobId = JobId
                };

                //For CDP
                var token = await GetRicardoToken();

                var client = httpClientFactory.CreateClient("RicardoNewAPI");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var url = "api/site_meta_datas?with-closed=true&with-pollutants=1";
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string responsebody = await response.Content.ReadAsStringAsync();

                var sitemetadatainfo = ParseSiteMeta(responsebody);

                var mappedPollutants = GetMappedPollutants(pollutantName, includeUnknowns: true);

                var filteredSites1 = sitemetadatainfo
                    .Select(site => new SiteInfo
                    {
                        SiteName = site.SiteName,
                        LocalSiteId = site.LocalSiteId,
                        AreaType = site.AreaType,
                        SiteType = site.SiteType,
                        ZoneRegion = site.ZoneRegion,
                        Latitude = site.Latitude,
                        Longitude = site.Longitude,
                        Pollutants = (site.Pollutants ?? new List<PollutantInfo>())
                            .Where(p => p.Name != null && mappedPollutants
                                .Any(tp => p.Name.Contains(tp, StringComparison.OrdinalIgnoreCase)))
                            .ToList()
                    })
                    .Where(site => site.Pollutants.Any()) // Keep only sites that have matching pollutants
                    .GroupBy(site => site.LocalSiteId)
                    .Select(g => g.First()) // Ensure distinct by LocalSiteId
                    .ToList();

                // Parse years and create year ranges once
                var yearInts = year.Split(',')
                                   .Select(y => int.TryParse(y, out int val) ? val : (int?)null)
                                   .Where(y => y.HasValue)
                                   .Select(y => y.Value)
                                   .ToList();

                var yearRanges = yearInts
                    .Select(y => new { Start = new DateTime(y, 1, 1), End = new DateTime(y, 12, 31) })
                    .ToList();

                // Filter sites based on pollutant date ranges intersecting with any year range
                var filterpollutantyear = filteredSites1
                    .Where(site =>
                        site.Pollutants != null &&
                        site.Pollutants.Any(p =>
                        {
                            if (!DateTime.TryParseExact(p.StartDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime startDate))
                                return false;

                            bool hasEndDate = DateTime.TryParseExact(p.EndDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime endDate);

                            return yearRanges.Any(range =>
                                hasEndDate
                                    ? startDate <= range.End && endDate >= range.Start
                                    : startDate <= range.End);
                        }))
                    .ToList();

                var stationData = await AtomDataSelectionStationBoundryService.GetAtomDataSelectionStationBoundryService(filterpollutantyear, region, regiontype);
                var stationcountresult = stationData.Count();

                if (dataselectorfiltertype == "dataSelectorCount")
                {
                    return stationcountresult.ToString();
                }
                else if (dataselectorfiltertype == "dataSelectorHourly")
                {
                    if (dataselectordownloadtype == "dataSelectorSingle")
                    {

                        _jobCollection = MongoDbClientFactory.GetCollection<JobDocument>("aqie_csvexport_jobs");

                        // ensure index on JobId for quick lookup
                        var indexKeys = Builders<JobDocument>.IndexKeys.Ascending(j => j.JobId);
                        _jobCollection.Indexes.CreateOne(new CreateIndexModel<JobDocument>(indexKeys));

                        // create GUID and persist job in MongoDB as Pending, then enqueue background work
                        var jobId = Guid.NewGuid().ToString("N");

                        var jobDoc = new JobDocument
                        {
                            JobId = jobId,
                            Status = JobStatusEnum.Pending,
                            StartTime = DateTime.UtcNow,
                            EndTime = null,
                            ErrorReason = null,
                            ResultUrl = null,
                            //email = email,
                            //mailSent = null,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        await _jobCollection.InsertOneAsync(jobDoc);

                        var job = new JobItem
                        {
                            JobId = jobId,
                            StationData = stationData,
                            PollutantName = pollutantName,
                            Year = year,
                            Data = queryStringData,
                            DownloadType = dataselectordownloadtype
                        };

                        //if (dataselectordownloadtype == "dataSelectorSingle")
                        //{
                        await _jobChannel.Writer.WriteAsync(job);
                        _ = EnsureQueueProcessorStartedAsync(); // fire-and-forget ensure processor running

                        // Return the job id immediately to front-end
                        return jobId;
                        //}
                    }
                    else
                    {
                        // dataselectordownloadtype == "dataSelectorMultiple"
                        // 1) generate csv bytes in background by fetching hourly data
                        Logger.LogInformation("Mail job strated generating CSV data");
                        var csvData = await AtomDataSelectionHourlyFetchService.GetAtomDataSelectionHourlyFetchService(stationData, pollutantName, year);
                        Logger.LogInformation("Mail job completed generating CSV data of count {Count}", csvData.Count);
                        // 2) write CSV to S3 and get presigned url
                        Logger.LogInformation("Mail job presigned strated writecsvtoawss3bucket");
                        var presignedUrl = await AWSS3BucketService.writecsvtoawss3bucket(csvData, queryStringData, dataselectordownloadtype);
                        Logger.LogInformation("Mail job presigned completed writecsvtoawss3bucket");
                        //var presignedUrl = "test";
                        return presignedUrl;
                    }
                }

                return "Failure";
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in GetAtomDataSelectionStation {Error}", ex.Message);
                Logger.LogError("Error in GetAtomDataSelectionStation {Error}", ex.StackTrace);
                return "Failure";
            }

        }
        private static List<SiteInfo> ParseSiteMeta(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            List<SiteInfo> siteList = new List<SiteInfo>();

            // Fix: Get the "member" property as a JsonElement, then enumerate its array items
            if (root.TryGetProperty("member", out var memberElement) && memberElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var site in memberElement.EnumerateArray())
                {
                    var siteInfo = new SiteInfo
                    {
                        SiteName = site.TryGetProperty("siteName", out var siteNameEl) ? siteNameEl.ToString() : null,
                        LocalSiteId = site.TryGetProperty("localSiteId", out var localSiteIdEl) ? localSiteIdEl.ToString() : null,
                        AreaType = site.TryGetProperty("areaType", out var areaTypeEl) ? areaTypeEl.ToString() : null,
                        SiteType = site.TryGetProperty("siteType", out var siteTypeEl) ? siteTypeEl.ToString() : null,
                        ZoneRegion = site.TryGetProperty("governmentRegion", out var zoneRegionEl) ? zoneRegionEl.ToString() : null,
                        Latitude = site.TryGetProperty("latitude", out var latitudeEl) ? latitudeEl.ToString() : null,
                        Longitude = site.TryGetProperty("longitude", out var longitudeEl) ? longitudeEl.ToString() : null,
                        Pollutants = new List<PollutantInfo>()
                    };

                    // Fix: Get "pollutantsMetaData" as a JsonElement and enumerate its properties
                    if (site.TryGetProperty("pollutantsMetaData", out var pollutantsMetaDataEl) && pollutantsMetaDataEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var pollutantProp in pollutantsMetaDataEl.EnumerateObject())
                        {
                            var data = pollutantProp.Value;
                            var pollutantInfo = new PollutantInfo
                            {
                                Name = data.TryGetProperty("name", out var nameEl) ? nameEl.ToString() : null,
                                StartDate = data.TryGetProperty("startDate", out var startDateEl) ? startDateEl.ToString() : null,
                                EndDate = data.TryGetProperty("endDate", out var endDateEl) ? endDateEl.ToString() : null
                            };
                            siteInfo.Pollutants.Add(pollutantInfo);
                        }
                    }

                    siteList.Add(siteInfo);
                }
            }

            return siteList;
        }
        private async Task<string> GetRicardoToken()
        {
            var emailFromConfig = Environment.GetEnvironmentVariable("RICARDO_API_KEY");
            var passwordFromConfig = Environment.GetEnvironmentVariable("RICARDO_API_VALUE");
            var token = await AuthService.GetTokenAsync(emailFromConfig, passwordFromConfig);
            if (string.IsNullOrEmpty(token))
            {
                Logger.LogError("GetRicardoToken Auth failed - no token returned");
                return "Failure";
            }
            return token;
        }


        private static List<string> GetMappedPollutants(string pollutantNames, bool includeUnknowns = false)
        {
            var result = new List<string>();
            var names = pollutantNames.Split(',');

            var pollutantMap = new Dictionary<string, List<string>>
            {
                { "Ozone", new List<string> { "Ozone" } },
                { "PM2.5", new List<string>
                    {
                        "PM<sub>2.5</sub> (Hourly measured)",
                        "Volatile PM<sub>2.5</sub> (Hourly measured)",
                        "Non-volatile PM<sub>2.5</sub> (Hourly measured)",
                        "PM<sub>2.5</sub> particulate matter (Hourly measured)"
                    }
                },
                { "PM10", new List<string>
                    {
                        "PM<sub>10</sub> (Hourly measured)",
                        "Volatile PM<sub>10</sub> (Hourly measured)",
                        "Non-volatile PM<sub>10</sub> (Hourly measured)",
                        "PM<sub>10</sub> particulate matter (Hourly measured)"
                    }
                },
                { "NO2", new List<string> { "Nitrogen dioxide" } },
                { "CO", new List<string> { "Carbon monoxide" } },
                { "SO2", new List<string> { "Sulphur dioxide" } },
                { "NOx", new List<string> { "Nitrogen oxides as nitrogen dioxide" } },
                { "NO", new List<string> { "Nitric oxide" } }
            };

            foreach (var name in names)
            {
                var trimmedName = name.Trim();
                if (pollutantMap.TryGetValue(trimmedName, out var mappedNames))
                {
                    result.AddRange(mappedNames); // Add all mapped values for this key
                }
                else if (includeUnknowns)
                {
                    result.Add(trimmedName); // Add raw name if not found
                }
                else
                {
                    Console.WriteLine($"Warning: Unknown pollutant '{trimmedName}' not found in map.");
                }
            }

            return result;
        }

        // Background processor start guard
        private Task EnsureQueueProcessorStartedAsync()
        {
            lock (_processorLock)
            {
                if (_processorTask is null || _processorTask.IsCompleted)
                {
                    _processorTask = Task.Run(ProcessQueueAsync);
                }
                return _processorTask;
            }
        }

        // Background processor: reads queue, updates MongoDB job document lifecycle
        private async Task ProcessQueueAsync()
        {
            await foreach (var job in _jobChannel.Reader.ReadAllAsync())
            {
                if (job == null) continue;

                if (_jobCollection == null)
                {
                    Logger.LogError("ProcessQueueAsync MongoDB job collection is not initialized. Skipping job {JobId}", job.JobId);
                    continue;
                }

                var filter = Builders<JobDocument>.Filter.Eq(d => d.JobId, job.JobId);
                var updateStart = Builders<JobDocument>.Update
                    .Set(d => d.Status, JobStatusEnum.Processing)
                    .Set(d => d.UpdatedAt, DateTime.UtcNow);
                await _jobCollection.UpdateOneAsync(filter, updateStart);

                try
                {
                    // 1) generate csv bytes in background by fetching hourly data
                    var csvData = await AtomDataSelectionHourlyFetchService.GetAtomDataSelectionHourlyFetchService(job.StationData, job.PollutantName, job.Year);
                    Logger.LogInformation("ProcessQueueAsync Background job {JobId} generated CSV data of count {Count}", job.JobId, csvData.Count);

                    // 2) write CSV to S3 and get presigned url
                    var presignedUrl = await AWSS3BucketService.writecsvtoawss3bucket(csvData, job.Data, job.DownloadType);
                    //var presignedUrl = "test";

                    // 3) update job as completed with ResultUrl
                    var updateCompleted = Builders<JobDocument>.Update
                        .Set(d => d.Status, JobStatusEnum.Completed)
                        .Set(d => d.EndTime, DateTime.UtcNow)
                        .Set(d => d.ResultUrl, presignedUrl)
                        .Set(d => d.UpdatedAt, DateTime.UtcNow);

                    await _jobCollection.UpdateOneAsync(filter, updateCompleted);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "ProcessQueueAsync Background job {JobId} failed", job.JobId);

                    var updateFailed = Builders<JobDocument>.Update
                        .Set(d => d.Status, JobStatusEnum.Failed)
                        .Set(d => d.EndTime, DateTime.UtcNow)
                        .Set(d => d.ErrorReason, ex.Message)
                        .Set(d => d.UpdatedAt, DateTime.UtcNow);

                    await _jobCollection.UpdateOneAsync(filter, updateFailed);
                }
            }
        }


    }


}