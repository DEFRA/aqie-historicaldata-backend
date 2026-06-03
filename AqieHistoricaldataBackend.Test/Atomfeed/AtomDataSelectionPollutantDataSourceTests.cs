using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    public class AtomDataSelectionPollutantDataSourceTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock = new();
        private readonly Mock<IMongoDbClientFactory> _mongoFactoryMock = new();
        private readonly Mock<IMongoCollection<StationDetailDocument>> _collectionMock = new();
        private readonly Mock<IAsyncCursor<StationDetailDocument>> _cursorMock = new();

        private AtomDataSelectionPollutantDataSource CreateSut()
        {
            _mongoFactoryMock
                .Setup(f => f.GetCollection<StationDetailDocument>(
                    "aqie_atom_non_aurn_networks_station_details"))
                .Returns(_collectionMock.Object);

            return new AtomDataSelectionPollutantDataSource(
                _loggerMock.Object,
                _mongoFactoryMock.Object);
        }

        private AtomDataSelectionPollutantDataSource CreateSutWithProjections(
            List<(string? NetworkType, string? NetworkId)> projections)
        {
            var docs = projections
                .Select(p => new StationDetailDocument
                {
                    NetworkType = p.NetworkType,
                    NetworkID   = p.NetworkId,
                    pollutantID = "1"
                })
                .ToList();

            var projCursorMock = new Mock<IAsyncCursor<StationDetailDocument>>();
            projCursorMock
                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            projCursorMock.Setup(c => c.Current).Returns(docs);

            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<StationDetailDocument>>(),
                    It.IsAny<FindOptions<StationDetailDocument, StationDetailDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(projCursorMock.Object);

            _mongoFactoryMock
                .Setup(f => f.GetCollection<StationDetailDocument>(
                    "aqie_atom_non_aurn_networks_station_details"))
                .Returns(_collectionMock.Object);

            return new AtomDataSelectionPollutantDataSource(
                _loggerMock.Object,
                _mongoFactoryMock.Object);
        }

        /// <summary>
        /// Serializes the SUT result through JSON to avoid cross-assembly
        /// anonymous-type dynamic binding failures.
        /// </summary>
        private static async Task<List<JsonElement>> InvokeAsync(
            AtomDataSelectionPollutantDataSource sut,
            QueryStringData data)
        {
            var result = await sut.GetAtomPollutantDataSource(data);

            if (result is string)
                return [];

            var json = JsonSerializer.Serialize(result);
            return JsonSerializer.Deserialize<List<JsonElement>>(json)!;
        }

        // =====================================================================
        // 1. pollutantId is null → empty list
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_NullPollutantId_ReturnsEmptyList()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = null };

            var result = await InvokeAsync(sut, data);

            Assert.Empty(result);
        }

        // =====================================================================
        // 2. pollutantId is empty string → empty list
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_EmptyPollutantId_ReturnsEmptyList()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = "   ,  , " };

            var result = await InvokeAsync(sut, data);

            Assert.Empty(result);
        }

        // =====================================================================
        // 3. AURN pollutant only + no DB results → only AurnCategory returned
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_AurnPollutantOnly_NoDbResults_ReturnsOnlyAurnCategory()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = "36" };

            var result = await InvokeAsync(sut, data);

            Assert.Single(result);
            Assert.Equal("Near real-time data from Defra", result[0].GetProperty("category").GetString());
        }

        // =====================================================================
        // 3b. AURN pollutant only + no DB results → OtherDataFromDefra absent
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_AurnPollutantOnly_NoDbResults_DoesNotReturnOtherDataFromDefra()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = "36" };

            var result = await InvokeAsync(sut, data);

            Assert.DoesNotContain(result, r => r.GetProperty("category").GetString() == "Other data from Defra");
        }

        // =====================================================================
        // 4. AURN category networks list contains AurnNetworkName
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_AurnPollutant_AurnCategoryHasNetworkName()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = "44" };

            var result = await InvokeAsync(sut, data);

            var aurnNetworks = result[0].GetProperty("networks")
                .EnumerateArray()
                .Select(e => e.GetString())
                .ToList();

            Assert.Contains("Automatic Urban and Rural Network (AURN)", aurnNetworks);
        }

        // =====================================================================
        // 5. Non-AURN pollutant + DB result → only OtherDataFromDefra returned
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_NonAurnPollutant_WithDbResult_ReturnsOtherDataCategory()
        {
            var sut  = CreateSutWithProjections([("LAQN", "5")]);
            var data = new QueryStringData { pollutantId = "1" };

            var result = await InvokeAsync(sut, data);

            Assert.Single(result);
            Assert.Equal("Other data from Defra", result[0].GetProperty("category").GetString());
        }

        // =====================================================================
        // 6. Non-numeric NetworkId → id defaults to -2
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_NonNumericNetworkId_DefaultsToMinusTwo()
        {
            var sut  = CreateSutWithProjections([("LAQN", "NOT_A_NUMBER")]);
            var data = new QueryStringData { pollutantId = "1" };

            var result = await InvokeAsync(sut, data);

            var network = result[0].GetProperty("networks").EnumerateArray().First();
            Assert.Equal(-2, network.GetProperty("id").GetInt32());
        }

        // =====================================================================
        // 7. Both AURN and non-AURN pollutants + DB results → two categories
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_BothAurnAndNonAurn_ReturnsTwoCategories()
        {
            var sut  = CreateSutWithProjections([("LAQN", "7")]);
            var data = new QueryStringData { pollutantId = "36,1" };

            var result = await InvokeAsync(sut, data);

            Assert.Equal(2, result.Count);
            Assert.Equal("Near real-time data from Defra", result[0].GetProperty("category").GetString());
            Assert.Equal("Other data from Defra",          result[1].GetProperty("category").GetString());
        }

        // =====================================================================
        // 8. Duplicate NetworkType rows → DistinctBy keeps only one
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_DuplicateNetworkType_DistinctByReducesToOne()
        {
            var sut  = CreateSutWithProjections([("LAQN", "5"), ("LAQN", "5")]);
            var data = new QueryStringData { pollutantId = "1" };

            var result = await InvokeAsync(sut, data);

            var networks = result[0].GetProperty("networks").EnumerateArray().ToList();
            Assert.Single(networks);
        }

        // =====================================================================
        // 9. Null NetworkType rows are filtered out
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_NullNetworkType_IsFilteredOut()
        {
            var sut  = CreateSutWithProjections([(null, null), ("LAQN", "3")]);
            var data = new QueryStringData { pollutantId = "1" };

            var result = await InvokeAsync(sut, data);

            var networks = result[0].GetProperty("networks").EnumerateArray().ToList();
            Assert.Single(networks);
        }

        // =====================================================================
        // 10. All null NetworkTypes + non-AURN pollutant → empty list
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_AllNullNetworkTypes_NonAurnPollutant_ReturnsEmptyList()
        {
            var sut  = CreateSutWithProjections([(null, null)]);
            var data = new QueryStringData { pollutantId = "1" };

            var result = await InvokeAsync(sut, data);

            Assert.Empty(result);
        }

        // =====================================================================
        // 11. Whitespace-only pollutantId → empty list
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_WhitespacePollutantId_ReturnsEmptyList()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = " , , " };

            var result = await InvokeAsync(sut, data);

            Assert.Empty(result);
        }

        // =====================================================================
        // 12. All known AURN pollutant IDs produce AurnCategory (boundary test)
        // =====================================================================
        [Theory]
        [InlineData("36")] [InlineData("37")] [InlineData("38")] [InlineData("39")]
        [InlineData("40")] [InlineData("44")] [InlineData("45")] [InlineData("46")]
        public async Task GetAtomPollutantDataSource_EachAurnPollutantId_ProducesAurnCategory(string pollutantId)
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = pollutantId };

            var result = await InvokeAsync(sut, data);

            Assert.Contains(result, r => r.GetProperty("category").GetString() == "Near real-time data from Defra");
        }

        // =====================================================================
        // 13. Exception → returns string "Failure"
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_WhenExceptionThrown_ReturnsFailureString()
        {
            _mongoFactoryMock
                .Setup(f => f.GetCollection<StationDetailDocument>(
                    "aqie_atom_non_aurn_networks_station_details"))
                .Throws(new InvalidOperationException("DB unavailable"));

            var sut    = new AtomDataSelectionPollutantDataSource(_loggerMock.Object, _mongoFactoryMock.Object);
            var data   = new QueryStringData { pollutantId = "36" };
            var result = await sut.GetAtomPollutantDataSource(data);

            Assert.Equal("Failure", (string)result);
        }

        // =====================================================================
        // 14. Exception → logger receives the error
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_WhenExceptionThrown_LogsError()
        {
            _mongoFactoryMock
                .Setup(f => f.GetCollection<StationDetailDocument>(
                    "aqie_atom_non_aurn_networks_station_details"))
                .Throws(new InvalidOperationException("DB unavailable"));

            var sut  = new AtomDataSelectionPollutantDataSource(_loggerMock.Object, _mongoFactoryMock.Object);
            var data = new QueryStringData { pollutantId = "1" };
            await sut.GetAtomPollutantDataSource(data);

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("GetAtomPollutantDataSource")),
                    It.IsAny<InvalidOperationException>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}