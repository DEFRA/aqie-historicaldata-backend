using AqieHistoricaldataBackend.Utils.Mongo;
using MongoDB.Driver;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Services.AtomDataSelectionStationService;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    //public sealed record JobInfoDto
    //{
    //    public string JobId { get; init; } = string.Empty;
    //    public string Status { get; init; } = string.Empty;
    //    public string? ResultUrl { get; init; }
    //    public string? ErrorReason { get; init; }
    //    public DateTime? CreatedAt { get; init; }
    //    public DateTime? UpdatedAt { get; init; }
    //    public DateTime? StartTime { get; init; }
    //    public DateTime? EndTime { get; init; }
    //}
    public class AtomDataSelectionJobStatus(ILogger<HistoryexceedenceService> Logger,
    IAtomHourlyFetchService AtomHourlyFetchService,
    IAtomDataSelectionStationService AtomDataSelectionStationService,
    IMongoDbClientFactory MongoDbClientFactory) : IAtomDataSelectionJobStatus
    {
        // MongoDB collection for job documents
        private IMongoCollection<JobDocument>? _jobCollection;
        public async Task<JobInfoDto?> GetAtomDataSelectionJobStatusdata(string jobId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jobId)) return null;
                // Lazy-initialize the collection using the injected factory so _jobCollection won't be null
                if (_jobCollection == null)
                {
                    _jobCollection = MongoDbClientFactory.GetCollection<JobDocument>("aqie_csvexport_jobs");

                    // Ensure an index exists for quick lookups by JobId (no-op if it already exists)
                    try
                    {
                        var indexKeys = Builders<JobDocument>.IndexKeys.Ascending(j => j.JobId);
                        _jobCollection.Indexes.CreateOne(new CreateIndexModel<JobDocument>(indexKeys));
                    }
                    catch (Exception ix)
                    {
                        // Log index creation failure but continue — reads can still function
                        Logger.LogWarning(ix, "Failed to create index on aqie_csvexport_jobs collection");
                    }
                }

                var filter = Builders<JobDocument>.Filter.Eq(d => d.JobId, jobId);
                var doc = await _jobCollection.Find(filter).FirstOrDefaultAsync();
                if (doc == null) return null;

                return new JobInfoDto
                {
                    JobId = doc.JobId,
                    Status = doc.Status.ToString(),
                    ResultUrl = doc.ResultUrl,
                    ErrorReason = doc.ErrorReason,
                    CreatedAt = doc.CreatedAt,
                    UpdatedAt = doc.UpdatedAt,
                    StartTime = doc.StartTime,
                    EndTime = doc.EndTime
                };
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in Atom GetAtomDataSelectionJobStatus {Error}", ex.Message);
                Logger.LogError("Error in Atom GetAtomDataSelectionJobStatus {Error}", ex.StackTrace);
                return null;
            }
        }
    }
}