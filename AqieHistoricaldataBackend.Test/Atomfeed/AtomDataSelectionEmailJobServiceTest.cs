//using System;
//using System.Collections.Generic;
//using System.Net;
//using System.Net.Http;
//using System.Threading;
//using System.Threading.Tasks;
//using AqieHistoricaldataBackend.Atomfeed.Services;
//using AqieHistoricaldataBackend.Utils.Mongo;
//using Microsoft.Extensions.Logging;
//using Moq;
//using Moq.Protected;
//using MongoDB.Driver;
//using Xunit;
//using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
//using System.Net.Http.Json;

//namespace AqieHistoricaldataBackend.Test.Atomfeed
//{
//    /// <summary>
//    /// Testable subclass that allows injecting a controllable HttpClient
//    /// into MailService without changing production code.
//    /// </summary>
//    public class TestableAtomDataSelectionEmailJobService : AtomDataSelectionEmailJobService
//    {
//        private readonly HttpClient _httpClient;
//        private readonly ILogger<HistoryexceedenceService> _logger;

//        public TestableAtomDataSelectionEmailJobService(
//            ILogger<HistoryexceedenceService> logger,
//            IAtomHourlyFetchService atomHourlyFetchService,
//            IMongoDbClientFactory mongoDbClientFactory,
//            IAtomDataSelectionStationService atomDataSelectionStationService,
//            IHttpClientFactory httpClientFactory,
//            HttpClient httpClient)
//            : base(logger, atomHourlyFetchService, mongoDbClientFactory, atomDataSelectionStationService, httpClientFactory)
//        {
//            _httpClient = httpClient;
//            _logger = logger;
//        }

//        public new async Task<string> MailService(string email, string resultUrl)
//        {
//            try
//            {
//                _logger.LogInformation("MailService enterted.");

//                _httpClient.BaseAddress = new Uri(
//                    Environment.GetEnvironmentVariable("NOTIFY_BASEADDRESS") ?? "http://localhost/");

//                var url = Environment.GetEnvironmentVariable("NOTIFY_URL") ?? "/notify";

//                var notificationRequest = new
//                {
//                    emailAddress = email,
//                    templateId = Environment.GetEnvironmentVariable("EMAIL_TEMPLATEID"),
//                    personalisation = new { datalink = resultUrl }
//                };

//                var response = await _httpClient.PostAsJsonAsync(url, notificationRequest);

//                if (response.IsSuccessStatusCode)
//                {
//                    _logger.LogInformation("Email notification sent successfully");
//                    return "Success";
//                }

//                var errorContent = await response.Content.ReadAsStringAsync();
//                _logger.LogError("Failed to send email notification. Status: {StatusCode}, Error: {Error}",
//                    response.StatusCode, errorContent);
//                return errorContent;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError("Error in mailservice: {Error}", ex.Message);
//                return ex.Message;
//            }
//        }
//    }

//    public class AtomDataSelectionEmailJobServiceTests
//    {
//        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
//        private readonly Mock<IAtomHourlyFetchService> _hourlyFetchServiceMock;
//        private readonly Mock<IMongoDbClientFactory> _mongoDbClientFactoryMock;
//        private readonly Mock<IAtomDataSelectionStationService> _stationServiceMock;
//        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
//        private readonly Mock<IMongoCollection<eMailJobDocument>> _collectionMock;
//        private readonly Mock<IMongoIndexManager<eMailJobDocument>> _indexManagerMock;

//        public AtomDataSelectionEmailJobServiceTests()
//        {
//            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
//            _hourlyFetchServiceMock = new Mock<IAtomHourlyFetchService>();
//            _mongoDbClientFactoryMock = new Mock<IMongoDbClientFactory>();
//            _stationServiceMock = new Mock<IAtomDataSelectionStationService>();
//            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
//            _collectionMock = new Mock<IMongoCollection<eMailJobDocument>>();
//            _indexManagerMock = new Mock<IMongoIndexManager<eMailJobDocument>>();

//            _collectionMock.Setup(c => c.Indexes).Returns(_indexManagerMock.Object);
//            _indexManagerMock
//                .Setup(i => i.CreateOne(
//                    It.IsAny<CreateIndexModel<eMailJobDocument>>(),
//                    It.IsAny<CreateOneIndexOptions>(),
//                    It.IsAny<CancellationToken>()))
//                .Returns("index_name");

