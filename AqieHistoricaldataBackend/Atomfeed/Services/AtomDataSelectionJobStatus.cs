using AqieHistoricaldataBackend.Utils.Mongo;
using MongoDB.Driver;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Services.AtomDataSelectionStationService;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionJobStatus(ILogger<HistoryexceedenceService> Logger,
    IMongoDbClientFactory MongoDbClientFactory) : IAtomDataSelectionJobStatus
    {
        private IMongoCollection<JobDocument>? _jobCollection;

        public async Task<JobInfoDto?> GetAtomDataSelectionJobStatusdata(string? jobID)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jobID)) return null;
                if (_jobCollection == null)
                {
                    _jobCollection = MongoDbClientFactory.GetCollection<JobDocument>("aqie_csvexport_jobs");
                    try
                    {
                        var indexKeys = Builders<JobDocument>.IndexKeys.Ascending(j => j.JobId);
                        await _jobCollection.Indexes.CreateOneAsync(new CreateIndexModel<JobDocument>(indexKeys));
                    }
                    catch (Exception ix)
                    {
                        Logger.LogWarning(ix, "Failed to create index on aqie_csvexport_jobs collection");
                    }
                }

                var filter = Builders<JobDocument>.Filter.Eq(d => d.JobId, jobID);
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
                Logger.LogError(ex, "Error in Atom GetAtomDataSelectionJobStatus");
                return null;
            }
        }
    }
}