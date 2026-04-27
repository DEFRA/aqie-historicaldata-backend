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
using FluentAssertions;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionPollutantMasterTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
        private readonly Mock<IMongoDbClientFactory> _mongoFactoryMock;
        private readonly Mock<IMongoCollection<PollutantMasterDocument>> _collectionMock;
        private readonly AtomDataSelectionPollutantMaster _sut;

        public AtomDataSelectionPollutantMasterTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _mongoFactoryMock = new Mock<IMongoDbClientFactory>();
            _collectionMock = new Mock<IMongoCollection<PollutantMasterDocument>>();

            _mongoFactoryMock
                .Setup(f => f.GetCollection<PollutantMasterDocument>(
                    "aqie_atom_non_aurn_networks_pollutant_master"))
                .Returns(_collectionMock.Object);

            _sut = new AtomDataSelectionPollutantMaster(
                _loggerMock.Object,
                _mongoFactoryMock.Object);
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Mocks collection.FindAsync&lt;PollutantMasterProjection&gt;, which is the real
        /// interface method invoked when the fluent Find().Project().ToListAsync() chain
        /// is evaluated.
        /// </summary>
        private void SetupFindAsyncReturns(List<PollutantMasterProjection> items)
        {
            var cursorMock = new Mock<IAsyncCursor<PollutantMasterProjection>>();
            cursorMock
                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(items.Count > 0)
                .ReturnsAsync(false);
            cursorMock
                .Setup(c => c.Current)
                .Returns(items);

            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<PollutantMasterDocument>>(),
                    It.IsAny<FindOptions<PollutantMasterDocument, PollutantMasterProjection>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(cursorMock.Object);
        }

        private void SetupFindAsyncThrows(Exception exception)
        {
            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<PollutantMasterDocument>>(),
                    It.IsAny<FindOptions<PollutantMasterDocument, PollutantMasterProjection>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
        }

        private static List<PollutantMasterProjection> BuildProjectionList() =>
            new()
            {
                new PollutantMasterProjection
                {
                    pollutantID          = "1",
                    pollutantName        = "Nitrogen Dioxide",
                    pollutant_Abbreviation = "NO2",
                    pollutant_value      = "no2"
                },
                new PollutantMasterProjection
                {
                    pollutantID          = "2",
                    pollutantName        = "Ozone",
                    pollutant_Abbreviation = "O3",
                    pollutant_value      = "o3"
                }
            };

        // ─── Tests ───────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetPollutantMaster_ReturnsList_WhenCollectionHasDocuments()
        {
            // Arrange
            var expected = BuildProjectionList();
            SetupFindAsyncReturns(expected);

            // Act
            var result = await _sut.GetPollutantMaster();

            // Assert
            ((List<PollutantMasterProjection>)result).Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task GetPollutantMaster_ReturnsEmptyString_WhenCollectionIsEmpty()
        {
            // Arrange
            SetupFindAsyncReturns(new List<PollutantMasterProjection>());

            // Act
            var result = await _sut.GetPollutantMaster();

            // Assert
            ((string)result).Should().Be("Empty");
        }

        [Fact]
        public async Task GetPollutantMaster_LogsWarning_WhenCollectionIsEmpty()
        {
            // Arrange
            SetupFindAsyncReturns(new List<PollutantMasterProjection>());

            // Act
            await _sut.GetPollutantMaster();

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Pollutant master collection is empty")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetPollutantMaster_ReturnsFailureString_WhenExceptionIsThrown()
        {
            // Arrange
            SetupFindAsyncThrows(new Exception("Mongo failure"));

            // Act
            var result = await _sut.GetPollutantMaster();

            // Assert
            ((string)result).Should().Be("Failure");
        }

        [Fact]
        public async Task GetPollutantMaster_LogsError_WhenExceptionIsThrown()
        {
            // Arrange
            var exception = new Exception("Mongo failure");
            SetupFindAsyncThrows(exception);

            // Act
            await _sut.GetPollutantMaster();

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Error in Atom GetPollutantMaster")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}