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
                                        IHttpClientFactory httpClientFactory,
                                        IAWSPreSignedURLService awsPreSignedURLService,
                                        IAmazonS3 amazonS3
                                        ) : IAtomDataSelectionPresignedUrlMail
    {
        //private readonly IEmailService _emailService = emailService;
        //private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        // MongoDB collection for job documents
        private IMongoCollection<eMailJobDocument>? _jobCollection;
        public async Task<string> GetPresignedUrlMail(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return null;
            // Lazy-initialize the collection using the injected factory so _jobCollection won't be null
            if (_jobCollection == null)
            {
                _jobCollection = MongoDbClientFactory.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs");

                // Ensure an index exists for quick lookups by JobId (no-op if it already exists)
                try
                {
                    var indexKeys = Builders<eMailJobDocument>.IndexKeys.Ascending(j => j.JobId);
                    _jobCollection.Indexes.CreateOne(new CreateIndexModel<eMailJobDocument>(indexKeys));
                }
                catch (Exception ix)
                {
                    // Log index creation failure but continue — reads can still function
                    Logger.LogWarning(ix, "Failed to create index on aqie_csvexport_jobs collection");
                }
            }

            var filter = Builders<eMailJobDocument>.Filter.Eq(d => d.JobId, jobId);
            var doc = await _jobCollection.Find(filter).FirstOrDefaultAsync();
            if (doc == null) return null;
            Logger.LogInformation("Document CreatedAt value: {CreatedAt}", doc.CreatedAt);
            //string resultUrl = "";
            string s3Key = $"{doc.DataSource}_{doc.PollutantName}_{doc.Region}_{doc.Year}.zip";
            string resultUrl = await GetS3data(s3Key);
            if (resultUrl == null) return null;

            Logger.LogInformation("MailService method started {Email},{ResultUrl}", doc.Email, resultUrl);
            
            string ms =  Getmilisecond(doc.CreatedAt.ToString());
            if (ms == null) return null;
            var mailResult = await MailService(doc.Email, doc.JobId + "/"+ ms);
            Logger.LogInformation("MailService method status response {MailResult}", mailResult);

            if (mailResult == "Success")
            {
                var update = Builders<eMailJobDocument>.Update
                    .Set(j => j.ResultUrl, resultUrl)
                    .Set(j => j.Status, JobStatusEnum.Completed)
                    .Set(j => j.MailSent, true)
                    .Set(j => j.UpdatedAt, DateTime.UtcNow);
                await _jobCollection.UpdateOneAsync(
                    Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, doc.JobId),
                    update
                );
                return resultUrl;
            }
            else
            {
                var update = Builders<eMailJobDocument>.Update
                    .Set(j => j.ResultUrl, resultUrl)
                    .Set(j => j.Status, JobStatusEnum.Failed)
                    .Set(j => j.MailSent, null)
                    .Set(j => j.ErrorReason, mailResult)
                    .Set(j => j.UpdatedAt, DateTime.UtcNow);
                await _jobCollection.UpdateOneAsync(
                    Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, doc.JobId),
                    update
                );
                return "";
            }

        }

        private async Task<string> GetS3data(string s3Key)
        {
            // Pull the object from S3 using the key stored in doc.ResultUrl
            string bucketName = Environment.GetEnvironmentVariable("S3_BUCKET_NAME");

            string s3Content = null;

            try
            {
                Logger.LogInformation("Fetching S3 object. Bucket: {BucketName}, Key: {S3Key}", bucketName, s3Key);

                var s3Request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = s3Key
                };

                using var s3Response = await amazonS3.GetObjectAsync(s3Request);
                using var reader = new StreamReader(s3Response.ResponseStream);
                s3Content = await reader.ReadToEndAsync();

                Logger.LogInformation("Successfully retrieved S3 object for key: {S3Key}", s3Key);

                Logger.LogInformation("S3 bucket GetPresignedUrlMail start {Datetime}", DateTime.Now);
                var url = await awsPreSignedURLService.GeneratePreSignedURL(bucketName, s3Key, 604800);
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

        private string Getmilisecond(string createdAt)
        {
            try
            {
                //var currentTime = DateTime.UtcNow;
                //var timeDifference = currentTime - createdAt;
                //return timeDifference.TotalMilliseconds.ToString();

                //string input = "ISODate('2026-02-24T12:56:53.825Z')";

                // 1) Extract the inner ISO string
                var match = Regex.Match(createdAt, @"ISODate\('(?<iso>[^']+)'\)", RegexOptions.CultureInvariant);
                if (!match.Success)
                    throw new FormatException("Input is not in the expected ISODate('...') format.");

                string iso = match.Groups["iso"].Value;

                // 2) Parse as DateTimeOffset (Z means UTC) and convert to milliseconds since epoch
                long ms = DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                                        .ToUnixTimeMilliseconds();
                return ms.ToString();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in Getmilisecond: {Error}", ex.Message);
                return null;
            }
        }
        public async Task<string> MailService(string email, string frameurl)
        {
            try
            {
                Logger.LogInformation("MailService enterted.");

                using var client = new HttpClient
                {
                    BaseAddress = new Uri(Environment.GetEnvironmentVariable("NOTIFY_BASEADDRESS"))
                };

                // Ensure the URL doesn't have leading slash if BaseAddress has trailing slash
                var url = Environment.GetEnvironmentVariable("NOTIFY_URL");

                var notificationRequest = new
                {
                    emailAddress = email,
                    templateId = Environment.GetEnvironmentVariable("EMAIL_TEMPLATEID"),
                    personalisation = new
                    {
                        datalink = Environment.GetEnvironmentVariable("EMAIL_BASEADDRESS") + frameurl
                    }
                };

                Logger.LogInformation("Sending notification to {BaseAddress}{Url}", client.BaseAddress, url);

                var response = await client.PostAsJsonAsync(url, notificationRequest);

                // Log the status code to help diagnose the issue
                Logger.LogInformation("Response status: {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    Logger.LogInformation("Email notification sent successfully");
                    return "Success";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Logger.LogError("Failed to send email notification to {Email}. Status: {StatusCode}, Error: {Error}",
                        email, response.StatusCode, errorContent);
                    //return "Failure";
                    return errorContent;
                }

            }
            catch (Exception ex)
            {
                Logger.LogError("Error in mailservice: {Error}", ex.Message);
                //return "Failure";
                return ex.Message;
            }
        }
    }
}
