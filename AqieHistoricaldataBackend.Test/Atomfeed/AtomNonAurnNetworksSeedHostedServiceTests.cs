using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Utils.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using Moq;
using System.Net;
using Xunit;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomNonAurnNetworksSeedHostedServiceTests
    {
        private const string LockCollection = "aqie_atom_seed_locks";

        private readonly Mock<ILogger<AtomNonAurnNetworksSeedHostedService>> _loggerMock;
        private readonly Mock<IAtomDataSelectionNonAurnNetworks>             _nonAurnServiceMock;
        private readonly Mock<IMongoDbClientFactory>                         _mongoFactoryMock;
        private readonly Mock<IMongoCollection<BsonDocument>>                _collectionMock;
        private readonly Mock<IMongoIndexManager<BsonDocument>>              _indexMock;
        private readonly AtomNonAurnNetworksSeedHostedService                _sut;

        public AtomNonAurnNetworksSeedHostedServiceTests()
        {
            _loggerMock         = new Mock<ILogger<AtomNonAurnNetworksSeedHostedService>>();
            _nonAurnServiceMock = new Mock<IAtomDataSelectionNonAurnNetworks>();
            _mongoFactoryMock   = new Mock<IMongoDbClientFactory>();
            _collectionMock     = new Mock<IMongoCollection<BsonDocument>>();
            _indexMock          = new Mock<IMongoIndexManager<BsonDocument>>();

            _collectionMock.Setup(c => c.Indexes).Returns(_indexMock.Object);

            _mongoFactoryMock
                .Setup(f => f.GetCollection<BsonDocument>(LockCollection))
                .Returns(_collectionMock.Object);

            _indexMock
                .Setup(i => i.CreateOneAsync(
                    It.IsAny<CreateIndexModel<BsonDocument>>(),
                    It.IsAny<CreateOneIndexOptions>(),       // ← was: null
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("ttl_lock");

            _sut = new AtomNonAurnNetworksSeedHostedService(
                _loggerMock.Object,
                _nonAurnServiceMock.Object,
                _mongoFactoryMock.Object);
        }

        // ── StopAsync ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task StopAsync_CompletesImmediately()
        {
            var exception = await Record.ExceptionAsync(() => _sut.StopAsync(CancellationToken.None));

            Assert.Null(exception);
        }

        // ── Happy path ────────────────────────────────────────────────────────────

        [Fact]
        public async Task StartAsync_SeedsAndMarksCompleted_WhenLockIsAcquired()
        {
            // Arrange — FindOneAndUpdate succeeds (lock acquired) on a "completed" or absent doc
            SetupFindOneAndUpdateSucceeds();
            SetupUpdateOneSucceeds();

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB(string.Empty),               Times.Once);
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB_Station_detials(string.Empty), Times.Once);
            _collectionMock.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<UpdateDefinition<BsonDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── Seed failure — lock must be removed ───────────────────────────────────

        [Fact]
        public async Task StartAsync_RemovesLock_WhenExceltoMongoDBThrows()
        {
            // Arrange
            SetupFindOneAndUpdateSucceeds();
            _nonAurnServiceMock
                .Setup(s => s.ExceltoMongoDB(It.IsAny<string>()))
                .ThrowsAsync(new Exception("pollutant seed failed"));
            SetupDeleteOneSucceeds();

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert — lock removed, completion never written
            _collectionMock.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<CancellationToken>()), Times.Once);
            _collectionMock.Verify(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<UpdateDefinition<BsonDocument>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StartAsync_RemovesLock_WhenExceltoMongoDBStationDetailsThrows()
        {
            // Arrange
            SetupFindOneAndUpdateSucceeds();
            _nonAurnServiceMock
                .Setup(s => s.ExceltoMongoDB_Station_detials(It.IsAny<string>()))
                .ThrowsAsync(new Exception("station seed failed"));
            SetupDeleteOneSucceeds();

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert
            _collectionMock.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAsync_LogsError_AndDoesNotThrow_WhenRemoveLockAlsoFails()
        {
            // Arrange — seed fails AND DeleteOneAsync also throws (swallowed by RemoveLockAsync)
            SetupFindOneAndUpdateSucceeds();
            _nonAurnServiceMock
                .Setup(s => s.ExceltoMongoDB(It.IsAny<string>()))
                .ThrowsAsync(new Exception("seed failed"));
            _collectionMock
                .Setup(c => c.DeleteOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("delete failed"));

            // Act — must NOT propagate
            var exception = await Record.ExceptionAsync(() => _sut.StartAsync(CancellationToken.None));

            Assert.Null(exception);
        }

        // ── DuplicateKey — another instance is actively seeding ───────────────────

        [Fact]
        public async Task StartAsync_Skips_WhenAnotherInstanceHoldsLock()
        {
            // Arrange — upsert conflicts because "locked" doc already exists
            SetupFindOneAndUpdateThrowsDuplicateKey();

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert — no seeding, no lock removal
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB(It.IsAny<string>()),               Times.Never);
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB_Station_detials(It.IsAny<string>()), Times.Never);
            _collectionMock.Verify(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<BsonDocument>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── Generic exception during lock acquisition ─────────────────────────────

        [Fact]
        public async Task StartAsync_Skips_WhenFindOneAndUpdateThrowsGenericException()
        {
            // Arrange
            _collectionMock
                .Setup(c => c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<UpdateDefinition<BsonDocument>>(),
                    It.IsAny<FindOneAndUpdateOptions<BsonDocument, BsonDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("mongo timeout"));

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task StartAsync_Skips_WhenCreateIndexThrowsGenericException()
        {
            // Arrange — index creation fails before FindOneAndUpdate is reached
            _indexMock
                .Setup(i => i.CreateOneAsync(
                    It.IsAny<CreateIndexModel<BsonDocument>>(),
                    It.IsAny<CreateOneIndexOptions>(),       // ← was: null
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("index creation failed"));

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB(It.IsAny<string>()), Times.Never);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void SetupFindOneAndUpdateSucceeds() =>
            _collectionMock
                .Setup(c => c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<UpdateDefinition<BsonDocument>>(),
                    It.IsAny<FindOneAndUpdateOptions<BsonDocument, BsonDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BsonDocument { ["status"] = "locked" });

        private void SetupFindOneAndUpdateThrowsDuplicateKey() =>
            _collectionMock
                .Setup(c => c.FindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<UpdateDefinition<BsonDocument>>(),
                    It.IsAny<FindOneAndUpdateOptions<BsonDocument, BsonDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(CreateDuplicateKeyCommandException());

        private void SetupUpdateOneSucceeds() =>
            _collectionMock
                .Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<UpdateDefinition<BsonDocument>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

        private void SetupDeleteOneSucceeds() =>
            _collectionMock
                .Setup(c => c.DeleteOneAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeleteResult.Acknowledged(1));

        private static MongoCommandException CreateDuplicateKeyCommandException()
        {
            var connectionId = new ConnectionId(
                new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017)));

            var command = new BsonDocument("insert", "aqie_atom_seed_locks");

            var result = new BsonDocument
            {
                ["ok"]       = 0,
                ["code"]     = 11000,
                ["codeName"] = "DuplicateKey",
                ["errmsg"]   = "E11000 duplicate key error"
            };

            return new MongoCommandException(connectionId, "E11000 duplicate key error", command, result);
        }
    }
}