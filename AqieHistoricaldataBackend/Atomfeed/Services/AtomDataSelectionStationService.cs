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
using NetTopologySuite.Noding;
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
    public class AtomDataSelectionStationService(ILogger<HistoryexceedenceService> Logger,
        IHttpClientFactory httpClientFactory,
    IAtomDataSelectionServices atomDataSelectionServices,
    IAwss3BucketService AWSS3BucketService, IAuthService AuthService,
    IMongoDbClientFactory MongoDbClientFactory) : IAtomDataSelectionStationService
    {
        private const string FailureResult = "Failure";
        // MongoDB collection for job documents
        private IMongoCollection<JobDocument>? _jobCollection;

        // In-memory queue for work items (stores minimal in-memory data; persistent state is in MongoDB)
        private readonly Channel<JobItem> _jobChannel = Channel.CreateUnbounded<JobItem>();
        private Task? _processorTask;
        private readonly object _processorLock = new();

        public async Task<object> GetAtomDataSelectionStation(AtomHistoryModel.QueryStringData queryStringData)
        {
            try
            {
                string? pollutantName = queryStringData.pollutantName;
                string? networkId = queryStringData.networkId;
                string? datasource = queryStringData.dataSource;
                string? year = queryStringData.Year;
                string? regionid = queryStringData.RegionId;
                string? region = queryStringData.Region;
                string? regiontype = queryStringData.regiontype;
                string? dataselectorfiltertype = queryStringData.dataselectorfiltertype;
                string? dataselectordownloadtype = queryStringData.dataselectordownloadtype;

                if (string.IsNullOrEmpty(pollutantName) || string.IsNullOrEmpty(year))
                {
                    Logger.LogWarning("GetAtomDataSelectionStation called with null or empty pollutantName or year.");
                    return FailureResult;
                }
                List<SiteInfo> filteredSites = new List<SiteInfo>();

                var resolvedPollutantName = await AtomSiteFilterHelper.ResolvePollutantNameAsync(pollutantName, Logger, MongoDbClientFactory);
                if (datasource == "AURN")
                {
                    var token = await AuthService.GetRicardoToken();
                    var sitemetadatainfo = await RicardoSiteMetadata.FetchSiteMetadata(httpClientFactory, token);
                    filteredSites = AtomSiteFilterHelper.FilterSitesByPollutants(sitemetadatainfo, resolvedPollutantName, Logger);
                }

                if (datasource == "NON-AURN")
                {
                    filteredSites = await AtomSiteFilterHelper.GetSiteInfoAsync(pollutantName, networkId ?? string.Empty, MongoDbClientFactory);
                }

                if (datasource == "AURN" && regionid != null)
                {
                    filteredSites = AtomSiteFilterHelper.FilterSitesByRegionId(filteredSites, regionid);
                }

                var filterpollutantyear = AtomSiteFilterHelper.FilterSitesByYearRanges(filteredSites, year);

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

                    return networkTypeCounts.Count > 0
                        ? networkTypeCounts
                        : new[] { new { NetworkType = "Unknown", Count = stationcountresult } }.ToList();
                }
                pollutantName = resolvedPollutantName;
                if (dataselectorfiltertype == "dataSelectorHourly")
                {
                    return await HandleHourlyDataSelection(stationData, pollutantName, year, queryStringData, dataselectordownloadtype ?? string.Empty);
                }

                return FailureResult;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in GetAtomDataSelectionStation");
                return FailureResult;
            }
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
                if (_jobCollection == null)
                {
                    // _jobCollection is always assigned before items reach the channel,
                    // so this path is unreachable in normal operation.
                    [ExcludeFromCodeCoverage]
                    void LogMissing() =>
                        Logger.LogError("ProcessQueueAsync MongoDB job collection is not initialized. Skipping job {JobId}", job.JobId);
                    LogMissing();
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
                    var csvData = await atomDataSelectionServices.HourlyFetch.GetAtomDataSelectionHourlyFetchService(job.StationData!, job.PollutantName!, job.Year!, job.Data!);
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

    }
}