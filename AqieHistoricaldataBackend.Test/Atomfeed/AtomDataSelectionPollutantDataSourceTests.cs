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
    public class AtomDataSelectionPollutantDataSourceTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
        private readonly Mock<IMongoDbClientFactory> _mongoFactoryMock;
        private readonly Mock<IMongoCollection<StationDetailDocument>> _collectionMock;
        private readonly AtomDataSelectionPollutantDataSource _sut;

        private static readonly List<string> AurnHardcodedSources =
        [
            "Near real-time data from Defra",
            "Automatic Urban and Rural Network (AURN)"
        ];

        private const string OtherDataFromDefra = "Other data from Defra";

        public AtomDataSelectionPollutantDataSourceTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _mongoFactoryMock = new Mock<IMongoDbClientFactory>();
            _collectionMock = new Mock<IMongoCollection<StationDetailDocument>>();

            _mongoFactoryMock
                .Setup(f => f.GetCollection<StationDetailDocument>("aqie_atom_non_aurn_networks_station_details"))
                .Returns(_collectionMock.Object);

            _sut = new AtomDataSelectionPollutantDataSource(
                _loggerMock.Object,
                _mongoFactoryMock.Object);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets up the collection mock so that Find(...).Project(...).ToListAsync()
        /// returns <paramref name="networkTypes"/>.
        /// </summary>
        private void SetupProjectedFind(IEnumerable<string?> networkTypes)
        {
            var cursorMock = new Mock<IAsyncCursor<string?>>();
            cursorMock
                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            cursorMock
                .Setup(c => c.Current)
                .Returns(new List<string?>(networkTypes));

            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<StationDetailDocument>>(),
                    It.IsAny<FindOptions<StationDetailDocument, string?>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(cursorMock.Object);
        }

        /// <summary>
        /// Sets up the collection mock so that Find(...).Project(...).ToListAsync() throws.
        /// </summary>
        private void SetupProjectedFindThrows(Exception ex)
        {
            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<StationDetailDocument>>(),
                    It.IsAny<FindOptions<StationDetailDocument, string?>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(ex);
        }

        // ── Branch: hasAurnPollutants = true ─────────────────────────────────────

        [Fact]
        public async Task GetAtomPollutantDataSource_AurnPollutant_NoDbResults_ReturnsAurnAndOtherDefra()
        {
            // Arrange – pollutant "36" is in the AURN set; DB returns nothing
            SetupProjectedFind([]);
            var data = new QueryStringData { pollutantId = "36" };

            // Act
            var result = (List<string?>)await _sut.GetAtomPollutantDataSource(data);

            // Assert
            result.Should().BeEquivalentTo(
                new List<string?> { AurnHardcodedSources[0], AurnHardcodedSources[1], OtherDataFromDefra },
                options => options.WithStrictOrdering());
        }

        [Fact]
        public async Task GetAtomPollutantDataSource_AurnPollutant_WithDbResults_ReturnsAurnSourcesPlusDbResults()
        {
            // Arrange – AURN pollutant + DB has additional network types
            SetupProjectedFind(["Welsh Air Quality Forum (WAQF)"]);
            var data = new QueryStringData { pollutantId = "40" };

            // Act
            var result = (List<string?>)await _sut.GetAtomPollutantDataSource(data);

            // Assert
            result.Should().BeEquivalentTo(
                new List<string?>
                {
                    AurnHardcodedSources[0],
                    AurnHardcodedSources[1],
                    OtherDataFromDefra,
                    "Welsh Air Quality Forum (WAQF)"
                },
                options => options.WithStrictOrdering());
        }

        [Theory]
        [InlineData("36")]
        [InlineData("37")]
        [InlineData("38")]
        [InlineData("39")]
        [InlineData("40")]
        [InlineData("44")]
        [InlineData("45")]
        [InlineData("46")]
        public async Task GetAtomPollutantDataSource_AllAurnPollutantIds_AlwaysIncludeAurnSources(string aurnId)
        {
            // Arrange
            SetupProjectedFind([]);
            var data = new QueryStringData { pollutantId = aurnId };

            // Act
            var result = (List<string?>)await _sut.GetAtomPollutantDataSource(data);

            // Assert – all 8 AURN pollutant IDs must trigger the AURN branch
            result.Should().Contain(AurnHardcodedSources[0]);
            result.Should().Contain(AurnHardcodedSources[1]);
            result.Should().Contain(OtherDataFromDefra);
        }

        [Fact]
        public async Task GetAtomPollutantDataSource_MixedAurnAndNonAurnPollutants_ReturnsAurnSources()
        {
            // Arrange – comma-separated list with one AURN and one non-AURN ID
            SetupProjectedFind(["London Air"]);
            var data = new QueryStringData { pollutantId = "36,99" };

            // Act
            var result = (List<string?>)await _sut.GetAtomPollutantDataSource(data);

            // Assert – AURN branch wins because at least one AURN ID is present
            result.Should().Contain(AurnHardcodedSources[0]);
            result.Should().Contain(AurnHardcodedSources[1]);
        }

        // ── Branch: hasAurnPollutants = false, hasResults = true ─────────────────

        [Fact]
        public async Task GetAtomPollutantDataSource_NonAurnPollutant_WithDbResults_ReturnsOtherDefraAndDbResults()
        {
            // Arrange – pollutant "99" is NOT in the AURN set
            SetupProjectedFind(["London Air"]);
            var data = new QueryStringData { pollutantId = "99" };

            // Act
            var result = (List<string?>)await _sut.GetAtomPollutantDataSource(data);

            // Assert
            result.Should().BeEquivalentTo(
                new List<string?> { OtherDataFromDefra, "London Air" },
                options => options.WithStrictOrdering());
        }

        // ── Branch: hasAurnPollutants = false, hasResults = false ────────────────

        [Fact]
        public async Task GetAtomPollutantDataSource_NonAurnPollutant_NoDbResults_ReturnsEmptyList()
        {
            // Arrange
            SetupProjectedFind([]);
            var data = new QueryStringData { pollutantId = "99" };

            // Act
            var result = (List<string?>)await _sut.GetAtomPollutantDataSource(data);

            // Assert
            ((List<string?>)result).Should().BeEmpty();
        }

        // ── Null pollutantId ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomPollutantDataSource_NullPollutantId_ReturnsEmptyList()
        {
            // Arrange – pollutantId is null → pollutantIds resolves to []
            SetupProjectedFind([]);
            var data = new QueryStringData { pollutantId = null };

            // Act
            var result = (List<string?>)await _sut.GetAtomPollutantDataSource(data);

            // Assert – no AURN IDs, no DB results → empty prefix, empty final list
            ((List<string?>)result).Should().BeEmpty();
        }

        // ── Null / duplicate DB values ────────────────────────────────────────────

        [Fact]
        public async Task GetAtomPollutantDataSource_DbReturnsNullEntries_NullsAreFiltered()
        {
            // Arrange – DB returns a mix of valid and null NetworkType values
            SetupProjectedFind([null, "London Air", null]);
            var data = new QueryStringData { pollutantId = "99" };

            // Act
            var result = (List<string?>)await _sut.GetAtomPollutantDataSource(data);

            // Assert – nulls stripped; OtherDataFromDefra prefixed because hasResults=true
            result.Should().BeEquivalentTo(
                new List<string?> { OtherDataFromDefra, "London Air" },
                options => options.WithStrictOrdering());
        }

        [Fact]
        public async Task GetAtomPollutantDataSource_DbReturnsDuplicates_ResultIsDistinct()
        {
            // Arrange
            SetupProjectedFind(["London Air", "London Air", "London Air"]);
            var data = new QueryStringData { pollutantId = "99" };

            // Act
            var result = (List<string?>)await _sut.GetAtomPollutantDataSource(data);

            // Assert – duplicates collapsed
            result.Should().BeEquivalentTo(
                new List<string?> { OtherDataFromDefra, "London Air" },
                options => options.WithStrictOrdering());
        }

        [Fact]
        public async Task GetAtomPollutantDataSource_DbReturnsOtherDataFromDefra_DeduplicatedInFinalList()
        {
            // Arrange – DB result is the same value as OtherDataFromDefra constant
            SetupProjectedFind([OtherDataFromDefra]);
            var data = new QueryStringData { pollutantId = "99" };

            // Act
            var result = (List<string?>)await _sut.GetAtomPollutantDataSource(data);

            // Assert – OtherDataFromDefra appears only once after Distinct()
            result.Should().ContainSingle(x => x == OtherDataFromDefra);
        }

        // ── Exception / catch branch ──────────────────────────────────────────────

        [Fact]
        public async Task GetAtomPollutantDataSource_MongoThrows_ReturnsFailureString()
        {
            // Arrange
            SetupProjectedFindThrows(new Exception("Mongo connection lost"));
            var data = new QueryStringData { pollutantId = "36" };

            // Act
            var result = await _sut.GetAtomPollutantDataSource(data);

            // Assert
            Assert.Equal("Failure", result);
        }

        [Fact]
        public async Task GetAtomPollutantDataSource_MongoThrows_LogsError()
        {
            // Arrange
            var exception = new Exception("Mongo connection lost");
            SetupProjectedFindThrows(exception);
            var data = new QueryStringData { pollutantId = "36" };

            // Act
            await _sut.GetAtomPollutantDataSource(data);

            // Assert – logger received at least one error-level call
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        // ── Empty pollutantId string ──────────────────────────────────────────────

        [Fact]
        public async Task GetAtomPollutantDataSource_EmptyPollutantId_ReturnsEmptyList()
        {
            // Arrange – empty string splits to []
            SetupProjectedFind([]);
            var data = new QueryStringData { pollutantId = "   " };

            // Act
            var result = (List<string?>)await _sut.GetAtomPollutantDataSource(data);

            // Assert
            ((List<string?>)result).Should().BeEmpty();
        }
    }
}