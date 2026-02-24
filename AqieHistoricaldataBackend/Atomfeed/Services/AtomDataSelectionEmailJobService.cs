using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using AqieHistoricaldataBackend.Utils.Mongo;
using Elastic.CommonSchema;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using MongoDB.Driver;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    //public interface IEmailService
    //{
    //    Task SendEmailAsync(string to, string subject, string body);
    //}
    public class AtomDataSelectionEmailJobService(
        ILogger<HistoryexceedenceService> Logger,
        IAtomHourlyFetchService AtomHourlyFetchService,
        IMongoDbClientFactory MongoDbClientFactory,
        IAtomDataSelectionStationService AtomDataSelectionStationService,
        //IEmailService emailService,
        IHttpClientFactory httpClientFactory
    ) : IAtomDataSelectionEmailJobService
    {
        //private readonly IEmailService _emailService = emailService;
        //private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        // MongoDB collection for job documents
        private IMongoCollection<eMailJobDocument>? _jobCollection;
        public async Task<string> GetAtomemailjobDataSelection(QueryStringData data)
        {
            try
            {
                //for local
                //ProcessPendingEmailJobsAsync();
                // Declare _jobCollection as a local variable
                var jobCollection = MongoDbClientFactory.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs");

                // ensure index on JobId for quick lookup
                var indexKeys = Builders<eMailJobDocument>.IndexKeys.Ascending(j => j.JobId);
                jobCollection.Indexes.CreateOne(new CreateIndexModel<eMailJobDocument>(indexKeys));

                // create GUID and persist job in MongoDB as Pending, then enqueue background work
                var jobId = Guid.NewGuid().ToString("N");

                var jobDoc = new eMailJobDocument
                {
                    JobId = jobId,
                    PollutantName = data.pollutantName,
                    DataSource = data.dataSource,
                    Year = data.Year,
                    Region = data.Region,
                    Regiontype = data.regiontype,
                    Dataselectorfiltertype = data.dataselectorfiltertype,
                    Dataselectordownloadtype = data.dataselectordownloadtype,
                    Status = JobStatusEnum.Pending,
                    StartTime = null,
                    EndTime = null,
                    ErrorReason = null,
                    ResultUrl = null,
                    Email = data.email,
                    MailSent = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null
                };

                await jobCollection.InsertOneAsync(jobDoc);
                return "Success";
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in Atom GetAtomemailjobDataSelection {Error}", ex.Message);
                Logger.LogError("Error in Atom GetAtomemailjobDataSelection {Error}", ex.StackTrace);
                return "Failure";
            }
        }

        // Change the parameter type of ProcessPendingEmailJobsAsync from string to CancellationToken
        //public async Task ProcessPendingEmailJobsAsync(CancellationToken stoppingToken)
        //public async Task ProcessPendingEmailJobsAsync()
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
                            //job.JobId
                        );

                        Logger.LogInformation("ProcessPendingEmailJobsAsync presigned url {ResultUrl}", resultUrl);

                        var endTimeUpdate = Builders<eMailJobDocument>.Update
                            .Set(j => j.EndTime, DateTime.UtcNow);
                        await jobCollection.UpdateOneAsync(
                            Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, job.JobId),
                            endTimeUpdate,
                            cancellationToken: stoppingToken
                        );


                    }
                    catch (Exception exJob)
                    {
                        Logger.LogError("ProcessPendingEmailJobsAsync Error processing email job {JobId}: {Error}", job.JobId, exJob.Message);
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
                Logger.LogError("Error in ProcessPendingEmailJobsAsync: {Error}", ex.Message);
            }
        }





            //public class EmailService : IEmailService
            //{
            //    public async Task SendEmailAsync(string to, string subject, string body)
            //    {
            //        var smtpClient = new SmtpClient("smtp.yourserver.com")
            //        {
            //            Port = 587,
            //            Credentials = new NetworkCredential("yourusername", "yourpassword"),
            //            EnableSsl = true,
            //        };

            //        var mailMessage = new MailMessage("from@yourdomain.com", to, subject, body);
            //        await smtpClient.SendMailAsync(mailMessage);
            //    }
            //}
        }
}