//            _mongoDbClientFactoryMock
//                .Setup(f => f.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs"))
//                .Returns(_collectionMock.Object);
//        }

//        private AtomDataSelectionEmailJobService CreateService()
//        {
//            return new AtomDataSelectionEmailJobService(
//                _loggerMock.Object,
//                _hourlyFetchServiceMock.Object,
//                _mongoDbClientFactoryMock.Object,
//                _stationServiceMock.Object,
//                _httpClientFactoryMock.Object);
//        }

//        private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content = "")
//        {
//            var handlerMock = new Mock<HttpMessageHandler>();
//            handlerMock.Protected()
//                .Setup<Task<HttpResponseMessage>>(
//                    "SendAsync",
//                    ItExpr.IsAny<HttpRequestMessage>(),
//                    ItExpr.IsAny<CancellationToken>())
//                .ReturnsAsync(new HttpResponseMessage(statusCode)
//                {
//                    Content = new StringContent(content)
//                });
//            return new HttpClient(handlerMock.Object);
//        }

//        // -----------------------------------------------------------------------
//        // GetAtomemailjobDataSelection — success path
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomemailjobDataSelection_ReturnsSuccess_WhenInsertSucceeds()
//        {
//            _collectionMock
//                .Setup(c => c.InsertOneAsync(
//                    It.IsAny<eMailJobDocument>(),
//                    It.IsAny<InsertOneOptions>(),
//                    It.IsAny<CancellationToken>()))
//                .Returns(Task.CompletedTask);

//            var service = CreateService();
//            var data = new QueryStringData
//            {
//                pollutantName = "NO2",
//                dataSource = "AURN",
//                Year = "2024",
//                Region = "London",
//                regiontype = "country",
//                dataselectorfiltertype = "all",
//                dataselectordownloadtype = "hourly",
//                email = "test@example.com"
//            };

//            var result = await service.GetAtomemailjobDataSelection(data);

//            Assert.Equal("Success", result);
//            _collectionMock.Verify(
//                c => c.InsertOneAsync(
//                    It.Is<eMailJobDocument>(d =>
//                        d.PollutantName == "NO2" &&
//                        d.Email == "test@example.com" &&
//                        d.Status == JobStatusEnum.Pending &&
//                        d.MailSent == null &&
//                        d.StartTime == null),
//                    It.IsAny<InsertOneOptions>(),
//                    It.IsAny<CancellationToken>()),
//                Times.Once);
//        }

//        [Fact]
//        public async Task GetAtomemailjobDataSelection_SetsAllFieldsFromQueryStringData()
//        {
//            eMailJobDocument capturedDoc = null!;
//            _collectionMock
//                .Setup(c => c.InsertOneAsync(
//                    It.IsAny<eMailJobDocument>(),
//                    It.IsAny<InsertOneOptions>(),
//                    It.IsAny<CancellationToken>()))
//                .Callback<eMailJobDocument, InsertOneOptions, CancellationToken>(
//                    (doc, _, _) => capturedDoc = doc)
//                .Returns(Task.CompletedTask);

//            var service = CreateService();
//            var data = new QueryStringData
//            {
//                pollutantName = "PM10",
//                dataSource = "AQMS",
//                Year = "2023",
//                Region = "North East",
//                regiontype = "region",
//                dataselectorfiltertype = "region",
//                dataselectordownloadtype = "daily",
//                email = "user@domain.com"
//            };

//            await service.GetAtomemailjobDataSelection(data);

//            Assert.NotNull(capturedDoc);
//            Assert.Equal("PM10", capturedDoc.PollutantName);
//            Assert.Equal("AQMS", capturedDoc.DataSource);
//            Assert.Equal("2023", capturedDoc.Year);
//            Assert.Equal("North East", capturedDoc.Region);
//            Assert.Equal("region", capturedDoc.Regiontype);
//            Assert.Equal("region", capturedDoc.Dataselectorfiltertype);
//            Assert.Equal("daily", capturedDoc.Dataselectordownloadtype);
//            Assert.Equal("user@domain.com", capturedDoc.Email);
//            Assert.Equal(JobStatusEnum.Pending, capturedDoc.Status);
//            Assert.NotEmpty(capturedDoc.JobId);
//        }

