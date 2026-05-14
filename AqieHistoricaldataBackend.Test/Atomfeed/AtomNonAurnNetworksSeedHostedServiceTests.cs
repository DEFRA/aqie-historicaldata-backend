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
using System.Reflection;
using Xunit;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomNonAurnNetworksSeedHostedServiceTests
    {
        private const string LockCollection = "aqie_atom_seed_locks";

        private readonly Mock<ILogger<AtomNonAurnNetworksSeedHostedService>> _loggerMock;
        private readonly Mock<IAtomDataSelectionNonAurnNetworks> _nonAurnServiceMock;
        private readonly Mock<IMongoDbClientFactory> _mongoFactoryMock;
        private readonly Mock<IMongoCollection<BsonDocument>> _collectionMock;
        private readonly Mock<IMongoIndexManager<BsonDocument>> _indexMock;
        private readonly AtomNonAurnNetworksSeedHostedService _sut;

        public AtomNonAurnNetworksSeedHostedServiceTests()
        {
            _loggerMock        = new Mock<ILogger<AtomNonAurnNetworksSeedHostedService>>();
            _nonAurnServiceMock = new Mock<IAtomDataSelectionNonAurnNetworks>();
            _mongoFactoryMock  = new Mock<IMongoDbClientFactory>();
            _collectionMock    = new Mock<IMongoCollection<BsonDocument>>();
            _indexMock         = new Mock<IMongoIndexManager<BsonDocument>>();

            _collectionMock.Setup(c => c.Indexes).Returns(_indexMock.Object);

            _mongoFactoryMock
                .Setup(f => f.GetCollection<BsonDocument>(LockCollection))
                .Returns(_collectionMock.Object);

            _indexMock
                .Setup(i => i.CreateOneAsync(
                    It.IsAny<CreateIndexModel<BsonDocument>>(),
                    null,
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
            await _sut.StopAsync(CancellationToken.None);
            // No exception — StopAsync returns Task.CompletedTask
        }

        // ── Happy path ────────────────────────────────────────────────────────────

        [Fact]
        public async Task StartAsync_SeedsAndMarksCompleted_WhenLockIsAcquired()
        {
            // Arrange
            SetupInsertOneSucceeds();
            SetupUpdateOneSucceeds();

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB(string.Empty), Times.Once);
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
            SetupInsertOneSucceeds();
            _nonAurnServiceMock
                .Setup(s => s.ExceltoMongoDB(It.IsAny<string>()))
                .ThrowsAsync(new Exception("pollutant seed failed"));
            SetupDeleteOneSucceeds();

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert
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
            SetupInsertOneSucceeds();
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
            // Arrange — seed fails AND DeleteOneAsync also throws (swallowed by catch in RemoveLockAsync)
            SetupInsertOneSucceeds();
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

        // ── DuplicateKey — another instance holds the lock ────────────────────────

        [Fact]
        public async Task StartAsync_Skips_WhenLockDocExistsWithStatusLocked()
        {
            // Arrange
            SetupInsertOneThrowsDuplicateKey();
            SetupFindReturnsStatus("locked");

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert — no seeding attempted
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB(It.IsAny<string>()), Times.Never);
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB_Station_detials(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task StartAsync_Skips_WhenLockDocExistsWithStatusCompleted()
        {
            // Arrange
            SetupInsertOneThrowsDuplicateKey();
            SetupFindReturnsStatus("completed");

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task StartAsync_Skips_WhenDuplicateKeyButDocNotFound()
        {
            // Arrange — race: another instance inserted then immediately removed the doc
            SetupInsertOneThrowsDuplicateKey();
            SetupFindReturnsEmpty();

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert — status logged as "unknown"; no seeding
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB(It.IsAny<string>()), Times.Never);
        }

        // ── Generic exception during lock acquisition ─────────────────────────────

        [Fact]
        public async Task StartAsync_Skips_WhenInsertOneThrowsGenericException()
        {
            // Arrange
            _collectionMock
                .Setup(c => c.InsertOneAsync(
                    It.IsAny<BsonDocument>(),
                    It.IsAny<InsertOneOptions>(),
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
            // Arrange — index creation fails before InsertOne is even reached
            _indexMock
                .Setup(i => i.CreateOneAsync(
                    It.IsAny<CreateIndexModel<BsonDocument>>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("index creation failed"));

            // Act
            await _sut.StartAsync(CancellationToken.None);

            // Assert
            _nonAurnServiceMock.Verify(s => s.ExceltoMongoDB(It.IsAny<string>()), Times.Never);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void SetupInsertOneSucceeds() =>
            _collectionMock
                .Setup(c => c.InsertOneAsync(
                    It.IsAny<BsonDocument>(),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

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

        private void SetupInsertOneThrowsDuplicateKey() =>
            _collectionMock
                .Setup(c => c.InsertOneAsync(
                    It.IsAny<BsonDocument>(),
                    It.IsAny<InsertOneOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(CreateDuplicateKeyException());

        private void SetupFindReturnsStatus(string status)
        {
            var doc    = new BsonDocument { ["_id"] = "non_aurn_networks_seed", ["status"] = status };
            var cursor = new Mock<IAsyncCursor<BsonDocument>>();
            cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(true)
                  .ReturnsAsync(false);
            cursor.Setup(c => c.Current).Returns(new[] { doc });

            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(cursor.Object);
        }

        private void SetupFindReturnsEmpty()
        {
            var cursor = new Mock<IAsyncCursor<BsonDocument>>();
            cursor.Setup(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(false);
            cursor.Setup(c => c.Current).Returns(Array.Empty<BsonDocument>());

            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<BsonDocument>>(),
                    It.IsAny<FindOptions<BsonDocument, BsonDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(cursor.Object);
        }

        /// <summary>
        /// Builds a real <see cref="MongoWriteException"/> whose WriteError.Category is
        /// <see cref="ServerErrorCategory.DuplicateKey"/> (error code 11000).
        /// WriteError has an internal constructor, so reflection is required.
        /// </summary>
        private static MongoWriteException CreateDuplicateKeyException()
        {
            var connectionId = new ConnectionId(
                new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017)));

            var writeError = (WriteError)Activator.CreateInstance(
                typeof(WriteError),
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                args: new object[] { ServerErrorCategory.DuplicateKey, 11000, "E11000 duplicate key error", new BsonDocument() },
                culture: null)!;

            return new MongoWriteException(connectionId, writeError, writeConcernError: null, new Exception("dup key"));
        }
    }
}