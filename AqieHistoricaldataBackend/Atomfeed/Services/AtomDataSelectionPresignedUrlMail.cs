using Amazon.S3;
using Amazon.S3.Model;
using AqieHistoricaldataBackend.Utils.Mongo;
using Hangfire.Common;
using Hangfire.MemoryStorage.Database;
using MongoDB.Driver;
using System.Globalization;
using System.Text.RegularExpressions;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Services.AtomDataSelectionPresignedUrlMail;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionPresignedUrlMail(
                                        ILogger<HistoryexceedenceService> Logger,
                                        IMongoDbClientFactory MongoDbClientFactory,
                                        IAwsPreSignedUrLService AwsPreSignedUrLService
                                        ) : IAtomDataSelectionPresignedUrlMail
    {
        private IMongoCollection<eMailJobDocument>? _jobCollection;

        public async Task<string?> GetPresignedUrlMail(string jobId)
        {
            try
            {
                Logger.LogInformation("GetPresignedUrlMail method started for JobId: {JobId}", jobId);
                if (string.IsNullOrWhiteSpace(jobId)) return null;
                if (_jobCollection == null)
                {
                    _jobCollection = MongoDbClientFactory.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs");
                    try
                    {
                        var indexKeys = Builders<eMailJobDocument>.IndexKeys.Ascending(j => j.JobId);
                        await _jobCollection.Indexes.CreateOneAsync(new CreateIndexModel<eMailJobDocument>(indexKeys));
                    }
                    catch (Exception ix)
                    {
                        Logger.LogWarning(ix, "Failed to create index on aqie_csvexport_jobs collection");
                    }
                }

                var filter = Builders<eMailJobDocument>.Filter.Eq(d => d.JobId, jobId);

                // Use FindAsync directly so the injected IMongoCollection mock is invoked
                // unambiguously, avoiding extension-method indirection that bypasses the mock.
                eMailJobDocument? doc;
                using (var cursor = await _jobCollection.FindAsync(filter))
                {
                    doc = await cursor.FirstOrDefaultAsync();
                }

                if (doc == null) return null;

                Logger.LogInformation("Document CreatedAt value: {CreatedAt}", doc.CreatedAt);

                string s3Key = $"{doc.DataSource}_{doc.PollutantName}_{doc.Region}_{doc.Year}.zip";
                string? resultUrl = await GetS3data(s3Key);
                if (resultUrl == null) return null;

                var update = Builders<eMailJobDocument>.Update
                            .Set(j => j.ResultUrl, resultUrl);
                await _jobCollection.UpdateOneAsync(
                    Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, doc.JobId),
                    update
                );
                return resultUrl;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in GetPresignedUrlMail for JobId: {JobId}", jobId);

                if (_jobCollection != null)
                {
                    var update = Builders<eMailJobDocument>.Update
                                .Set(j => j.ResultUrl, null)
                                .Set(j => j.Status, JobStatusEnum.Failed)
                                .Set(j => j.MailSent, null)
                                .Set(j => j.ErrorReason, ex.Message);
                    await _jobCollection.UpdateOneAsync(
                        Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, jobId),
                        update
                    );
                }
                return null;
            }
        }

        private async Task<string?> GetS3data(string s3Key)
        {
            string? bucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME");
            Logger.LogInformation("Fetching S3 object. Bucket: {BucketName}, Key: {S3Key}", bucketName, s3Key);

            try
            {
                Logger.LogInformation("S3 bucket GetPresignedUrlMail start {Datetime}", DateTime.Now);
                var url = await AwsPreSignedUrLService.GeneratePreSignedURL(bucketName, s3Key, 604800);
                Logger.LogInformation("S3 bucket GetPresignedUrlMail final URL {PresignedUrl}", url);
                return url;
            }
            catch (AmazonS3Exception s3Ex)
            {
                Logger.LogError(s3Ex, "S3 error retrieving object. Bucket: {BucketName}, Key: {S3Key}, ErrorCode: {ErrorCode}",
                    bucketName, s3Key, s3Ex.ErrorCode);
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error retrieving S3 object. Bucket: {BucketName}, Key: {S3Key}",
                    bucketName, s3Key);
                return null;
            }
        }
    }
}