//        [Fact]
//        public async Task GetAtomemailjobDataSelection_GeneratesUniqueJobIds()
//        {
//            var jobIds = new List<string>();
//            _collectionMock
//                .Setup(c => c.InsertOneAsync(
//                    It.IsAny<eMailJobDocument>(),
//                    It.IsAny<InsertOneOptions>(),
//                    It.IsAny<CancellationToken>()))
//                .Callback<eMailJobDocument, InsertOneOptions, CancellationToken>(
//                    (doc, _, _) => jobIds.Add(doc.JobId))
//                .Returns(Task.CompletedTask);

//            var service = CreateService();
//            var data = new QueryStringData { email = "a@b.com" };

//            await service.GetAtomemailjobDataSelection(data);
//            await service.GetAtomemailjobDataSelection(data);

//            Assert.Equal(2, jobIds.Count);
//            Assert.NotEqual(jobIds[0], jobIds[1]);
//        }

//        // -----------------------------------------------------------------------
//        // GetAtomemailjobDataSelection — exception / failure path
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task GetAtomemailjobDataSelection_ReturnsFailure_WhenInsertThrows()
//        {
//            _collectionMock
//                .Setup(c => c.InsertOneAsync(
//                    It.IsAny<eMailJobDocument>(),
//                    It.IsAny<InsertOneOptions>(),
//                    It.IsAny<CancellationToken>()))
//                .ThrowsAsync(new Exception("Mongo unavailable"));

//            var service = CreateService();
//            var result = await service.GetAtomemailjobDataSelection(new QueryStringData());

//            Assert.Equal("Failure", result);
//        }

//        [Fact]
//        public async Task GetAtomemailjobDataSelection_ReturnsFailure_WhenGetCollectionThrows()
//        {
//            _mongoDbClientFactoryMock
//                .Setup(f => f.GetCollection<eMailJobDocument>(It.IsAny<string>()))
//                .Throws(new Exception("Connection error"));

//            var service = CreateService();
//            var result = await service.GetAtomemailjobDataSelection(new QueryStringData());

//            Assert.Equal("Failure", result);
//            _loggerMock.Verify(
//                x => x.Log(
//                    LogLevel.Error,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, _) => true),
//                    It.IsAny<Exception>(),
//                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//                Times.AtLeast(2));
//        }

//        // -----------------------------------------------------------------------
//        // ProcessPendingEmailJobsAsync — no pending jobs
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task ProcessPendingEmailJobsAsync_DoesNothing_WhenNoPendingJobs()
//        {
//            SetupFindReturning(new List<eMailJobDocument>());

//            var service = CreateService();
//            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

//            _collectionMock.Verify(
//                c => c.FindOneAndUpdateAsync(
//                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
//                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
//                    It.IsAny<FindOneAndUpdateOptions<eMailJobDocument>>(),
//                    It.IsAny<CancellationToken>()),
//                Times.Never);
//        }

//        // -----------------------------------------------------------------------
//        // ProcessPendingEmailJobsAsync — job already claimed by another process
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task ProcessPendingEmailJobsAsync_SkipsJob_WhenAlreadyClaimed()
//        {
//            var job = BuildPendingJob("job-001", "a@b.com");
//            SetupFindReturning(new List<eMailJobDocument> { job });

//            // FindOneAndUpdateAsync returns null → already claimed
//            _collectionMock
//                .Setup(c => c.FindOneAndUpdateAsync(
//                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
//                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
//                    It.IsAny<FindOneAndUpdateOptions<eMailJobDocument>>(),
//                    It.IsAny<CancellationToken>()))
//                .ReturnsAsync((eMailJobDocument)null!);

//            var service = CreateService();
//            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

//            _stationServiceMock.Verify(
//                s => s.GetAtomDataSelectionStation(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>()),
//                Times.Never);
//        }

//        // -----------------------------------------------------------------------
//        // ProcessPendingEmailJobsAsync — successful mail send
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task ProcessPendingEmailJobsAsync_SetsStatusCompleted_WhenMailSucceeds()
//        {
//            var job = BuildPendingJob("job-002", "success@test.com");
//            SetupFindReturning(new List<eMailJobDocument> { job });
//            SetupClaimReturning(job);

//            _stationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync("https://s3.bucket/file.csv");

//            SetupUpdateOneSucceeds();

//            Environment.SetEnvironmentVariable("NOTIFY_BASEADDRESS", "http://notify.local/");
//            Environment.SetEnvironmentVariable("NOTIFY_URL", "/v2/notifications/email");
//            Environment.SetEnvironmentVariable("EMAIL_TEMPLATEID", "tmpl-001");

