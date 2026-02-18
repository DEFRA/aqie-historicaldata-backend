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
                Logger.LogInformation("ProcessPendingEmailJobsAsync enterted.");
                var jobCollection = MongoDbClientFactory.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs");

                // Build the filter: email is not null/empty, mailSent is null
                var filter = Builders<eMailJobDocument>.Filter.And(
                    Builders<eMailJobDocument>.Filter.Ne(j => j.Email, null),
                    Builders<eMailJobDocument>.Filter.Ne(j => j.Email, ""), // Exclude empty strings
                    Builders<eMailJobDocument>.Filter.Eq(j => j.MailSent, null)
                );

                var allJobs = await jobCollection.Find(Builders<eMailJobDocument>.Filter.Empty).ToListAsync(stoppingToken);
                Logger.LogInformation("Total jobs in collection: {Count}", allJobs.Count);

                //Read the filtered data
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
                        //Call the station service for each job

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

                        Logger.LogInformation("ProcessPendingEmailJobsAsync presigned url {ResultUrl}", ResultUrl);
                        if (!string.IsNullOrEmpty(job.Email) && !string.IsNullOrEmpty(ResultUrl))
                            {
                            //await _emailService.SendEmailAsync(job.Email, "Mail job Your Data Export", $"Download: {job.ResultUrl}");
                            Logger.LogInformation("MailService method started{Email},{ResultUrl}", job.Email, ResultUrl);
                            var mailresult = await MailService(job.Email, ResultUrl);
                            Logger.LogInformation("MailService method status response {mailresult}", mailresult);
                            if (mailresult == "Success")
                                {
                                    // Mark job as mail sent (set to "success")
                                    var update = Builders<eMailJobDocument>.Update
                                        .Set(j => j.ResultUrl, ResultUrl)
                                        .Set(j => j.MailSent, true)
                                        .Set(j => j.UpdatedAt, DateTime.UtcNow);
                                    await jobCollection.UpdateOneAsync(
                                        Builders<eMailJobDocument>.Filter.Eq(j => j.JobId, job.JobId),
                                        update,
                                        cancellationToken: stoppingToken
                                    );
                                }
                            }
                    }
                    catch (Exception exJob)
                    {
                        Logger.LogError("ProcessPendingEmailJobsAsync Error processing email job {JobId}: {Error}", job.JobId, exJob.Message);
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

        public async Task<string> MailService(string email, string ResultUrl)
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
                        datalink = ResultUrl
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
                    return "Failure";
                }

            }
            catch (Exception ex)
            {
                Logger.LogError("Error in mailservice: {Error}", ex.Message);
                return "Failure";
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
