using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Utils.Mongo;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Driver;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionJobStatusTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
        private readonly Mock<IAtomHourlyFetchService> _hourlyFetchServiceMock;
        private readonly Mock<IAtomDataSelectionStationService> _stationServiceMock;
        private readonly Mock<IMongoDbClientFactory> _mongoFactoryMock;
        private readonly Mock<IMongoCollection<JobDocument>> _collectionMock;
        private readonly Mock<IMongoIndexManager<JobDocument>> _indexManagerMock;
        private readonly AtomDataSelectionJobStatus _sut;

        public AtomDataSelectionJobStatusTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _hourlyFetchServiceMock = new Mock<IAtomHourlyFetchService>();
            _stationServiceMock = new Mock<IAtomDataSelectionStationService>();
            _mongoFactoryMock = new Mock<IMongoDbClientFactory>();
            _collectionMock = new Mock<IMongoCollection<JobDocument>>();
            _indexManagerMock = new Mock<IMongoIndexManager<JobDocument>>();

            // Wire up index manager on the collection mock
            _collectionMock
                .Setup(c => c.Indexes)
                .Returns(_indexManagerMock.Object);

            _mongoFactoryMock
                .Setup(f => f.GetCollection<JobDocument>("aqie_csvexport_jobs"))
                .Returns(_collectionMock.Object);

            _sut = new AtomDataSelectionJobStatus(
                _loggerMock.Object,
                _hourlyFetchServiceMock.Object,
                _stationServiceMock.Object,
                _mongoFactoryMock.Object);
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Configures the collection mock so that Find().FirstOrDefaultAsync() returns
        /// <paramref name="document"/>.
        /// </summary>
        private void SetupFindReturns(JobDocument? document)
        {
            var cursorMock = new Mock<IAsyncCursor<JobDocument>>();
            cursorMock
                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(document != null)
                .ReturnsAsync(false);
            cursorMock
                .Setup(c => c.Current)
                .Returns(document != null ? new List<JobDocument> { document } : new List<JobDocument>());

            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<JobDocument>>(),
                    It.IsAny<FindOptions<JobDocument, JobDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(cursorMock.Object);
        }

        /// <summary>
        /// Configures the collection mock so that Find().FirstOrDefaultAsync() throws
        /// <paramref name="exception"/>.
        /// </summary>
        private void SetupFindThrows(Exception exception)
        {
            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<JobDocument>>(),
                    It.IsAny<FindOptions<JobDocument, JobDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
        }

        private static JobDocument BuildJobDocument(
            string jobId = "abc123",
            JobStatusEnum status = JobStatusEnum.Completed,
            string? resultUrl = "https://s3.example.com/result.csv",
            string? errorReason = null)
        {
            return new JobDocument
            {
                JobId = jobId,
                Status = status,
                ResultUrl = resultUrl,
                ErrorReason = errorReason,
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                StartTime = new DateTime(2025, 1, 1, 1, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2025, 1, 1, 2, 0, 0, DateTimeKind.Utc)
            };
        }

        // ─── Null / whitespace / empty guard tests ───────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_ReturnsNull_WhenJobIdIsNull()
        {
            var result = await _sut.GetAtomDataSelectionJobStatusdata(null!);

            Assert.Null(result);
            _mongoFactoryMock.Verify(f => f.GetCollection<JobDocument>(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_ReturnsNull_WhenJobIdIsEmpty()
        {
            var result = await _sut.GetAtomDataSelectionJobStatusdata(string.Empty);

            Assert.Null(result);
            _mongoFactoryMock.Verify(f => f.GetCollection<JobDocument>(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_ReturnsNull_WhenJobIdIsWhitespace()
        {
            var result = await _sut.GetAtomDataSelectionJobStatusdata("   ");

            Assert.Null(result);
            _mongoFactoryMock.Verify(f => f.GetCollection<JobDocument>(It.IsAny<string>()), Times.Never);
        }

        // ─── Document not found ──────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_ReturnsNull_WhenDocumentNotFound()
        {
            SetupFindReturns(null);

            var result = await _sut.GetAtomDataSelectionJobStatusdata("non-existent-job");

            Assert.Null(result);
        }

        // ─── Happy-path: document found, all fields mapped correctly ─────────────

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_ReturnsCorrectDto_WhenDocumentFound()
        {
            var doc = BuildJobDocument();
            SetupFindReturns(doc);

            var result = await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            Assert.NotNull(result);
            Assert.Equal(doc.JobId, result!.JobId);
            Assert.Equal(doc.Status.ToString(), result.Status);
            Assert.Equal(doc.ResultUrl, result.ResultUrl);
            Assert.Equal(doc.ErrorReason, result.ErrorReason);
            Assert.Equal(doc.CreatedAt, result.CreatedAt);
            Assert.Equal(doc.UpdatedAt, result.UpdatedAt);
            Assert.Equal(doc.StartTime, result.StartTime);
            Assert.Equal(doc.EndTime, result.EndTime);
        }

        [Theory]
        [InlineData(JobStatusEnum.Pending)]
        [InlineData(JobStatusEnum.Processing)]
        [InlineData(JobStatusEnum.Completed)]
        [InlineData(JobStatusEnum.Failed)]
        public async Task GetAtomDataSelectionJobStatusdata_MapsStatusEnumToString_ForAllStatuses(
            JobStatusEnum status)
        {
            var doc = BuildJobDocument(status: status);
            SetupFindReturns(doc);

            var result = await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            Assert.NotNull(result);
            Assert.Equal(status.ToString(), result!.Status);
        }

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_ReturnsDto_WhenOptionalFieldsAreNull()
        {
            var doc = BuildJobDocument(resultUrl: null, errorReason: null);
            doc.UpdatedAt = null;
            doc.EndTime = null;
            SetupFindReturns(doc);

            var result = await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            Assert.NotNull(result);
            Assert.Null(result!.ResultUrl);
            Assert.Null(result.ErrorReason);
            Assert.Null(result.UpdatedAt);
            Assert.Null(result.EndTime);
        }

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_ReturnsDto_WhenJobHasErrorReason()
        {
            var doc = BuildJobDocument(
                status: JobStatusEnum.Failed,
                resultUrl: null,
                errorReason: "Upstream timeout");
            SetupFindReturns(doc);

            var result = await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            Assert.NotNull(result);
            Assert.Equal("Failed", result!.Status);
            Assert.Equal("Upstream timeout", result.ErrorReason);
            Assert.Null(result.ResultUrl);
        }

        // ─── Lazy-initialisation: collection fetched once per instance ───────────

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_InitialisesCollection_OnFirstCall()
        {
            SetupFindReturns(BuildJobDocument());

            await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            _mongoFactoryMock.Verify(
                f => f.GetCollection<JobDocument>("aqie_csvexport_jobs"),
                Times.Once);
        }

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_DoesNotReinitialiseCollection_OnSubsequentCalls()
        {
            SetupFindReturns(BuildJobDocument());

            await _sut.GetAtomDataSelectionJobStatusdata("abc123");
            await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            _mongoFactoryMock.Verify(
                f => f.GetCollection<JobDocument>("aqie_csvexport_jobs"),
                Times.Once);
        }

        // ─── Index creation ───────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_AttemptsToCreateIndex_OnFirstCall()
        {
            SetupFindReturns(BuildJobDocument());

            await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            _indexManagerMock.Verify(
                i => i.CreateOne(
                    It.IsAny<CreateIndexModel<JobDocument>>(),
                    It.IsAny<CreateOneIndexOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_ContinuesAndReturnsDto_WhenIndexCreationThrows()
        {
            _indexManagerMock
                .Setup(i => i.CreateOne(
                    It.IsAny<CreateIndexModel<JobDocument>>(),
                    It.IsAny<CreateOneIndexOptions>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new MongoException("Index already exists"));

            SetupFindReturns(BuildJobDocument());

            // Should NOT throw; the catch block logs a warning and continues
            var result = await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_LogsWarning_WhenIndexCreationThrows()
        {
            _indexManagerMock
                .Setup(i => i.CreateOne(
                    It.IsAny<CreateIndexModel<JobDocument>>(),
                    It.IsAny<CreateOneIndexOptions>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new MongoException("Index conflict"));

            SetupFindReturns(BuildJobDocument());

            await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ─── Exception / error handling ──────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_ReturnsNull_WhenFindThrows()
        {
            SetupFindThrows(new Exception("Mongo connection lost"));

            var result = await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_LogsTwoErrors_WhenFindThrows()
        {
            SetupFindThrows(new Exception("Mongo error"));

            await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_ReturnsNull_WhenGetCollectionThrows()
        {
            _mongoFactoryMock
                .Setup(f => f.GetCollection<JobDocument>(It.IsAny<string>()))
                .Throws(new InvalidOperationException("Factory error"));

            var result = await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_LogsTwoErrors_WhenGetCollectionThrows()
        {
            _mongoFactoryMock
                .Setup(f => f.GetCollection<JobDocument>(It.IsAny<string>()))
                .Throws(new InvalidOperationException("Factory error"));

            await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2));
        }

        // ─── Correct MongoDB collection name used ─────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionJobStatusdata_UsesCorrectCollectionName()
        {
            SetupFindReturns(BuildJobDocument());

            await _sut.GetAtomDataSelectionJobStatusdata("abc123");

            _mongoFactoryMock.Verify(
                f => f.GetCollection<JobDocument>("aqie_csvexport_jobs"),
                Times.Once);
        }
    }
}