//            var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
//            var service = new TestableAtomDataSelectionEmailJobService(
//                _loggerMock.Object,
//                _hourlyFetchServiceMock.Object,
//                _mongoDbClientFactoryMock.Object,
//                _stationServiceMock.Object,
//                _httpClientFactoryMock.Object,
//                httpClient);

//            // Call ProcessPendingEmailJobsAsync on base — wire MailService via override
//            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

//            // UpdateOneAsync should be called at least twice (endTime + status)
//            _collectionMock.Verify(
//                c => c.UpdateOneAsync(
//                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
//                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
//                    It.IsAny<UpdateOptions>(),
//                    It.IsAny<CancellationToken>()),
//                Times.AtLeast(2));
//        }

//        // -----------------------------------------------------------------------
//        // ProcessPendingEmailJobsAsync — mail fails (non-success HTTP)
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task ProcessPendingEmailJobsAsync_SetsStatusFailed_WhenMailFails()
//        {
//            var job = BuildPendingJob("job-003", "fail@test.com");
//            SetupFindReturning(new List<eMailJobDocument> { job });
//            SetupClaimReturning(job);

//            _stationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync("https://s3.bucket/file.csv");

//            SetupUpdateOneSucceeds();

//            Environment.SetEnvironmentVariable("NOTIFY_BASEADDRESS", "http://notify.local/");
//            Environment.SetEnvironmentVariable("NOTIFY_URL", "/v2/notifications/email");
//            Environment.SetEnvironmentVariable("EMAIL_TEMPLATEID", "tmpl-001");

//            var httpClient = CreateMockHttpClient(HttpStatusCode.BadRequest, "Bad request error");
//            var service = new TestableAtomDataSelectionEmailJobService(
//                _loggerMock.Object,
//                _hourlyFetchServiceMock.Object,
//                _mongoDbClientFactoryMock.Object,
//                _stationServiceMock.Object,
//                _httpClientFactoryMock.Object,
//                httpClient);

//            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

//            _collectionMock.Verify(
//                c => c.UpdateOneAsync(
//                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
//                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
//                    It.IsAny<UpdateOptions>(),
//                    It.IsAny<CancellationToken>()),
//                Times.AtLeast(2));
//        }

//        // -----------------------------------------------------------------------
//        // ProcessPendingEmailJobsAsync — station service throws per-job exception
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task ProcessPendingEmailJobsAsync_SetsJobFailed_WhenStationServiceThrows()
//        {
//            var job = BuildPendingJob("job-004", "error@test.com");
//            SetupFindReturning(new List<eMailJobDocument> { job });
//            SetupClaimReturning(job);

//            _stationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>()))
//                .ThrowsAsync(new Exception("S3 error"));

//            SetupUpdateOneSucceeds();

//            var service = CreateService();
//            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

//            _collectionMock.Verify(
//                c => c.UpdateOneAsync(
//                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
//                    It.Is<UpdateDefinition<eMailJobDocument>>(u => u != null),
//                    It.IsAny<UpdateOptions>(),
//                    It.IsAny<CancellationToken>()),
//                Times.Once);
//        }

//        // -----------------------------------------------------------------------
//        // ProcessPendingEmailJobsAsync — outer exception (collection throws)
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task ProcessPendingEmailJobsAsync_LogsError_WhenOuterExceptionOccurs()
//        {
//            _mongoDbClientFactoryMock
//                .Setup(f => f.GetCollection<eMailJobDocument>(It.IsAny<string>()))
//                .Throws(new Exception("DB unavailable"));

//            var service = CreateService();
//            // Should not throw — swallowed by outer catch
//            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

//            _loggerMock.Verify(
//                x => x.Log(
//                    LogLevel.Error,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, _) => true),
//                    It.IsAny<Exception>(),
//                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//                Times.Once);
//        }

//        // -----------------------------------------------------------------------
//        // ProcessPendingEmailJobsAsync — multiple jobs, processes all
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task ProcessPendingEmailJobsAsync_ProcessesAllPendingJobs()
//        {
//            var job1 = BuildPendingJob("job-010", "a@test.com");
//            var job2 = BuildPendingJob("job-011", "b@test.com");

//            SetupFindReturning(new List<eMailJobDocument> { job1, job2 });

