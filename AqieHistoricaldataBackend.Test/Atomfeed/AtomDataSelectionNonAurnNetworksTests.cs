using Amazon.S3;
using Amazon.S3.Model;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Utils.Mongo;
using ClosedXML.Excel;
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
        private readonly Mock<IAmazonS3> _s3Mock;
        private readonly Mock<IMongoCollection<BsonDocument>> _pollutantCollectionMock;
        private readonly Mock<IMongoCollection<BsonDocument>> _stationCollectionMock;
        private readonly Mock<IMongoIndexManager<BsonDocument>> _pollutantIndexMock;
        private readonly Mock<IMongoIndexManager<BsonDocument>> _stationIndexMock;
        private readonly Mock<IMongoDatabase> _pollutantDatabaseMock;
        private readonly Mock<IMongoDatabase> _stationDatabaseMock;
        private readonly AtomDataSelectionNonAurnNetworks _sut;

        private const string BucketName = "test-bucket";
        private const string PollutantMasterKey = "pollutant-master-key.xlsx";
        private const string StationMasterKey = "station-master-key.xlsx";

        private static readonly string[] PollutantUniqueKeys = ["pollutantID", "pollutantName", "pollutant_value"];
        private static readonly string[] StationUniqueKeys = ["SiteID", "Network Type", "Pollutant Name"];

        public AtomDataSelectionNonAurnNetworksTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _mongoFactoryMock = new Mock<IMongoDbClientFactory>();
            _s3Mock = new Mock<IAmazonS3>();

            _pollutantCollectionMock = new Mock<IMongoCollection<BsonDocument>>();
            _stationCollectionMock = new Mock<IMongoCollection<BsonDocument>>();
            _pollutantIndexMock = new Mock<IMongoIndexManager<BsonDocument>>();
            _stationIndexMock = new Mock<IMongoIndexManager<BsonDocument>>();
            _pollutantDatabaseMock = new Mock<IMongoDatabase>();
            _stationDatabaseMock = new Mock<IMongoDatabase>();

            // Wire up Database property so DropCollectionAsync doesn't throw
            _pollutantCollectionMock.Setup(c => c.Database).Returns(_pollutantDatabaseMock.Object);
            _stationCollectionMock.Setup(c => c.Database).Returns(_stationDatabaseMock.Object);

            _pollutantDatabaseMock
                .Setup(d => d.DropCollectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _stationDatabaseMock
                .Setup(d => d.DropCollectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

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
                _s3Mock.Object);

            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", BucketName);
            Environment.SetEnvironmentVariable("POLLUTANT_MASTER_KEY", PollutantMasterKey);
            Environment.SetEnvironmentVariable("POLLUTANT_STATION_MASTER_KEY", StationMasterKey);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Environment.SetEnvironmentVariable("S3_BUCKET_NAME", null);
                Environment.SetEnvironmentVariable("POLLUTANT_MASTER_KEY", null);
                Environment.SetEnvironmentVariable("POLLUTANT_STATION_MASTER_KEY", null);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

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

        /// <summary>Routes S3 setup to the correct key based on which public method is under test.</summary>
        private void SetupS3(bool isPollutant, MemoryStream stream)
        {
            string key = isPollutant ? PollutantMasterKey : StationMasterKey;
            _s3Mock
                .Setup(s => s.GetObjectAsync(BucketName, key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetObjectResponse { ResponseStream = stream });
        }

        /// <summary>Calls the appropriate public method under test.</summary>
        private Task CallSutMethod(bool isPollutant) =>
            isPollutant
                ? _sut.ExceltoMongoDB("any")
                : _sut.ExceltoMongoDB_Station_detials("any");

        private Mock<IMongoCollection<BsonDocument>> GetCollectionMock(bool isPollutant) =>
            isPollutant ? _pollutantCollectionMock : _stationCollectionMock;

        private Mock<IMongoIndexManager<BsonDocument>> GetIndexMock(bool isPollutant) =>
            isPollutant ? _pollutantIndexMock : _stationIndexMock;

        private Mock<IMongoDatabase> GetDatabaseMock(bool isPollutant) =>
            isPollutant ? _pollutantDatabaseMock : _stationDatabaseMock;

        private static string[] GetUniqueKeys(bool isPollutant) =>
            isPollutant ? PollutantUniqueKeys : StationUniqueKeys;

        private static string GetCollectionName(bool isPollutant) =>
            isPollutant
                ? "aqie_atom_non_aurn_networks_pollutant_master"
                : "aqie_atom_non_aurn_networks_station_details";

        private static string[][] GetSampleRows(bool isPollutant) =>
            isPollutant
                ? [["1", "NO2", "high"]]
                : [["SITE001", "Urban", "NO2"]];

        // ── GetAtomNonAurnNetworks – routing ──────────────────────────────────────

        [Fact]
        public async Task GetAtomNonAurnNetworks_ReturnsSuccess_AndCallsPollutantPath_WhenSiteNameIsPollutant()
        {
            // Arrange
            SetupS3(isPollutant: true, CreateExcelStream(PollutantUniqueKeys));
            var data = new QueryStringData { SiteName = "Pollutant", pollutantName = "any" };

            // Act
            var result = await _sut.GetAtomNonAurnNetworks(data);

            // Assert
            Assert.Equal("Success", (string)result);
            _mongoFactoryMock.Verify(
                f => f.GetCollection<BsonDocument>("aqie_atom_non_aurn_networks_pollutant_master"),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetAtomNonAurnNetworks_ReturnsSuccess_AndCallsStationPath_WhenSiteNameIsNotPollutant()
        {
            // Arrange
            SetupS3(isPollutant: false, CreateExcelStream(StationUniqueKeys));
            var data = new QueryStringData { SiteName = "Station", pollutantName = "any" };

            // Act
            var result = await _sut.GetAtomNonAurnNetworks(data);

            // Assert
            Assert.Equal("Success", (string)result);
            _mongoFactoryMock.Verify(
                f => f.GetCollection<BsonDocument>("aqie_atom_non_aurn_networks_station_details"),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetAtomNonAurnNetworks_ReturnsFailure_WhenExceptionIsThrown()
        {
            // S3 not set up → GetObjectAsync throws → outer catch returns "Failure"
            _s3Mock
                .Setup(s => s.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("unexpected"));

            var data = new QueryStringData { SiteName = "Pollutant", pollutantName = "any" };

            var result = await _sut.GetAtomNonAurnNetworks(data);

            Assert.Equal("Failure", (string)result);
        }

        // ── Environment-variable guards (path-specific env vars kept as Facts) ────

        [Fact]
        public async Task ExceltoMongoDB_Throws_WhenS3BucketNameMissing()
        {
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.ExceltoMongoDB("any"));
        }

        [Fact]
        public async Task ExceltoMongoDB_Throws_WhenPollutantMasterKeyMissing()
        {
            // The SUT reads POLLUTANT_MASTER_KEY (not POLLUTANT_MASTER)
            Environment.SetEnvironmentVariable("POLLUTANT_MASTER_KEY", null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.ExceltoMongoDB("any"));
        }

        [Fact]
        public async Task ExceltoMongoDB_StationDetails_Throws_WhenS3BucketNameMissing()
        {
            Environment.SetEnvironmentVariable("S3_BUCKET_NAME", null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.ExceltoMongoDB_Station_detials("any"));
        }

        [Fact]
        public async Task ExceltoMongoDB_StationDetails_Throws_WhenStationMasterKeyMissing()
        {
            // The SUT reads POLLUTANT_STATION_MASTER_KEY (not POLLUTANT_STATION_MASTER)
            Environment.SetEnvironmentVariable("POLLUTANT_STATION_MASTER_KEY", null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.ExceltoMongoDB_Station_detials("any"));
        }

        // ── Shared LoadExcelToMongoDbAsync behaviour (Theory covers both paths) ───

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LoadExcel_DoesNotUpsert_WhenWorksheetHasOnlyHeaderRow(bool isPollutant)
        {
            // Arrange – header row only; Skip(1) produces no iterations
            SetupS3(isPollutant, CreateExcelStream(GetUniqueKeys(isPollutant)));

            // Act
            await CallSutMethod(isPollutant);

            // Assert
            GetCollectionMock(isPollutant).Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LoadExcel_UpsertsDocument_WhenAllKeyFieldsPresent(bool isPollutant)
        {
            // Arrange
            SetupS3(isPollutant, CreateExcelStream(GetUniqueKeys(isPollutant), GetSampleRows(isPollutant)));

            // Act
            await CallSutMethod(isPollutant);

            // Assert – exactly one upsert
            GetCollectionMock(isPollutant).Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.Is<ReplaceOptions>(o => o.IsUpsert),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LoadExcel_UpsertsMultipleDocuments_WhenMultipleValidRowsPresent(bool isPollutant)
        {
            // Arrange
            string[][] rows = isPollutant
                ? [["1", "NO2", "high"], ["2", "PM10", "medium"], ["3", "SO2", "low"]]
                : [["SITE001", "Urban", "NO2"], ["SITE002", "Rural", "PM10"], ["SITE003", "Suburban", "SO2"]];

            SetupS3(isPollutant, CreateExcelStream(GetUniqueKeys(isPollutant), rows));

            // Act
            await CallSutMethod(isPollutant);

            // Assert
            GetCollectionMock(isPollutant).Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(3));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LoadExcel_SkipsRow_WhenKeyFieldIsMissing(bool isPollutant)
        {
            // Headers deliberately omit one unique key so the row is skipped
            string[] incompleteHeaders = isPollutant
                ? ["pollutantID", "pollutantName", "extra_column"]   // missing: pollutant_value
                : ["SiteID", "Network Type", "OtherField"];           // missing: Pollutant Name

            SetupS3(isPollutant, CreateExcelStream(incompleteHeaders, [["val1", "val2", "val3"]]));

            // Act
            await CallSutMethod(isPollutant);

            // Assert
            GetCollectionMock(isPollutant).Verify(
                c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LoadExcel_DropsAndRecreatesCollection_BeforeUpserting(bool isPollutant)
        {
            // Arrange
            SetupS3(isPollutant, CreateExcelStream(GetUniqueKeys(isPollutant)));
            string collectionName = GetCollectionName(isPollutant);

            // Act
            await CallSutMethod(isPollutant);

            // Assert
            // Collection is fetched twice: once before drop, once after recreate
            _mongoFactoryMock.Verify(
                f => f.GetCollection<BsonDocument>(collectionName),
                Times.Exactly(2));

            GetDatabaseMock(isPollutant).Verify(
                d => d.DropCollectionAsync(collectionName, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LoadExcel_CreatesUniqueIndex_OnExpectedKeyFields(bool isPollutant)
        {
            // Arrange
            SetupS3(isPollutant, CreateExcelStream(GetUniqueKeys(isPollutant)));

            // Act
            await CallSutMethod(isPollutant);

            // Assert
            GetIndexMock(isPollutant).Verify(
                i => i.CreateOneAsync(
                    It.Is<CreateIndexModel<BsonDocument>>(m => m.Options.Unique == true),
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // ── Exception propagation (Theory covers both paths) ──────────────────────

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LoadExcel_RethrowsMongoException_FromReplaceOneAsync(bool isPollutant)
        {
            // Arrange
            SetupS3(isPollutant, CreateExcelStream(GetUniqueKeys(isPollutant), GetSampleRows(isPollutant)));

            GetCollectionMock(isPollutant)
                .Setup(c => c.ReplaceOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<BsonDocument>(),
                    It.IsAny<ReplaceOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new MongoException("Mongo failure"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CallSutMethod(isPollutant));

            Assert.IsType<MongoException>(ex.InnerException);
            Assert.Equal("Mongo failure", ex.InnerException!.Message);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LoadExcel_RethrowsException_WhenS3Throws(bool isPollutant)
        {
            // Arrange
            string s3Key = isPollutant ? PollutantMasterKey : StationMasterKey;
            _s3Mock
                .Setup(s => s.GetObjectAsync(BucketName, s3Key, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("S3 unavailable"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CallSutMethod(isPollutant));

            Assert.NotNull(ex.InnerException);
            Assert.Equal("S3 unavailable", ex.InnerException!.Message);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LoadExcel_RethrowsMongoException_FromCreateIndexAsync(bool isPollutant)
        {
            // Arrange
            SetupS3(isPollutant, CreateExcelStream(GetUniqueKeys(isPollutant)));

            GetIndexMock(isPollutant)
                .Setup(i => i.CreateOneAsync(
                    It.IsAny<CreateIndexModel<BsonDocument>>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new MongoException("Index creation failed"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CallSutMethod(isPollutant));

            Assert.IsType<MongoException>(ex.InnerException);
            Assert.Equal("Index creation failed", ex.InnerException!.Message);
        }
    }
}