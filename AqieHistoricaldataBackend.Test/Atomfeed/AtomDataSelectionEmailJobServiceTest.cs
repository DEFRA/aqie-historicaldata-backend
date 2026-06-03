using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Utils.Mongo;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using MongoDB.Driver;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    // ---------------------------------------------------------------------------
    // Testable subclass — injects a controllable HttpClient via CreateHttpClient().
    // MailService is no longer shadowed, so ProcessPendingEmailJobsAsync dispatches
    // through the real (virtual) base implementation using the mock transport.
    // ---------------------------------------------------------------------------
    public class TestableAtomDataSelectionEmailJobService : AtomDataSelectionEmailJobService
    {
        private readonly HttpClient _httpClient;

        public TestableAtomDataSelectionEmailJobService(
            ILogger<HistoryexceedenceService> logger,
            IMongoDbClientFactory mongoDbClientFactory,
            IAtomDataSelectionStationService atomDataSelectionStationService,
            HttpClient httpClient)
            : base(logger, mongoDbClientFactory, atomDataSelectionStationService)
        {
            _httpClient = httpClient;
        }

        protected override HttpClient CreateHttpClient() => _httpClient;
    }

    // ---------------------------------------------------------------------------
    // Subclass that forces Getmilisecond to return null so the ms == null branch
    // inside ProcessPendingEmailJobsAsync can be exercised.
    // ---------------------------------------------------------------------------
    public class NullMillisecondEmailJobService : AtomDataSelectionEmailJobService
    {
        public NullMillisecondEmailJobService(
            ILogger<HistoryexceedenceService> logger,
            IMongoDbClientFactory mongoDbClientFactory,
            IAtomDataSelectionStationService atomDataSelectionStationService)
            : base(logger, mongoDbClientFactory, atomDataSelectionStationService) { }

        protected override string? Getmilisecond(DateTime createdAt) => null;
    }

    public class AtomDataSelectionEmailJobServiceTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
        private readonly Mock<IAtomHourlyFetchService> _hourlyFetchServiceMock;
        private readonly Mock<IMongoDbClientFactory> _mongoDbClientFactoryMock;
        private readonly Mock<IAtomDataSelectionStationService> _stationServiceMock;
        private readonly Mock<IMongoCollection<eMailJobDocument>> _collectionMock;
        private readonly Mock<IMongoIndexManager<eMailJobDocument>> _indexManagerMock;

        public AtomDataSelectionEmailJobServiceTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _hourlyFetchServiceMock = new Mock<IAtomHourlyFetchService>();
            _mongoDbClientFactoryMock = new Mock<IMongoDbClientFactory>();
            _stationServiceMock = new Mock<IAtomDataSelectionStationService>();
            _collectionMock = new Mock<IMongoCollection<eMailJobDocument>>();
            _indexManagerMock = new Mock<IMongoIndexManager<eMailJobDocument>>();

            _collectionMock.Setup(c => c.Indexes).Returns(_indexManagerMock.Object);
            _indexManagerMock
                .Setup(i => i.CreateOne(
                    It.IsAny<CreateIndexModel<eMailJobDocument>>(),
                    It.IsAny<CreateOneIndexOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns("index_name");

            _mongoDbClientFactoryMock
                .Setup(f => f.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs"))
                .Returns(_collectionMock.Object);
        }

        // -----------------------------------------------------------------------
        // Factory helpers
        // -----------------------------------------------------------------------

        private AtomDataSelectionEmailJobService CreateService() =>
            new(_loggerMock.Object, _mongoDbClientFactoryMock.Object, _stationServiceMock.Object);

        private TestableAtomDataSelectionEmailJobService CreateTestableService(HttpClient httpClient) =>
            new(_loggerMock.Object, _mongoDbClientFactoryMock.Object, _stationServiceMock.Object, httpClient);

        private NullMillisecondEmailJobService CreateNullMsService() =>
            new(_loggerMock.Object, _mongoDbClientFactoryMock.Object, _stationServiceMock.Object);

        private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content = "")
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content)
                });
            return new HttpClient(handlerMock.Object);
        }

        private static HttpClient CreateThrowingHttpClient(Exception ex)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(ex);
            return new HttpClient(handlerMock.Object);
        }

        private static void SetNotifyEnvVars(
            string baseAddress = "http://notify.local/",
            string url = "/v2/notifications/email",
            string templateId = "tmpl-001")
        {
            Environment.SetEnvironmentVariable("NOTIFY_BASEADDRESS", baseAddress);
            Environment.SetEnvironmentVariable("NOTIFY_URL", url);
            Environment.SetEnvironmentVariable("EMAIL_TEMPLATEID", templateId);
            Environment.SetEnvironmentVariable("EMAIL_BASEADDRESS", "http://frontend.local/");
        }

        // -----------------------------------------------------------------------
        // GetAtomemailjobDataSelection — success path
        // -----------------------------------------------------------------------

        [Fact]
        public async Task GetAtomemailjobDataSelection_ReturnsSuccess_WhenInsertSucceeds()
        {
            _collectionMock
                .Setup(c => c.InsertOneAsync(
                    It.IsAny<eMailJobDocument>(),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = CreateService();
            var data = new QueryStringData
            {
                pollutantName = "NO2",
                dataSource = "AURN",
                Year = "2024",
                Region = "London",
                regiontype = "country",
                dataselectorfiltertype = "all",
                dataselectordownloadtype = "hourly",
                email = "test@example.com"
            };

            var result = await service.GetAtomemailjobDataSelection(data);

            Assert.Equal("Success", result);
            _collectionMock.Verify(
                c => c.InsertOneAsync(
                    It.Is<eMailJobDocument>(d =>
                        d.PollutantName == "NO2" &&
                        d.Email == "test@example.com" &&
                        d.Status == JobStatusEnum.Pending &&
                        d.MailSent == null &&
                        d.StartTime == null),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAtomemailjobDataSelection_SetsAllFieldsFromQueryStringData()
        {
            eMailJobDocument capturedDoc = null!;
            _collectionMock
                .Setup(c => c.InsertOneAsync(
                    It.IsAny<eMailJobDocument>(),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<eMailJobDocument, InsertOneOptions, CancellationToken>(
                    (doc, _, _) => capturedDoc = doc)
                .Returns(Task.CompletedTask);

            var data = new QueryStringData
            {
                pollutantName = "PM10",
                networkId = "NET01",
                dataSource = "AQMS",
                Year = "2023",
                Region = "North East",
                regiontype = "region",
                dataselectorfiltertype = "region",
                dataselectordownloadtype = "daily",
                email = "user@domain.com"
            };

            await CreateService().GetAtomemailjobDataSelection(data);

            Assert.NotNull(capturedDoc);
            Assert.Equal("PM10", capturedDoc.PollutantName);
            Assert.Equal("NET01", capturedDoc.NetworkId);
            Assert.Equal("AQMS", capturedDoc.DataSource);
            Assert.Equal("2023", capturedDoc.Year);
            Assert.Equal("North East", capturedDoc.Region);
            Assert.Equal("region", capturedDoc.Regiontype);
            Assert.Equal("region", capturedDoc.Dataselectorfiltertype);
            Assert.Equal("daily", capturedDoc.Dataselectordownloadtype);
            Assert.Equal("user@domain.com", capturedDoc.Email);
            Assert.Equal(JobStatusEnum.Pending, capturedDoc.Status);
            Assert.NotEmpty(capturedDoc.JobId);
            Assert.Null(capturedDoc.UpdatedAt);
            Assert.Equal(string.Empty, capturedDoc.ErrorReason);
            Assert.Equal(string.Empty, capturedDoc.ResultUrl);
        }

        [Fact]
        public async Task GetAtomemailjobDataSelection_NullQueryStringFields_DefaultToEmptyString()
        {
            eMailJobDocument capturedDoc = null!;
            _collectionMock
                .Setup(c => c.InsertOneAsync(
                    It.IsAny<eMailJobDocument>(),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<eMailJobDocument, InsertOneOptions, CancellationToken>(
                    (doc, _, _) => capturedDoc = doc)
                .Returns(Task.CompletedTask);

            await CreateService().GetAtomemailjobDataSelection(new QueryStringData());

            Assert.Equal(string.Empty, capturedDoc.PollutantName);
            Assert.Equal(string.Empty, capturedDoc.NetworkId);
            Assert.Equal(string.Empty, capturedDoc.DataSource);
            Assert.Equal(string.Empty, capturedDoc.Email);
        }

        [Fact]
        public async Task GetAtomemailjobDataSelection_GeneratesUniqueJobIds()
        {
            var jobIds = new List<string>();
            _collectionMock
                .Setup(c => c.InsertOneAsync(
                    It.IsAny<eMailJobDocument>(),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<eMailJobDocument, InsertOneOptions, CancellationToken>(
                    (doc, _, _) => jobIds.Add(doc.JobId))
                .Returns(Task.CompletedTask);

            var service = CreateService();
            var data = new QueryStringData { email = "a@b.com" };

            await service.GetAtomemailjobDataSelection(data);
            await service.GetAtomemailjobDataSelection(data);

            Assert.Equal(2, jobIds.Count);
            Assert.NotEqual(jobIds[0], jobIds[1]);
        }

        // -----------------------------------------------------------------------
        // GetAtomemailjobDataSelection — failure paths
        // -----------------------------------------------------------------------

        [Fact]
        public async Task GetAtomemailjobDataSelection_ReturnsFailure_WhenInsertThrows()
        {
            _collectionMock
                .Setup(c => c.InsertOneAsync(
                    It.IsAny<eMailJobDocument>(),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Mongo unavailable"));

            var result = await CreateService().GetAtomemailjobDataSelection(new QueryStringData());

            Assert.Equal("Failure", result);
        }

        [Fact]
        public async Task GetAtomemailjobDataSelection_ReturnsFailure_WhenGetCollectionThrows()
        {
            _mongoDbClientFactoryMock
                .Setup(f => f.GetCollection<eMailJobDocument>(It.IsAny<string>()))
                .Throws(new Exception("Connection error"));

            var result = await CreateService().GetAtomemailjobDataSelection(new QueryStringData());

            Assert.Equal("Failure", result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once());
        }

        // -----------------------------------------------------------------------
        // ProcessPendingEmailJobsAsync — no pending jobs
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ProcessPendingEmailJobsAsync_DoesNothing_WhenNoPendingJobs()
        {
            SetupFindReturning([]);

            await CreateService().ProcessPendingEmailJobsAsync(CancellationToken.None);

            _collectionMock.Verify(
                c => c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
                    It.IsAny<FindOneAndUpdateOptions<eMailJobDocument>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // -----------------------------------------------------------------------
        // ProcessPendingEmailJobsAsync — job already claimed
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ProcessPendingEmailJobsAsync_SkipsJob_WhenAlreadyClaimed()
        {
            var job = BuildPendingJob("job-001", "a@b.com");
            SetupFindReturning([job]);

            _collectionMock
                .Setup(c => c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
                    It.IsAny<FindOneAndUpdateOptions<eMailJobDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((eMailJobDocument)null!);

            await CreateService().ProcessPendingEmailJobsAsync(CancellationToken.None);

            _stationServiceMock.Verify(
                s => s.GetAtomDataSelectionStation(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        // -----------------------------------------------------------------------
        // ProcessPendingEmailJobsAsync — successful mail send → Completed
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ProcessPendingEmailJobsAsync_SetsStatusCompleted_WhenMailSucceeds()
        {
            var job = BuildPendingJob("job-002", "success@test.com");
            SetupFindReturning([job]);
            SetupClaimReturning(job);
            SetupStationServiceReturning("https://s3.bucket/file.csv");
            SetupUpdateOneSucceeds();
            SetNotifyEnvVars();

            var service = CreateTestableService(CreateMockHttpClient(HttpStatusCode.OK));
            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

            // endTime update + Completed status update
            _collectionMock.Verify(
                c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        // -----------------------------------------------------------------------
        // ProcessPendingEmailJobsAsync — mail returns non-success → Failed
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ProcessPendingEmailJobsAsync_SetsStatusFailed_WhenMailFails()
        {
            var job = BuildPendingJob("job-003", "fail@test.com");
            SetupFindReturning([job]);
            SetupClaimReturning(job);
            SetupStationServiceReturning("https://s3.bucket/file.csv");
            SetupUpdateOneSucceeds();
            SetNotifyEnvVars();

            var service = CreateTestableService(CreateMockHttpClient(HttpStatusCode.BadRequest, "Bad request error"));
            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

            // endTime update + Failed status update
            _collectionMock.Verify(
                c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        // -----------------------------------------------------------------------
        // ProcessPendingEmailJobsAsync — station service throws → job Failed
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ProcessPendingEmailJobsAsync_SetsJobFailed_WhenStationServiceThrows()
        {
            var job = BuildPendingJob("job-004", "error@test.com");
            SetupFindReturning([job]);
            SetupClaimReturning(job);

            _stationServiceMock
                .Setup(s => s.GetAtomDataSelectionStation(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("S3 error"));

            SetupUpdateOneSucceeds();

            await CreateService().ProcessPendingEmailJobsAsync(CancellationToken.None);

            _collectionMock.Verify(
                c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.Is<UpdateDefinition<eMailJobDocument>>(u => u != null),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // -----------------------------------------------------------------------
        // ProcessPendingEmailJobsAsync — ms == null → job skipped (continue branch)
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ProcessPendingEmailJobsAsync_SkipsMailStep_WhenMillisecondIsNull()
        {
            var job = BuildPendingJob("job-005", "ms@test.com");
            SetupFindReturning([job]);
            SetupClaimReturning(job);
            SetupStationServiceReturning("https://s3.bucket/file.csv");
            SetupUpdateOneSucceeds();

            var service = CreateNullMsService();
            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

            // Only the endTime UpdateOneAsync fires; the MailService step is skipped
            _collectionMock.Verify(
                c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // -----------------------------------------------------------------------
        // ProcessPendingEmailJobsAsync — non-UTC CreatedAt covers Getmilisecond branch
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ProcessPendingEmailJobsAsync_HandlesNonUtcCreatedAt()
        {
            // Local kind triggers the SpecifyKind branch inside Getmilisecond
            var job = BuildPendingJob("job-006", "localtime@test.com",
                createdAt: new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Local));
            SetupFindReturning([job]);
            SetupClaimReturning(job);
            SetupStationServiceReturning("https://s3.bucket/file.csv");
            SetupUpdateOneSucceeds();
            SetNotifyEnvVars();

            var service = CreateTestableService(CreateMockHttpClient(HttpStatusCode.OK));
            // Should complete without exception; the non-UTC branch is covered
            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

            _collectionMock.Verify(
                c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        // -----------------------------------------------------------------------
        // ProcessPendingEmailJobsAsync — outer exception (collection throws)
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ProcessPendingEmailJobsAsync_LogsError_WhenOuterExceptionOccurs()
        {
            _mongoDbClientFactoryMock
                .Setup(f => f.GetCollection<eMailJobDocument>(It.IsAny<string>()))
                .Throws(new Exception("DB unavailable"));

            // Should not propagate — swallowed by the outer catch
            await CreateService().ProcessPendingEmailJobsAsync(CancellationToken.None);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // -----------------------------------------------------------------------
        // ProcessPendingEmailJobsAsync — multiple jobs, all processed
        // -----------------------------------------------------------------------

        [Fact]
        public async Task ProcessPendingEmailJobsAsync_ProcessesAllPendingJobs()
        {
            var job1 = BuildPendingJob("job-010", "a@test.com");
            var job2 = BuildPendingJob("job-011", "b@test.com");
            SetupFindReturning([job1, job2]);

            _collectionMock
                .SetupSequence(c => c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
                    It.IsAny<FindOneAndUpdateOptions<eMailJobDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(job1)
                .ReturnsAsync(job2);

            SetupStationServiceReturning("https://s3.bucket/file.csv");
            SetupUpdateOneSucceeds();
            SetNotifyEnvVars();

            var service = CreateTestableService(CreateMockHttpClient(HttpStatusCode.OK));
            await service.ProcessPendingEmailJobsAsync(CancellationToken.None);

            _stationServiceMock.Verify(
                s => s.GetAtomDataSelectionStation(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Exactly(2));
        }

        // -----------------------------------------------------------------------
        // MailService — NOTIFY_BASEADDRESS null → configuration error
        // -----------------------------------------------------------------------

        [Fact]
        public async Task MailService_ReturnsConfigError_WhenBaseAddressIsNull()
        {
            Environment.SetEnvironmentVariable("NOTIFY_BASEADDRESS", null);

            var result = await CreateService().MailService("user@test.com", "frame/123");

            Assert.Equal("Configuration error: NOTIFY_BASEADDRESS not set", result);
            _loggerMock.Verify(
                x => x.Log(LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task MailService_ReturnsConfigError_WhenBaseAddressIsEmptyString()
        {
            // Covers the IsNullOrEmpty "empty string" sub-branch
            Environment.SetEnvironmentVariable("NOTIFY_BASEADDRESS", "");

            var result = await CreateService().MailService("user@test.com", "frame/123");

            Assert.Equal("Configuration error: NOTIFY_BASEADDRESS not set", result);
        }

        // -----------------------------------------------------------------------
        // MailService — HTTP 2xx → "Success"
        // -----------------------------------------------------------------------

        [Fact]
        public async Task MailService_ReturnsSuccess_WhenHttpResponseIsSuccessful()
        {
            SetNotifyEnvVars();

            var result = await CreateTestableService(CreateMockHttpClient(HttpStatusCode.Created))
                .MailService("user@test.com", "frame/123");

            Assert.Equal("Success", result);
        }

        // -----------------------------------------------------------------------
        // MailService — HTTP non-2xx → error content returned
        // -----------------------------------------------------------------------

        [Fact]
        public async Task MailService_ReturnsErrorContent_WhenHttpResponseIsInternalServerError()
        {
            SetNotifyEnvVars();

            var result = await CreateTestableService(
                    CreateMockHttpClient(HttpStatusCode.InternalServerError, "Internal Server Error"))
                .MailService("user@test.com", "frame/123");

            Assert.Equal("Internal Server Error", result);
            _loggerMock.Verify(
                x => x.Log(LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task MailService_ReturnsErrorContent_WhenHttpResponseIsUnauthorized()
        {
            SetNotifyEnvVars();

            var result = await CreateTestableService(
                    CreateMockHttpClient(HttpStatusCode.Unauthorized, "Unauthorized"))
                .MailService("user@test.com", "frame/123");

            Assert.Equal("Unauthorized", result);
        }

        [Fact]
        public async Task MailService_ReturnsErrorContent_WhenHttpResponseIsBadRequest()
        {
            SetNotifyEnvVars();

            var result = await CreateTestableService(
                    CreateMockHttpClient(HttpStatusCode.BadRequest, "Bad request"))
                .MailService("user@test.com", "frame/123");

            Assert.Equal("Bad request", result);
        }

        // -----------------------------------------------------------------------
        // MailService — HttpClient throws → exception message returned
        // -----------------------------------------------------------------------

        [Fact]
        public async Task MailService_ReturnsExceptionMessage_WhenHttpClientThrows()
        {
            SetNotifyEnvVars();

            var result = await CreateTestableService(
                    CreateThrowingHttpClient(new HttpRequestException("Network failure")))
                .MailService("user@test.com", "frame/123");

            Assert.Equal("Network failure", result);
            _loggerMock.Verify(
                x => x.Log(LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static eMailJobDocument BuildPendingJob(
            string jobId,
            string email,
            DateTime? createdAt = null) =>
            new()
            {
                JobId = jobId,
                Email = email,
                Status = JobStatusEnum.Pending,
                MailSent = null,
                PollutantName = "NO2",
                NetworkId = "NET01",
                DataSource = "AURN",
                Year = "2024",
                Region = "London",
                Regiontype = "country",
                Dataselectorfiltertype = "all",
                Dataselectordownloadtype = "hourly",
                CreatedAt = createdAt ?? DateTime.UtcNow
            };

        private void SetupFindReturning(List<eMailJobDocument> jobs)
        {
            var cursorMock = new Mock<IAsyncCursor<eMailJobDocument>>();
            cursorMock
                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            cursorMock
                .Setup(c => c.Current)
                .Returns(jobs);

            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<FindOptions<eMailJobDocument, eMailJobDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(cursorMock.Object);
        }

        private void SetupClaimReturning(eMailJobDocument job)
        {
            _collectionMock
                .Setup(c => c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
                    It.IsAny<FindOneAndUpdateOptions<eMailJobDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(job);
        }

        private void SetupStationServiceReturning(string url)
        {
            _stationServiceMock
                .Setup(s => s.GetAtomDataSelectionStation(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(url);
        }

        private void SetupUpdateOneSucceeds()
        {
            _collectionMock
                .Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateDefinition<eMailJobDocument>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));
        }
    }
}