//            // Both are claimed successfully
//            _collectionMock
//                .SetupSequence(c => c.FindOneAndUpdateAsync(
//                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
//                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
//                    It.IsAny<FindOneAndUpdateOptions<eMailJobDocument>>(),
//                    It.IsAny<CancellationToken>()))
//                .ReturnsAsync(job1)
//                .ReturnsAsync(job2);

//            _stationServiceMock
//                .Setup(s => s.GetAtomDataSelectionStation(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>()))
//                .ReturnsAsync("https://s3.bucket/file.csv");

//            SetupUpdateOneSucceeds();

//            Environment.SetEnvironmentVariable("NOTIFY_BASEADDRESS", "http://notify.local/");
//            Environment.SetEnvironmentVariable("NOTIFY_URL", "/v2/notifications/email");
//            Environment.SetEnvironmentVariable("EMAIL_TEMPLATEID", "tmpl-001");

//            var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
//            var service = new TestableAtomDataSelectionEmailJobService(
//                _loggerMock.Object,
//                _hourlyFetchServiceMock.Object,
//                _mongoDbClientFactoryMock.Object,
//                _stationServiceMock.Object,
//                _httpClientFactoryMock.Object,
//                httpClient);

//            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

//            _stationServiceMock.Verify(
//                s => s.GetAtomDataSelectionStation(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>()),
//                Times.Exactly(2));
//        }

//        // -----------------------------------------------------------------------
//        // MailService — success
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task MailService_ReturnsSuccess_WhenHttpResponseIsSuccessful()
//        {
//            Environment.SetEnvironmentVariable("NOTIFY_BASEADDRESS", "http://notify.local/");
//            Environment.SetEnvironmentVariable("NOTIFY_URL", "/v2/notifications/email");
//            Environment.SetEnvironmentVariable("EMAIL_TEMPLATEID", "tmpl-abc");

//            var httpClient = CreateMockHttpClient(HttpStatusCode.Created);
//            var service = new TestableAtomDataSelectionEmailJobService(
//                _loggerMock.Object,
//                _hourlyFetchServiceMock.Object,
//                _mongoDbClientFactoryMock.Object,
//                _stationServiceMock.Object,
//                _httpClientFactoryMock.Object,
//                httpClient);

//            var result = await service.MailService("user@test.com", "https://s3.bucket/file.csv");

//            Assert.Equal("Success", result);
//        }

//        // -----------------------------------------------------------------------
//        // MailService — non-success HTTP
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task MailService_ReturnsErrorContent_WhenHttpResponseIsNotSuccessful()
//        {
//            Environment.SetEnvironmentVariable("NOTIFY_BASEADDRESS", "http://notify.local/");
//            Environment.SetEnvironmentVariable("NOTIFY_URL", "/v2/notifications/email");
//            Environment.SetEnvironmentVariable("EMAIL_TEMPLATEID", "tmpl-abc");

//            var httpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "Internal Server Error");
//            var service = new TestableAtomDataSelectionEmailJobService(
//                _loggerMock.Object,
//                _hourlyFetchServiceMock.Object,
//                _mongoDbClientFactoryMock.Object,
//                _stationServiceMock.Object,
//                _httpClientFactoryMock.Object,
//                httpClient);

//            var result = await service.MailService("user@test.com", "https://s3.bucket/file.csv");

//            Assert.Equal("Internal Server Error", result);
//        }

//        [Fact]
//        public async Task MailService_ReturnsErrorContent_WhenHttpResponseIsUnauthorized()
//        {
//            Environment.SetEnvironmentVariable("NOTIFY_BASEADDRESS", "http://notify.local/");
//            Environment.SetEnvironmentVariable("NOTIFY_URL", "/v2/notifications/email");
//            Environment.SetEnvironmentVariable("EMAIL_TEMPLATEID", "tmpl-abc");

//            var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized, "Unauthorized");
//            var service = new TestableAtomDataSelectionEmailJobService(
//                _loggerMock.Object,
//                _hourlyFetchServiceMock.Object,
//                _mongoDbClientFactoryMock.Object,
//                _stationServiceMock.Object,
//                _httpClientFactoryMock.Object,
//                httpClient);

//            var result = await service.MailService("user@test.com", "https://s3.bucket/file.csv");

//            Assert.Equal("Unauthorized", result);
//            _loggerMock.Verify(
//                x => x.Log(
//                    LogLevel.Error,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, _) => true),
//                    It.IsAny<Exception>(),
//                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//                Times.Once);
//        }

