using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using AqieHistoricaldataBackend.Utils.Mongo;
using Elastic.CommonSchema;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using MongoDB.Driver;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionEmailJobService(
        ILogger<HistoryexceedenceService> Logger,
        IMongoDbClientFactory MongoDbClientFactory,
        IAtomDataSelectionStationService AtomDataSelectionStationService
    ) : IAtomDataSelectionEmailJobService
    {
        public async Task<string> GetAtomemailjobDataSelection(QueryStringData data)
        {
            try
            {

                var jobCollection = MongoDbClientFactory.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs");

                // ensure index on JobId for quick lookup
                var indexKeys = Builders<eMailJobDocument>.IndexKeys.Ascending(j => j.JobId);
                await jobCollection.Indexes.CreateOneAsync(new CreateIndexModel<eMailJobDocument>(indexKeys));

                // create GUID and persist job in MongoDB as Pending, then enqueue background work
                var jobId = Guid.NewGuid().ToString("N");

                var jobDoc = new eMailJobDocument
                {
                    JobId = jobId,
                    PollutantName = data.pollutantName ?? string.Empty,
                    DataSource = data.dataSource ?? string.Empty,
                    Year = data.Year ?? string.Empty,
                    Region = data.Region ?? string.Empty,
                    Regiontype = data.regiontype ?? string.Empty,
                    Dataselectorfiltertype = data.dataselectorfiltertype ?? string.Empty,
                    Dataselectordownloadtype = data.dataselectordownloadtype ?? string.Empty,
                    Status = JobStatusEnum.Pending,
                    StartTime = null,
                    EndTime = null,
                    ErrorReason = string.Empty,
                    ResultUrl = string.Empty,
                    Email = data.email ?? string.Empty,
                    MailSent = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null
                };

                await jobCollection.InsertOneAsync(jobDoc);
                return "Success";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in Atom GetAtomemailjobDataSelection"); // called exactly once
                return "Failure";
            }
        }

        public async Task ProcessPendingEmailJobsAsync(CancellationToken stoppingToken)
        {
            try
            {



                Logger.LogInformation("ProcessPendingEmailJobsAsync entered.");
                var jobCollection = MongoDbClientFactory.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs");

                // Filter: Status == Pending, email is not null/empty, mailSent is null
                var filter = Builders<eMailJobDocument>.Filter.And(
                    Builders<eMailJobDocument>.Filter.Eq(j => j.Status, JobStatusEnum.Pending),
                    Builders<eMailJobDocument>.Filter.Ne(j => j.Email, null),
                    Builders<eMailJobDocument>.Filter.Ne(j => j.Email, ""),
                    Builders<eMailJobDocument>.Filter.Eq(j => j.MailSent, null)
                );

                var pendingJobs = await jobCollection.Find(filter).ToListAsync(stoppingToken);
                Logger.LogInformation("Found {Count} pending email jobs.", pendingJobs.Count);

                if (pendingJobs.Count == 0)
                {
                    Logger.LogInformation("No pending email jobs found. Exiting ProcessPendingEmailJobsAsync.");
                    return;
                }

                foreach (var job in pendingJobs)
                {
                    try
                    {
                        // Atomically claim the job — only succeeds if Status is still Pending
                        var claimFilter = Builders<eMailJobDocument>.Filter.And(
                            Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, job.JobId),
                            Builders<eMailJobDocument>.Filter.Eq(j => j.Status, JobStatusEnum.Pending)
                        );

                        var claimUpdate = Builders<eMailJobDocument>.Update
                            .Set(j => j.Status, JobStatusEnum.Processing)
                            .Set(j => j.StartTime, DateTime.UtcNow);

                        var claimedJob = await jobCollection.FindOneAndUpdateAsync(
                            claimFilter,
                            claimUpdate,
                            cancellationToken: stoppingToken
                        );

                        // If claimedJob is null, another process already claimed it
                        if (claimedJob == null)
                        {
                            Logger.LogInformation("Job {JobId} already claimed by another process.", job.JobId);
                            continue;
                        }                   

                        var resultUrl = await AtomDataSelectionStationService.GetAtomDataSelectionStation(
                            job.PollutantName,
                            job.DataSource,
                            job.Year,
                            job.Region,
                            job.Regiontype,
                            job.Dataselectorfiltertype,
                            job.Dataselectordownloadtype,
                            job.Email
                        );

                        Logger.LogInformation("ProcessPendingEmailJobsAsync presigned url {ResultUrl}", resultUrl);

                        var endTimeUpdate = Builders<eMailJobDocument>.Update
                            .Set(j => j.EndTime, DateTime.UtcNow);
                        await jobCollection.UpdateOneAsync(
                            Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, job.JobId),
                            endTimeUpdate,
                            cancellationToken: stoppingToken
                        );

                        Logger.LogInformation("Document CreatedAt value: {CreatedAt}", job.CreatedAt);
                        var ms = Getmilisecond(job.CreatedAt);
                        if (ms == null)
                        {
                            continue;
                        }

                        Logger.LogInformation("MailService method started {Email},{ResultUrl}", job.Email, resultUrl);
                        var mailResult = await MailService(job.Email, job.JobId + "/" + ms);
                        Logger.LogInformation("MailService method status response {MailResult}", mailResult);

                        if (mailResult == "Success")
                        {
                            var update = Builders<eMailJobDocument>.Update
                                .Set(j => j.ResultUrl, resultUrl)
                                .Set(j => j.Status, JobStatusEnum.Completed)
                                .Set(j => j.MailSent, true)
                                .Set(j => j.UpdatedAt, DateTime.UtcNow);
                            await jobCollection.UpdateOneAsync(
                                Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, job.JobId),
                                update,
                                cancellationToken: stoppingToken
                            );
                        }
                        else
                        {
                            var update = Builders<eMailJobDocument>.Update
                                .Set(j => j.ResultUrl, resultUrl)
                                .Set(j => j.Status, JobStatusEnum.Failed)
                                .Set(j => j.MailSent, null)
                                .Set(j => j.ErrorReason, mailResult)
                                .Set(j => j.UpdatedAt, DateTime.UtcNow);
                            await jobCollection.UpdateOneAsync(
                                Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, job.JobId),
                                update,
                                cancellationToken: stoppingToken
                            );
                        }
                    }
                    catch (Exception exJob)
                    {
                        Logger.LogError(exJob, "ProcessPendingEmailJobsAsync Error processing email job {JobId}", job.JobId);
                        var update = Builders<eMailJobDocument>.Update
                            .Set(j => j.ErrorReason, exJob.Message)
                            .Set(j => j.Status, JobStatusEnum.Failed)
                            .Set(j => j.UpdatedAt, DateTime.UtcNow);
                        await jobCollection.UpdateOneAsync(
                            Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, job.JobId),
                            update,
                            cancellationToken: stoppingToken
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,"Error in ProcessPendingEmailJobsAsync");
            }
        }

        public async Task<string> MailService(string email, string frameurl)
        {
            try
            {
                Logger.LogInformation("MailService enterted.");

                var notifyBaseAddress = Environment.GetEnvironmentVariable("NOTIFY_BASEADDRESS");
                if (string.IsNullOrEmpty(notifyBaseAddress))
                {
                    Logger.LogError("NOTIFY_BASEADDRESS environment variable is not set");
                    return "Configuration error: NOTIFY_BASEADDRESS not set";
                }

                using var client = new HttpClient
                {
                    BaseAddress = new Uri(notifyBaseAddress)
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
                    return errorContent;
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex,"Error in mailservice");
                return ex.Message;
            }
        }

        private string? Getmilisecond(DateTime createdAt)
        {
            try
            {
                if (createdAt.Kind != DateTimeKind.Utc)
                    createdAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);

                return new DateTimeOffset(createdAt).ToUnixTimeMilliseconds().ToString();

            }
            catch (Exception ex)
            {
                Logger.LogError(ex,"Error in Getmilisecond");
                return null;
            }
        }
    }
}
