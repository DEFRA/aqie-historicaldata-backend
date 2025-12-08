using AqieHistoricaldataBackend.Utils.Mongo;
using MongoDB.Driver;
using System.Net;
using System.Net.Mail;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{

    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
    public class AtomDataSelectionEmailJobService(
        ILogger<HistoryexceedenceService> Logger,
        IAtomHourlyFetchService AtomHourlyFetchService,
        IMongoDbClientFactory MongoDbClientFactory,
        IAtomDataSelectionStationService AtomDataSelectionStationService,
        IEmailService emailService
    ) : IAtomDataSelectionEmailJobService
    {
        private readonly IEmailService _emailService = emailService;
        public async Task<string> GetAtomemailjobDataSelection(QueryStringData data)
        {
            try
            {
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
        public async Task ProcessPendingEmailJobsAsync(CancellationToken stoppingToken)
        {
            try
            {
                var jobCollection = MongoDbClientFactory.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs");

                // Build the filter: email is not null/empty, mailSent is null
                var filter = Builders<eMailJobDocument>.Filter.And(
                    Builders<eMailJobDocument>.Filter.Ne(j => j.Email, null),
                    Builders<eMailJobDocument>.Filter.Ne(j => j.Email, ""), // Exclude empty strings
                    Builders<eMailJobDocument>.Filter.Eq(j => j.MailSent, null)
                );

                var allJobs = await jobCollection.Find(Builders<eMailJobDocument>.Filter.Empty).ToListAsync(stoppingToken);
                Logger.LogInformation("Total jobs in collection: {Count}", allJobs.Count);

                // Read the filtered data
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
                        // Call the station service for each job
                        var ResultUrl = await AtomDataSelectionStationService.GetAtomDataSelectionStation(
                            job.PollutantName,
                            job.DataSource,
                            job.Year,
                            job.Region,
                            job.Regiontype,
                            job.Dataselectorfiltertype,
                            job.Dataselectordownloadtype,
                            job.Email
                        );

                        if (!string.IsNullOrEmpty(job.Email) && !string.IsNullOrEmpty(ResultUrl))
                        {
                            await _emailService.SendEmailAsync(job.Email, "Your Data Export", $"Download: {job.ResultUrl}");
                            // Mark job as mail sent (set to "success")
                            var update = Builders<eMailJobDocument>.Update
                                .Set(j => j.MailSent, true)
                                .Set(j => j.UpdatedAt, DateTime.UtcNow);
                            await jobCollection.UpdateOneAsync(
                                Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, job.JobId),
                                update,
                                cancellationToken: stoppingToken
                            );
                        }

                        Logger.LogInformation("Processed email job {JobId} for {Email}", job.JobId, job.Email);
                    }
                    catch (Exception exJob)
                    {
                        Logger.LogError("Error processing email job {JobId}: {Error}", job.JobId, exJob.Message);
                        // Optionally, update job with error reason
                        var update = Builders<eMailJobDocument>.Update
                            .Set(j => j.ErrorReason, exJob.Message)
                            .Set(j => j.MailSent, false)
                            .Set(j => j.UpdatedAt, DateTime.UtcNow);
                        await jobCollection.UpdateOneAsync(
                            Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, job.JobId),
                            update
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ProcessPendingEmailJobsAsync: {Error}", ex.Message);
            }
        }

        public class EmailService : IEmailService
        {
            public async Task SendEmailAsync(string to, string subject, string body)
            {
                var smtpClient = new SmtpClient("smtp.yourserver.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("yourusername", "yourpassword"),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage("from@yourdomain.com", to, subject, body);
                await smtpClient.SendMailAsync(mailMessage);
            }
        }
    }
}