//        // -----------------------------------------------------------------------
//        // MailService — HttpClient throws
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task MailService_ReturnsExceptionMessage_WhenHttpClientThrows()
//        {
//            Environment.SetEnvironmentVariable("NOTIFY_BASEADDRESS", "http://notify.local/");
//            Environment.SetEnvironmentVariable("NOTIFY_URL", "/v2/notifications/email");
//            Environment.SetEnvironmentVariable("EMAIL_TEMPLATEID", "tmpl-abc");

//            var handlerMock = new Mock<HttpMessageHandler>();
//            handlerMock.Protected()
//                .Setup<Task<HttpResponseMessage>>(
//                    "SendAsync",
//                    ItExpr.IsAny<HttpRequestMessage>(),
//                    ItExpr.IsAny<CancellationToken>())
//                .ThrowsAsync(new HttpRequestException("Network failure"));

//            var httpClient = new HttpClient(handlerMock.Object);
//            var service = new TestableAtomDataSelectionEmailJobService(
//                _loggerMock.Object,
//                _hourlyFetchServiceMock.Object,
//                _mongoDbClientFactoryMock.Object,
//                _stationServiceMock.Object,
//                _httpClientFactoryMock.Object,
//                httpClient);

//            var result = await service.MailService("user@test.com", "https://s3.bucket/file.csv");

//            Assert.Equal("Network failure", result);
//            _loggerMock.Verify(
//                x => x.Log(
//                    LogLevel.Error,
//                    It.IsAny<EventId>(),
//                    It.Is<It.IsAnyType>((v, _) => true),
//                    It.IsAny<Exception>(),
//                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
//                Times.Once);
//        }

//        // -----------------------------------------------------------------------
//        // MailService — missing NOTIFY_BASEADDRESS env var
//        // -----------------------------------------------------------------------

//        [Fact]
//        public async Task MailService_ReturnsExceptionMessage_WhenBaseAddressEnvVarMissing()
//        {
//            Environment.SetEnvironmentVariable("NOTIFY_BASEADDRESS", null);
//            Environment.SetEnvironmentVariable("NOTIFY_URL", "/v2/notifications/email");
//            Environment.SetEnvironmentVariable("EMAIL_TEMPLATEID", "tmpl-abc");

//            var service = CreateService();

//            // Production MailService creates HttpClient from env var — will throw on null URI
//            var result = await service.MailService("user@test.com", "https://s3.bucket/file.csv");

//            Assert.NotEqual("Success", result);
//        }

//        // -----------------------------------------------------------------------
//        // Helpers
//        // -----------------------------------------------------------------------

//        private static eMailJobDocument BuildPendingJob(string jobId, string email)
//        {
//            return new eMailJobDocument
//            {
//                JobId = jobId,
//                Email = email,
//                Status = JobStatusEnum.Pending,
//                MailSent = null,
//                PollutantName = "NO2",
//                DataSource = "AURN",
//                Year = "2024",
//                Region = "London",
//                Regiontype = "country",
//                Dataselectorfiltertype = "all",
//                Dataselectordownloadtype = "hourly",
//                CreatedAt = DateTime.UtcNow
//            };
//        }

//        private void SetupFindReturning(List<eMailJobDocument> jobs)
//        {
//            var cursorMock = new Mock<IAsyncCursor<eMailJobDocument>>();
//            cursorMock
//                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
//                .ReturnsAsync(true)
//                .ReturnsAsync(false);
//            cursorMock
//                .Setup(c => c.Current)
//                .Returns(jobs);

//            _collectionMock
//                .Setup(c => c.FindAsync(
//                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
//                    It.IsAny<FindOptions<eMailJobDocument, eMailJobDocument>>(),
//                    It.IsAny<CancellationToken>()))
//                .ReturnsAsync(cursorMock.Object);
//        }

//        private void SetupClaimReturning(eMailJobDocument job)
//        {
//            _collectionMock
//                .Setup(c => c.FindOneAndUpdateAsync(
//                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
//                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
//                    It.IsAny<FindOneAndUpdateOptions<eMailJobDocument>>(),
//                    It.IsAny<CancellationToken>()))
//                .ReturnsAsync(job);
//        }

//        private void SetupUpdateOneSucceeds()
//        {
//            _collectionMock
//                .Setup(c => c.UpdateOneAsync(
//                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
//                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
//                    It.IsAny<UpdateOptions>(),
//                    It.IsAny<CancellationToken>()))
//                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
//        }
//    }
//}