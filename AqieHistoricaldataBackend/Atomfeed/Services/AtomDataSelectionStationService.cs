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
using static AqieHistoricaldataBackend.Atomfeed.Services.Awss3BucketService;
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
    IAtomDataSelectionServices atomDataSelectionServices,
    IAwss3BucketService AWSS3BucketService, IAuthService AuthService,
    IMongoDbClientFactory MongoDbClientFactory) : IAtomDataSelectionStationService
    {
        // MongoDB collection for job documents
        private IMongoCollection<JobDocument>? _jobCollection;

        // In-memory queue for work items (stores minimal in-memory data; persistent state is in MongoDB)
        private readonly Channel<JobItem> _jobChannel = Channel.CreateUnbounded<JobItem>();
        private Task? _processorTask;
        private readonly object _processorLock = new();

        // Primary constructor parameters are available as fields by the primary-constructor syntax used in this project.
        // (Logger, httpClientFactory, atomDataSelectionServices, AWSS3BucketService, AuthService)

        public async Task<string> GetAtomDataSelectionStation(string? pollutantName, string? datasource, string? year, string? region, string? regiontype, string? dataselectorfiltertype, string? dataselectordownloadtype, string? email)
        {
            try
            {
                if (string.IsNullOrEmpty(pollutantName) || string.IsNullOrEmpty(year))
                {
                    Logger.LogWarning("GetAtomDataSelectionStation called with null or empty pollutantName or year.");
                    return "Failure";
                }
                List<SiteInfo> filteredSites = new List<SiteInfo>();
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
                };

                var resolvedPollutantName = await ResolvePollutantNameAsync(pollutantName);
                if (datasource == "AURN")
                {
                    var token = await GetRicardoToken();
                    var sitemetadatainfo = await FetchSiteMetadata(token);
                    filteredSites = FilterSitesByPollutants(sitemetadatainfo, resolvedPollutantName, Logger);
                }

                if (datasource == "NON-AURN")
                {
                    filteredSites = await GetSiteInfoAsync(pollutantName);
                }

                var filterpollutantyear = FilterSitesByYearRanges(filteredSites, year);

                var stationData = await atomDataSelectionServices.StationBoundry.GetAtomDataSelectionStationBoundryService(
                    filterpollutantyear,
                    region ?? string.Empty,
                    regiontype ?? string.Empty);
                var stationcountresult = stationData.Count;

                if (dataselectorfiltertype == "dataSelectorCount" && datasource == "AURN")
                {
                    return stationcountresult.ToString();
                }
                if (dataselectorfiltertype == "dataSelectorCount" && datasource == "NON-AURN")
                {
                    var networkTypeCounts = stationData
                        .GroupBy(s => s.NetworkType ?? "Unknown")
                        .Select(g => new
                        {
                            NetworkType = g.Key,
                            Count = g.Count()
                        })
                        .ToList();

                    //return networkTypeCounts.Any()
                    //    ? string.Join(", ",
                    //        networkTypeCounts.Select(nc =>
                    //            $"{{NetworkType:\"{nc.NetworkType}\",Count:\"{nc.Count}\"}}"))
                    //    : $"{{NetworkType:\"Unknown\",Count:\"{stationcountresult}\"}}";

                    return networkTypeCounts.Any()
                            ? string.Join(", ", networkTypeCounts.Select(nc => $"NetworkType:\"{nc.NetworkType}\",Count:\"{nc.Count}\""))
                            : $"NetworkType:\"Unknown\",Count:\"{stationcountresult}\"";
                    //return type as "UKEAP - Rural NO2 Network: 11"
                    //return networkTypeCounts.Any()
                    //    ? string.Join(", ", networkTypeCounts.Select(nc => $"{nc.NetworkType}: {nc.Count}"))
                    //    : "Unknown: " + stationcountresult.ToString();
                    //return stationcountresult.ToString();
                }
                pollutantName = resolvedPollutantName;
                if (dataselectorfiltertype == "dataSelectorHourly")
                {
                    return await HandleHourlyDataSelection(stationData, pollutantName, year, queryStringData, dataselectordownloadtype ?? string.Empty);
                }

                return "Failure";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in GetAtomDataSelectionStation");
                return "Failure";
            }
        }

        private async Task<List<SiteInfo>> FetchSiteMetadata(string token)
        {
            var client = httpClientFactory.CreateClient("RicardoNewAPI");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = "api/site_meta_datas?with-closed=true&with-pollutants=1";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string responsebody = await response.Content.ReadAsStringAsync();
            return ParseSiteMeta(responsebody);
        }

        private static List<SiteInfo> FilterSitesByPollutants(List<SiteInfo> sites, string pollutantName, ILogger logger)
        {
            var mappedPollutants = GetMappedPollutants(pollutantName, logger, includeUnknowns: true);

            return sites
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
                .Where(site => site.Pollutants?.Count > 0)
                .GroupBy(site => site.LocalSiteId)
                .Select(g => g.First())
                .ToList();
        }

        private static List<SiteInfo> FilterSitesByYearRanges(List<SiteInfo> sites, string year)
        {
            var yearRanges = ParseYearRanges(year);

            return sites
                .Where(site =>
                    site.Pollutants != null &&
                    site.Pollutants.Any(p => IsPollutantInYearRange(p, yearRanges)))
                .ToList();
        }

        private static List<(DateTime Start, DateTime End)> ParseYearRanges(string year)
        {
            var yearInts = year.Split(',')
                .Select(y => int.TryParse(y, out int val) ? val : (int?)null)
                .Where(y => y.HasValue)
                .Select(y => y!.Value)
                .ToList();

            return yearInts
                .Select(y => (
                    Start: new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    End: new DateTime(y, 12, 31, 0, 0, 0, DateTimeKind.Utc)))
                .ToList();
        }

        private static bool IsPollutantInYearRange(PollutantInfo pollutant, List<(DateTime Start, DateTime End)> yearRanges)
        {
            if (!DateTime.TryParseExact(pollutant.StartDate, "dd/MM/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime startDate))
                return false;

            bool hasEndDate = DateTime.TryParseExact(pollutant.EndDate, "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime endDate);

            return yearRanges.Any(range =>
                hasEndDate
                    ? startDate <= range.End && endDate >= range.Start
                    : startDate <= range.End);
        }

        private async Task<string> HandleHourlyDataSelection(List<SiteInfo> stationData, string pollutantName,
            string year, QueryStringData queryStringData, string dataselectordownloadtype)
        {
            if (dataselectordownloadtype == "dataSelectorSingle")
            {
                return await CreateAndEnqueueJob(stationData, pollutantName, year, queryStringData, dataselectordownloadtype);
            }

            return await ProcessEmailDownload(stationData, pollutantName, year, queryStringData, dataselectordownloadtype);
        }

        private async Task<string> CreateAndEnqueueJob(List<SiteInfo> stationData, string pollutantName,
            string year, QueryStringData queryStringData, string dataselectordownloadtype)
        {
            _jobCollection = MongoDbClientFactory.GetCollection<JobDocument>("aqie_csvexport_jobs");

            var indexKeys = Builders<JobDocument>.IndexKeys.Ascending(j => j.JobId);
            await _jobCollection.Indexes.CreateOneAsync(new CreateIndexModel<JobDocument>(indexKeys));

            var jobId = Guid.NewGuid().ToString("N");

            var jobDoc = new JobDocument
            {
                JobId = jobId,
                Status = JobStatusEnum.Pending,
                StartTime = DateTime.UtcNow,
                EndTime = null,
                ErrorReason = null,
                ResultUrl = null,
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

            await _jobChannel.Writer.WriteAsync(job);
            _ = EnsureQueueProcessorStartedAsync();

            return jobId;
        }

        private async Task<string> ProcessEmailDownload(List<SiteInfo> stationData, string pollutantName,
            string year, QueryStringData queryStringData, string dataselectordownloadtype)
        {
            Logger.LogInformation("Mail job strated generating CSV data");
            var csvData = await atomDataSelectionServices.HourlyFetch.GetAtomDataSelectionHourlyFetchService(stationData, pollutantName, year, queryStringData);
            Logger.LogInformation("Mail job completed generating CSV data of count {Count}", csvData.Count);

            Logger.LogInformation("Mail job presigned strated WriteCsvToAwsS3BucketAsync");
            var presignedUrl = await AWSS3BucketService.WriteCsvToAwsS3BucketAsync(csvData, queryStringData, dataselectordownloadtype);
            Logger.LogInformation("Mail job presigned completed WriteCsvToAwsS3BucketAsync");

            return presignedUrl;
        }
        private static List<SiteInfo> ParseSiteMeta(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            List<SiteInfo> siteList = new List<SiteInfo>();

            if (root.TryGetProperty("member", out var memberElement) && memberElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var site in memberElement.EnumerateArray())
                {
                    var siteInfo = ParseSingleSite(site);
                    siteList.Add(siteInfo);
                }
            }

            return siteList;
        }

        private static SiteInfo ParseSingleSite(JsonElement site)
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
                Pollutants = ParsePollutantsMetaData(site)
            };

            return siteInfo;
        }

        private static List<PollutantInfo> ParsePollutantsMetaData(JsonElement site)
        {
            var pollutants = new List<PollutantInfo>();

            if (site.TryGetProperty("pollutantsMetaData", out var pollutantsMetaDataEl) && pollutantsMetaDataEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var pollutantProp in pollutantsMetaDataEl.EnumerateObject())
                {
                    var pollutantInfo = ParseSinglePollutant(pollutantProp.Value);
                    pollutants.Add(pollutantInfo);
                }
            }

            return pollutants;
        }

        private static PollutantInfo ParseSinglePollutant(JsonElement data)
        {
            return new PollutantInfo
            {
                Name = data.TryGetProperty("name", out var nameEl) ? nameEl.ToString() : null,
                StartDate = data.TryGetProperty("startDate", out var startDateEl) ? startDateEl.ToString() : null,
                EndDate = data.TryGetProperty("endDate", out var endDateEl) ? endDateEl.ToString() : null
            };
        }

        private async Task<string> GetRicardoToken()
        {
            var emailFromConfig = Environment.GetEnvironmentVariable("RICARDO_API_KEY");
            var passwordFromConfig = Environment.GetEnvironmentVariable("RICARDO_API_VALUE");

            if (string.IsNullOrEmpty(emailFromConfig) || string.IsNullOrEmpty(passwordFromConfig))
            {
                Logger.LogError("GetRicardoToken Auth failed - credentials not found in environment variables");
                return "Failure";
            }

            var token = await AuthService.GetTokenAsync(emailFromConfig, passwordFromConfig);
            if (string.IsNullOrEmpty(token))
            {
                Logger.LogError("GetRicardoToken Auth failed - no token returned");
                return "Failure";
            }
            return token;
        }


        private static List<string> GetMappedPollutants(string pollutantName, ILogger logger, bool includeUnknowns = false)
        {
            var result = new List<string>();
            var names = pollutantName.Split(',');

            var pollutantMap = new Dictionary<string, List<string>>
            {
                { "Ozone", new List<string> { "Ozone" } },
                { "Fine particulate matter", new List<string>
                    {
                        "PM<sub>2.5</sub> (Hourly measured)",
                        "Volatile PM<sub>2.5</sub> (Hourly measured)",
                        "Non-volatile PM<sub>2.5</sub> (Hourly measured)",
                        "PM<sub>2.5</sub> particulate matter (Hourly measured)"
                    }
                },
                { "Particulate matter", new List<string>
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
                    result.AddRange(mappedNames);
                }
                else if (includeUnknowns)
                {
                    result.Add(trimmedName);
                }
                else
                {
                    logger.LogWarning("Unknown pollutant '{PollutantName}' not found in map.", trimmedName);
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
                    var csvData = await atomDataSelectionServices.HourlyFetch.GetAtomDataSelectionHourlyFetchService(job.StationData!, job.PollutantName!, job.Year!,job.Data!);
                    Logger.LogInformation("ProcessQueueAsync Background job {JobId} generated CSV data of count {Count}", job.JobId, csvData.Count);

                    // 2) write CSV to S3 and get presigned url
                    var presignedUrl = await AWSS3BucketService.WriteCsvToAwsS3BucketAsync(csvData, job.Data!, job.DownloadType!);

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
        private static (string? AreaType, string? SiteType) SplitEnvironmentType(string? environmentType)
        {
            if (string.IsNullOrWhiteSpace(environmentType))
                return (null, null);

            var lastSpace = environmentType.LastIndexOf(' ');
            if (lastSpace < 0)
                return (null, environmentType.Trim()); // single word — treat as SiteType only

            return (
                environmentType[..lastSpace].Trim(),
                environmentType[lastSpace..].Trim()
            );
        }

        public async Task<string> ResolvePollutantNameAsync(string pollutantName)
        {
            // Parse the comma-separated pollutant IDs: "44,40,36,46,47" → ["44","40","36","46","47"]
            var pollutantIds = pollutantName
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var pollutantCollection = MongoDbClientFactory.GetCollection<PollutantMasterDocument>(
                "aqie_atom_non_aurn_networks_pollutant_master");

            // Filter documents whose pollutantID is in the provided list
            var idFilter = Builders<PollutantMasterDocument>.Filter
                .In(d => d.pollutantID, pollutantIds);

            var matchedPollutants = await pollutantCollection.Find(idFilter).ToListAsync();

            // Extract the resolved pollutant names as a comma-separated string
            var resolvedPollutantName = string.Join(",",
                matchedPollutants
                    .Where(p => !string.IsNullOrEmpty(p.pollutantName))
                    .Select(p => p.pollutantName!));

            Logger.LogInformation("Resolved pollutant names: {Names}", resolvedPollutantName);
            return resolvedPollutantName;
        }

        public async Task<List<SiteInfo>> GetSiteInfoAsync(string pollutantName)
        {
            // Implementation for fetching site info
            var siteCollection = MongoDbClientFactory.GetCollection<StationDetailDocument>("aqie_atom_non_aurn_networks_station_details");

            var pollutantIds = pollutantName
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var pollutantFilter = Builders<StationDetailDocument>.Filter
                .In(d => d.pollutantID, pollutantIds);

            var documents = await siteCollection.Find(pollutantFilter).ToListAsync();

            var filteredSites = documents
                        .GroupBy(d => d.SiteID)
                        .Select(g =>
                        {
                            var first = g.First();
                            var (areaType, siteType) = SplitEnvironmentType(first.EnvironmentType);
                            return new SiteInfo
                            {
                                LocalSiteId = first.SiteID,
                                SiteName = first.SiteName,
                                AreaType = areaType,
                                SiteType = siteType,
                                Latitude = first.Latitude,
                                Longitude = first.Longitude,
                                NetworkType = first.NetworkType,
                                Pollutants = g
                                    .Where(d => d.PollutantName != null)
                                    .Select(d => new PollutantInfo
                                    {
                                        Name = d.PollutantName,
                                        StartDate = d.StartDate,
                                        EndDate = d.EndDate
                                    })
                                    .ToList()
                            };
                        })
                        .ToList();
            return filteredSites;
        }
    }


}