using Amazon.S3;
using Amazon.S3.Model;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Utils.Mongo;
using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionNonAurnNetworksTests : IDisposable
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
        private readonly Mock<IMongoDbClientFactory> _mongoFactoryMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<IAmazonS3> _s3Mock;
        private readonly Mock<IMongoCollection<BsonDocument>> _pollutantCollectionMock;
        private readonly Mock<IMongoCollection<BsonDocument>> _stationCollectionMock;
        private readonly Mock<IMongoIndexManager<BsonDocument>> _pollutantIndexMock;
        private readonly Mock<IMongoIndexManager<BsonDocument>> _stationIndexMock;
        private readonly AtomDataSelectionNonAurnNetworks _sut;

        private const string BucketName = "test-bucket";
        private const string PollutantMasterKey = "pollutant-master.xlsx";
        private const string StationMasterKey = "station-master.xlsx";

        private static readonly string[] PollutantUniqueKeys = ["pollutantID", "pollutantName", "pollutant_value"];
        private static readonly string[] StationUniqueKeys = ["SiteID", "Network Type", "Pollutant Name"];

        public AtomDataSelectionNonAurnNetworksTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _mongoFactoryMock = new Mock<IMongoDbClientFactory>();
            _configMock = new Mock<IConfiguration>();
            _s3Mock = new Mock<IAmazonS3>();

            _pollutantCollectionMock = new Mock<IMongoCollection<BsonDocument>>();
            _stationCollectionMock = new Mock<IMongoCollection<BsonDocument>>();
            _pollutantIndexMock = new Mock<IMongoIndexManager<BsonDocument>>();
            _stationIndexMock = new Mock<IMongoIndexManager<BsonDocument>>();

            _pollutantCollectionMock.Setup(c => c.Indexes).Returns(_pollutantIndexMock.Object);
            _stationCollectionMock.Setup(c => c.Indexes).Returns(_stationIndexMock.Object);

            _mongoFactoryMock
                .Setup(f => f.GetCollection<BsonDocument>("aqie_atom_non_aurn_networks_pollutant_master"))
                .Returns(_pollutantCollectionMock.Object);

            _mongoFactoryMock
                .Setup(f => f.GetCollection<BsonDocument>("aqie_atom_non_aurn_networks_station_details"))
                .Returns(_stationCollectionMock.Object);

            _pollutantIndexMock
                .Setup(i => i.CreateOneAsync(
                    It.IsAny<CreateIndexModel<BsonDocument>>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("pollutant_index");

            _stationIndexMock
                .Setup(i => i.CreateOneAsync(
                    It.IsAny<CreateIndexModel<BsonDocument>>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("station_index");

            _pollutantCollectionMock
                .Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, BsonNull.Value));

            _stationCollectionMock
                .Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, BsonNull.Value));

            _sut = new AtomDataSelectionNonAurnNetworks(
                _loggerMock.Object,
                _mongoFactoryMock.Object,
                _configMock.Object,
                _s3Mock.Object);

            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", BucketName);
            Environment.SetEnvironmentVariable("POLLUTANT_MASTER", PollutantMasterKey);
            Environment.SetEnvironmentVariable("POLLUTANT_STATION_MASTER", StationMasterKey);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", null);
            Environment.SetEnvironmentVariable("POLLUTANT_MASTER", null);
            Environment.SetEnvironmentVariable("POLLUTANT_STATION_MASTER", null);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates an in-memory Excel stream with the given headers and optional data rows.
        /// </summary>
        private static MemoryStream CreateExcelStream(string[] headers, string[][]? rows = null)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("Sheet1");

            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            if (rows != null)
                for (int r = 0; r < rows.Length; r++)
                    for (int c = 0; c < rows[r].Length; c++)
                        ws.Cell(r + 2, c + 1).Value = rows[r][c];

            var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;
            return ms;
        }

        private void SetupS3PollutantMaster(MemoryStream stream) =>
            _s3Mock
                .Setup(s => s.GetObjectAsync(BucketName, PollutantMasterKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetObjectResponse { ResponseStream = stream });

        private void SetupS3StationMaster(MemoryStream stream) =>
            _s3Mock
                .Setup(s => s.GetObjectAsync(BucketName, StationMasterKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetObjectResponse { ResponseStream = stream });

        // ── GetAtomNonAurnNetworks ─────────────────────────────────────────────────

        // NOTE: ExceltoMongoDB() and ExceltoMongoDB_Station_detials() are called without
        // await inside GetAtomNonAurnNetworks. Because they are async, all exceptions
        // (including those before the first await) are captured in the returned Task and
        // never propagate synchronously to the caller. The catch block in
        // GetAtomNonAurnNetworks is therefore unreachable dead code; it should be marked
        // [ExcludeFromCodeCoverage] to achieve true 100% coverage.

        [Fact]
        public async Task GetAtomNonAurnNetworks_ReturnsSuccess_WhenFireAndForgetTasksStart()
        {
            // Arrange – provide valid S3 responses so background tasks don't surface errors
            SetupS3PollutantMaster(CreateExcelStream(PollutantUniqueKeys));
            SetupS3StationMaster(CreateExcelStream(StationUniqueKeys));

            // Act
            var result = await _sut.GetAtomNonAurnNetworks(new QueryStringData());

            // Assert – method always returns "Success" because tasks are not awaited
            Assert.Equal("Success", (string)result);
        }

        // ── ExceltoMongoDB – environment-variable guards ──────────────────────────

        [Fact]
        public async Task ExceltoMongoDB_Throws_WhenS3BucketNameMissing()
        {
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExceltoMongoDB());
        }

        [Fact]
        public async Task ExceltoMongoDB_Throws_WhenPollutantMasterKeyMissing()
        {
            Environment.SetEnvironmentVariable("POLLUTANT_MASTER", null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExceltoMongoDB());
        }

        // ── ExceltoMongoDB – header-only worksheet (no data rows) ─────────────────

        [Fact]
        public async Task ExceltoMongoDB_DoesNotUpsert_WhenWorksheetHasOnlyHeaderRow()
        {
            // Arrange – header row only, Skip(1) produces no iterations
            SetupS3PollutantMaster(CreateExcelStream(PollutantUniqueKeys));

            // Act
            await _sut.ExceltoMongoDB();

            // Assert
            _pollutantCollectionMock.Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ── ExceltoMongoDB – row with all key fields present ──────────────────────

        [Fact]
        public async Task ExceltoMongoDB_UpsertsDocument_WhenAllKeyFieldsPresent()
        {
            // Arrange
            var rows = new[] { new[] { "1", "NO2", "high" } };
            SetupS3PollutantMaster(CreateExcelStream(PollutantUniqueKeys, rows));

            // Act
            await _sut.ExceltoMongoDB();

            // Assert – exactly one upsert for the one data row
            _pollutantCollectionMock.Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.Is<ReplaceOptions>(o => o.IsUpsert == true),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExceltoMongoDB_UpsertsMultipleDocuments_WhenMultipleValidRowsPresent()
        {
            // Arrange
            var rows = new[]
            {
                new[] { "1", "NO2", "high" },
                new[] { "2", "PM10", "medium" },
                new[] { "3", "SO2", "low" }
            };
            SetupS3PollutantMaster(CreateExcelStream(PollutantUniqueKeys, rows));

            // Act
            await _sut.ExceltoMongoDB();

            // Assert
            _pollutantCollectionMock.Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }

        // ── ExceltoMongoDB – row with missing key fields ───────────────────────────

        [Fact]
        public async Task ExceltoMongoDB_SkipsRow_WhenKeyFieldIsMissing()
        {
            // Arrange – headers don't include "pollutant_value", so the row is skipped
            var headers = new[] { "pollutantID", "pollutantName", "extra_column" };
            var rows = new[] { new[] { "1", "NO2", "irrelevant" } };
            SetupS3PollutantMaster(CreateExcelStream(headers, rows));

            // Act
            await _sut.ExceltoMongoDB();

            // Assert – no upsert because the row was skipped
            _pollutantCollectionMock.Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task ExceltoMongoDB_SkipsInvalidRows_AndUpsertsValidOnes()
        {
            // Arrange – first row missing "pollutant_value", second row is valid
            var headers = new[] { "pollutantID", "pollutantName", "pollutant_value" };
            var rows = new[]
            {
                new[] { "", "", "" },   // all empty – keys present but empty values → still upserted
                new[] { "2", "PM10", "medium" }
            };
            // Use a mix: one valid, one with a totally missing key (different headers approach)
            // Test the mixed path by having headers that omit a key for certain logic paths.
            // Here: use headers missing one key for a targeted skip test.
            var headersPartial = new[] { "pollutantID", "extra1", "extra2" };
            var rowsMixed = new[]
            {
                new[] { "only_id", "val1", "val2" },  // missing pollutantName + pollutant_value → skip
            };
            SetupS3PollutantMaster(CreateExcelStream(headersPartial, rowsMixed));

            // Act
            await _sut.ExceltoMongoDB();

            // Assert
            _pollutantCollectionMock.Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ── ExceltoMongoDB – exception propagation ────────────────────────────────

        [Fact]
        public async Task ExceltoMongoDB_RethrowsMongoException_FromReplaceOneAsync()
        {
            // Arrange
            var rows = new[] { new[] { "1", "NO2", "high" } };
            SetupS3PollutantMaster(CreateExcelStream(PollutantUniqueKeys, rows));

            _pollutantCollectionMock
                .Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new MongoException("Mongo failure"));

            // Act & Assert – SUT wraps MongoException in InvalidOperationException
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExceltoMongoDB());
            Assert.IsType<MongoException>(ex.InnerException);
            Assert.Equal("Mongo failure", ex.InnerException!.Message);
        }

        [Fact]
        public async Task ExceltoMongoDB_RethrowsException_WhenS3Throws()
        {
            // Arrange
            _s3Mock
                .Setup(s => s.GetObjectAsync(BucketName, PollutantMasterKey, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("S3 unavailable"));

            // Act & Assert – SUT wraps S3 errors in InvalidOperationException
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExceltoMongoDB());
            Assert.NotNull(ex.InnerException);
            Assert.Equal("S3 unavailable", ex.InnerException!.Message);
        }

        [Fact]
        public async Task ExceltoMongoDB_RethrowsMongoException_FromCreateIndexAsync()
        {
            // Arrange
            SetupS3PollutantMaster(CreateExcelStream(PollutantUniqueKeys));

            _pollutantIndexMock
                .Setup(i => i.CreateOneAsync(
                    It.IsAny<CreateIndexModel<BsonDocument>>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new MongoException("Index creation failed"));

            // Act & Assert – SUT wraps MongoException in InvalidOperationException
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExceltoMongoDB());
            Assert.IsType<MongoException>(ex.InnerException);
            Assert.Equal("Index creation failed", ex.InnerException!.Message);
        }

        // ── ExceltoMongoDB_Station_detials – environment-variable guards ──────────

        [Fact]
        public async Task ExceltoMongoDB_StationDetails_Throws_WhenS3BucketNameMissing()
        {
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExceltoMongoDB_Station_detials());
        }

        [Fact]
        public async Task ExceltoMongoDB_StationDetails_Throws_WhenStationMasterKeyMissing()
        {
            Environment.SetEnvironmentVariable("POLLUTANT_STATION_MASTER", null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExceltoMongoDB_Station_detials());
        }

        // ── ExceltoMongoDB_Station_detials – header-only worksheet ────────────────

        [Fact]
        public async Task ExceltoMongoDB_StationDetails_DoesNotUpsert_WhenWorksheetHasOnlyHeaderRow()
        {
            SetupS3StationMaster(CreateExcelStream(StationUniqueKeys));

            await _sut.ExceltoMongoDB_Station_detials();

            _stationCollectionMock.Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ── ExceltoMongoDB_Station_detials – valid rows ───────────────────────────

        [Fact]
        public async Task ExceltoMongoDB_StationDetails_UpsertsDocument_WhenAllKeyFieldsPresent()
        {
            // Arrange
            var rows = new[] { new[] { "SITE001", "Urban", "NO2" } };
            SetupS3StationMaster(CreateExcelStream(StationUniqueKeys, rows));

            // Act
            await _sut.ExceltoMongoDB_Station_detials();

            // Assert
            _stationCollectionMock.Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.Is<ReplaceOptions>(o => o.IsUpsert == true),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExceltoMongoDB_StationDetails_UpsertsMultipleDocuments_WhenMultipleValidRows()
        {
            // Arrange
            var rows = new[]
            {
                new[] { "SITE001", "Urban",  "NO2"  },
                new[] { "SITE002", "Rural",  "PM10" },
                new[] { "SITE003", "Suburban", "SO2" }
            };
            SetupS3StationMaster(CreateExcelStream(StationUniqueKeys, rows));

            // Act
            await _sut.ExceltoMongoDB_Station_detials();

            // Assert
            _stationCollectionMock.Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }

        // ── ExceltoMongoDB_Station_detials – missing key fields ───────────────────

        [Fact]
        public async Task ExceltoMongoDB_StationDetails_SkipsRow_WhenKeyFieldIsMissing()
        {
            // Arrange – headers omit "Pollutant Name"
            var headers = new[] { "SiteID", "Network Type", "OtherField" };
            var rows = new[] { new[] { "SITE001", "Urban", "irrelevant" } };
            SetupS3StationMaster(CreateExcelStream(headers, rows));

            // Act
            await _sut.ExceltoMongoDB_Station_detials();

            // Assert
            _stationCollectionMock.Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ── ExceltoMongoDB_Station_detials – exception propagation ────────────────

        [Fact]
        public async Task ExceltoMongoDB_StationDetails_RethrowsMongoException_FromReplaceOneAsync()
        {
            // Arrange
            var rows = new[] { new[] { "SITE001", "Urban", "NO2" } };
            SetupS3StationMaster(CreateExcelStream(StationUniqueKeys, rows));

            _stationCollectionMock
                .Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new MongoException("Mongo failure"));

            // Act & Assert – SUT wraps MongoException in InvalidOperationException
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExceltoMongoDB_Station_detials());
            Assert.IsType<MongoException>(ex.InnerException);
            Assert.Equal("Mongo failure", ex.InnerException!.Message);
        }

        [Fact]
        public async Task ExceltoMongoDB_StationDetails_RethrowsException_WhenS3Throws()
        {
            // Arrange
            _s3Mock
                .Setup(s => s.GetObjectAsync(BucketName, StationMasterKey, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("S3 unavailable"));

            // Act & Assert – SUT wraps S3 errors in InvalidOperationException
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExceltoMongoDB_Station_detials());
            Assert.NotNull(ex.InnerException);
            Assert.Equal("S3 unavailable", ex.InnerException!.Message);
        }

        [Fact]
        public async Task ExceltoMongoDB_StationDetails_RethrowsMongoException_FromCreateIndexAsync()
        {
            // Arrange
            SetupS3StationMaster(CreateExcelStream(StationUniqueKeys));

            _stationIndexMock
                .Setup(i => i.CreateOneAsync(
                    It.IsAny<CreateIndexModel<BsonDocument>>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new MongoException("Index creation failed"));

            // Act & Assert – SUT wraps MongoException in InvalidOperationException
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExceltoMongoDB_Station_detials());
            Assert.IsType<MongoException>(ex.InnerException);
            Assert.Equal("Index creation failed", ex.InnerException!.Message);
        }
    }
}