using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Utils.Mongo;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Driver;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionPresignedUrlMailTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
        private readonly Mock<IMongoDbClientFactory> _mongoFactoryMock;
        private readonly Mock<IAWSPreSignedURLService> _awsPreSignedURLServiceMock;
        private readonly Mock<IMongoCollection<eMailJobDocument>> _collectionMock;
        private readonly Mock<IMongoIndexManager<eMailJobDocument>> _indexManagerMock;
        private readonly AtomDataSelectionPresignedUrlMail _sut;

        public AtomDataSelectionPresignedUrlMailTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _mongoFactoryMock = new Mock<IMongoDbClientFactory>();
            _awsPreSignedURLServiceMock = new Mock<IAWSPreSignedURLService>();
            _collectionMock = new Mock<IMongoCollection<eMailJobDocument>>();
            _indexManagerMock = new Mock<IMongoIndexManager<eMailJobDocument>>();

            _collectionMock
                .Setup(c => c.Indexes)
                .Returns(_indexManagerMock.Object);

            _mongoFactoryMock
                .Setup(f => f.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs"))
                .Returns(_collectionMock.Object);

            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", "test-bucket");

            _sut = new AtomDataSelectionPresignedUrlMail(
                _loggerMock.Object,
                _mongoFactoryMock.Object,
                _awsPreSignedURLServiceMock.Object);
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Configures the collection mock so that every call to FindAsync returns
        /// a fresh cursor that yields <paramref name="document"/> (or nothing when null).
        /// Using a factory delegate ensures repeated calls each get a new cursor.
        /// </summary>
        private void SetupFindReturns(eMailJobDocument? document)
        {
            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<FindOptions<eMailJobDocument, eMailJobDocument>>(),
                    It.IsAny<CancellationToken>()))
                .Returns<FilterDefinition<eMailJobDocument>,
                         FindOptions<eMailJobDocument, eMailJobDocument>,
                         CancellationToken>((_, _, _) =>
                {
                    var cursorMock = new Mock<IAsyncCursor<eMailJobDocument>>();
                    cursorMock
                        .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(document != null)
                        .ReturnsAsync(false);
                    cursorMock
                        .Setup(c => c.Current)
                        .Returns(document != null
                            ? new List<eMailJobDocument> { document }
                            : new List<eMailJobDocument>());
                    return Task.FromResult(cursorMock.Object);
                });
        }

        /// <summary>
        /// Configures FindAsync to throw <paramref name="exception"/> on every call.
        /// </summary>
        private void SetupFindThrows(Exception exception)
        {
            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<eMailJobDocument>>(),
                    It.IsAny<FindOptions<eMailJobDocument, eMailJobDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
        }

        private static eMailJobDocument BuildEmailJobDocument(
            string jobId = "job-001",
            string dataSource = "AURN",
            string pollutantName = "NO2",
            string region = "London",
            string year = "2024")
        {
            return new eMailJobDocument
            {
                JobId = jobId,
                DataSource = dataSource,
                PollutantName = pollutantName,
                Region = region,
                Year = year,
                Status = JobStatusEnum.Pending,
                Email = "test@example.com",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };
        }

        // ─── Guard: null / empty / whitespace jobId ──────────────────────────────

        [Fact]
        public async Task GetPresignedUrlMail_ReturnsNull_WhenJobIdIsNull()
        {
            var result = await _sut.GetPresignedUrlMail(null!);

            Assert.Null(result);
            _mongoFactoryMock.Verify(f => f.GetCollection<eMailJobDocument>(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetPresignedUrlMail_ReturnsNull_WhenJobIdIsEmpty()
        {
            var result = await _sut.GetPresignedUrlMail(string.Empty);

            Assert.Null(result);
            _mongoFactoryMock.Verify(f => f.GetCollection<eMailJobDocument>(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetPresignedUrlMail_ReturnsNull_WhenJobIdIsWhitespace()
        {
            var result = await _sut.GetPresignedUrlMail("   ");

            Assert.Null(result);
            _mongoFactoryMock.Verify(f => f.GetCollection<eMailJobDocument>(It.IsAny<string>()), Times.Never);
        }

        // ─── Lazy collection init: index creation succeeds ───────────────────────

        [Fact]
        public async Task GetPresignedUrlMail_ReturnsNull_WhenDocumentNotFound()
        {
            SetupFindReturns(null);

            var result = await _sut.GetPresignedUrlMail("non-existent-job");

            Assert.Null(result);
            _mongoFactoryMock.Verify(f => f.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs"), Times.Once);
        }

        // ─── Lazy collection init: index creation throws → warning logged ────────

        [Fact]
        public async Task GetPresignedUrlMail_LogsWarning_WhenIndexCreationFails()
        {
            _indexManagerMock
                .Setup(i => i.CreateOne(
                    It.IsAny<CreateIndexModel<eMailJobDocument>>(),
                    It.IsAny<CreateOneIndexOptions>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new Exception("Index creation failed"));

            SetupFindReturns(null);

            // Should not throw; continues despite index failure and returns null (no document)
            var result = await _sut.GetPresignedUrlMail("job-001");

            Assert.Null(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ─── Lazy collection init: already initialised on second call ────────────

        [Fact]
        public async Task GetPresignedUrlMail_DoesNotReinitialiseCollection_OnSubsequentCalls()
        {
            SetupFindReturns(null);

            await _sut.GetPresignedUrlMail("job-001");
            await _sut.GetPresignedUrlMail("job-002");

            // GetCollection must only be called once across both invocations
            _mongoFactoryMock.Verify(f => f.GetCollection<eMailJobDocument>("aqie_csvemailexport_jobs"), Times.Once);
        }

        // ─── GetS3data: GeneratePreSignedURL returns null ────────────────────────

        [Fact]
        public async Task GetPresignedUrlMail_ReturnsNull_WhenS3ReturnsNull()
        {
            var doc = BuildEmailJobDocument();
            SetupFindReturns(doc);
            _awsPreSignedURLServiceMock
                .Setup(s => s.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>()))
                .ReturnsAsync((string?)null);

            var result = await _sut.GetPresignedUrlMail(doc.JobId);

            Assert.Null(result);
        }

        // ─── GetS3data: AmazonS3Exception ────────────────────────────────────────

        [Fact]
        public async Task GetPresignedUrlMail_ReturnsNull_WhenS3ThrowsAmazonS3Exception()
        {
            var doc = BuildEmailJobDocument();
            SetupFindReturns(doc);
            _awsPreSignedURLServiceMock
                .Setup(s => s.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>()))
                .ThrowsAsync(new AmazonS3Exception("S3 error"));

            var result = await _sut.GetPresignedUrlMail(doc.JobId);

            Assert.Null(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<AmazonS3Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ─── GetS3data: generic Exception ────────────────────────────────────────

        [Fact]
        public async Task GetPresignedUrlMail_ReturnsNull_WhenS3ThrowsGenericException()
        {
            var doc = BuildEmailJobDocument();
            SetupFindReturns(doc);
            _awsPreSignedURLServiceMock
                .Setup(s => s.GeneratePreSignedURL(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>()))
                .ThrowsAsync(new Exception("Unexpected S3 failure"));

            var result = await _sut.GetPresignedUrlMail(doc.JobId);

            Assert.Null(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        // ─── Happy path ──────────────────────────────────────────────────────────

        [Fact]
        public async Task GetPresignedUrlMail_ReturnsPresignedUrl_WhenAllSucceeds()
        {
            const string expectedUrl = "https://s3.amazonaws.com/test-bucket/AURN_NO2_London_2024.zip?sig=abc";
            var doc = BuildEmailJobDocument();
            SetupFindReturns(doc);
            _awsPreSignedURLServiceMock
                .Setup(s => s.GeneratePreSignedURL("test-bucket", "AURN_NO2_London_2024.zip", 604800))
                .ReturnsAsync(expectedUrl);

            var result = await _sut.GetPresignedUrlMail(doc.JobId);

            Assert.Equal(expectedUrl, result);
            _collectionMock.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<eMailJobDocument>>(),
                It.IsAny<UpdateDefinition<eMailJobDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ─── S3 key format ───────────────────────────────────────────────────────

        [Theory]
        [InlineData("AURN", "NO2",   "London",    "2024", "AURN_NO2_London_2024.zip")]
        [InlineData("LAQN", "PM2.5", "Manchester", "2023", "LAQN_PM2.5_Manchester_2023.zip")]
        [InlineData("AQE",  "O3",    "Bristol",   "2022", "AQE_O3_Bristol_2022.zip")]
        public async Task GetPresignedUrlMail_ConstructsCorrectS3Key(
            string dataSource, string pollutant, string region, string year, string expectedKey)
        {
            const string url = "https://example.com/file.zip";
            var doc = BuildEmailJobDocument(dataSource: dataSource, pollutantName: pollutant, region: region, year: year);
            SetupFindReturns(doc);
            _awsPreSignedURLServiceMock
                .Setup(s => s.GeneratePreSignedURL("test-bucket", expectedKey, 604800))
                .ReturnsAsync(url);

            var result = await _sut.GetPresignedUrlMail(doc.JobId);

            Assert.Equal(url, result);
            _awsPreSignedURLServiceMock.Verify(
                s => s.GeneratePreSignedURL("test-bucket", expectedKey, 604800), Times.Once);
        }

        // ─── Outer catch: FindAsync throws → catch updates document, returns null ─

        [Fact]
        public async Task GetPresignedUrlMail_ReturnsNull_WhenMongoFindThrows()
        {
            SetupFindThrows(new Exception("Mongo connection failure"));

            var result = await _sut.GetPresignedUrlMail("job-001");

            Assert.Null(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            _collectionMock.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<eMailJobDocument>>(),
                It.IsAny<UpdateDefinition<eMailJobDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}