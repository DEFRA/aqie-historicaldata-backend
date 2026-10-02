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

        // ------------------------------------------------------------------
        // Builder: documents with explicit pollutantID per entry
        // ------------------------------------------------------------------
        private AtomDataSelectionPollutantDataSource CreateSutWithDocuments(
            List<StationDetailDocument> documents)
        {
            var cursorMock = new Mock<IAsyncCursor<StationDetailDocument>>();
            cursorMock
                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            cursorMock.Setup(c => c.Current).Returns(documents);

            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<StationDetailDocument>>(),
                    It.IsAny<FindOptions<StationDetailDocument, StationDetailDocument>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(cursorMock.Object);

            _mongoFactoryMock
                .Setup(f => f.GetCollection<StationDetailDocument>(
                    "aqie_atom_non_aurn_networks_station_details"))
                .Returns(_collectionMock.Object);

            return new AtomDataSelectionPollutantDataSource(
                _loggerMock.Object,
                _mongoFactoryMock.Object);
        }

        // Convenience overload for tests that only care about NetworkType / NetworkID
        // and do not need specific pollutantID values (defaults to "1")
        private AtomDataSelectionPollutantDataSource CreateSutWithProjections(
            List<(string? NetworkType, string? NetworkId)> projections) =>
            CreateSutWithDocuments(projections
                .Select(p => new StationDetailDocument
                {
                    NetworkType = p.NetworkType,
                    NetworkID   = p.NetworkId,
                    pollutantID = "1"
                })
                .ToList());

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
        // 2. pollutantId is whitespace-only → empty list
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
        // 3b. AURN pollutant only → OtherDataFromDefra absent
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_AurnPollutantOnly_NoDbResults_DoesNotReturnOtherDataFromDefra()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = "36" };

            var result = await InvokeAsync(sut, data);

            Assert.DoesNotContain(result,
                r => r.GetProperty("category").GetString() == "Other data from Defra");
        }

        // =====================================================================
        // 4. AURN network is now an OBJECT with "name" and "pollutantID" fields
        //    (Breaking change from the previous string-array shape)
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_AurnPollutant_NetworkIsObjectWithNameAndPollutantId()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = "44" };

            var result = await InvokeAsync(sut, data);

            var network = result[0].GetProperty("networks").EnumerateArray().Single();

            Assert.Equal("Automatic Urban and Rural Network (AURN)", network.GetProperty("name").GetString());
            Assert.Equal("44", network.GetProperty("pollutantID").GetString());
        }

        // =====================================================================
        // 4b. Multiple AURN pollutants → pollutantID contains all requested IDs
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_MultipleAurnPollutants_PollutantIdContainsAll()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = "36,44" };

            var result = await InvokeAsync(sut, data);

            var network  = result[0].GetProperty("networks").EnumerateArray().Single();
            var parts    = network.GetProperty("pollutantID").GetString()!.Split(',');

            Assert.Contains("36", parts);
            Assert.Contains("44", parts);
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
        // 5b. Non-AURN network includes "name", "id", and "pollutantID"
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_NonAurnNetwork_HasNameIdAndPollutantId()
        {
            var sut = CreateSutWithDocuments(
            [
                new StationDetailDocument
                {
                    NetworkType = "UKEAP - Rural NO2 Network",
                    NetworkID   = "1",
                    pollutantID = "44"
                }
            ]);
            var data = new QueryStringData { pollutantId = "129" };

            var result  = await InvokeAsync(sut, data);
            var network = result[0].GetProperty("networks").EnumerateArray().Single();

            Assert.Equal("UKEAP - Rural NO2 Network", network.GetProperty("name").GetString());
            Assert.Equal(1,                            network.GetProperty("id").GetInt32());
            Assert.Equal("44",                         network.GetProperty("pollutantID").GetString());
        }

        // =====================================================================
        // 5c. Multiple documents for the same network → pollutantIDs are aggregated
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_MultipleDocsForSameNetwork_PollutantIdsAreAggregated()
        {
            var sut = CreateSutWithDocuments(
            [
                new StationDetailDocument { NetworkType = "UKEAP - Precip-Net", NetworkID = "3", pollutantID = "129" },
                new StationDetailDocument { NetworkType = "UKEAP - Precip-Net", NetworkID = "3", pollutantID = "130" },
                new StationDetailDocument { NetworkType = "UKEAP - Precip-Net", NetworkID = "3", pollutantID = "131" }
            ]);
            var data = new QueryStringData { pollutantId = "129,130,131" };

            var result   = await InvokeAsync(sut, data);
            var networks = result[0].GetProperty("networks").EnumerateArray().ToList();

            Assert.Single(networks);

            var parts = networks[0].GetProperty("pollutantID").GetString()!.Split(',');
            Assert.Contains("129", parts);
            Assert.Contains("130", parts);
            Assert.Contains("131", parts);
        }

        // =====================================================================
        // 5d. Duplicate pollutantID values for the same network → de-duplicated
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_DuplicatePollutantIdsForNetwork_AreDeduped()
        {
            var sut = CreateSutWithDocuments(
            [
                new StationDetailDocument { NetworkType = "UKEAP - Precip-Net", NetworkID = "3", pollutantID = "129" },
                new StationDetailDocument { NetworkType = "UKEAP - Precip-Net", NetworkID = "3", pollutantID = "129" }
            ]);
            var data = new QueryStringData { pollutantId = "129" };

            var result = await InvokeAsync(sut, data);
            var parts  = result[0].GetProperty("networks").EnumerateArray()
                                  .Single()
                                  .GetProperty("pollutantID").GetString()!
                                  .Split(',');

            Assert.Single(parts.Where(p => p == "129"));
        }

        // =====================================================================
        // 6. Non-numeric NetworkId → id defaults to -2
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_NonNumericNetworkId_DefaultsToMinusTwo()
        {
            var sut  = CreateSutWithProjections([("LAQN", "NOT_A_NUMBER")]);
            var data = new QueryStringData { pollutantId = "1" };

            var result  = await InvokeAsync(sut, data);
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
        // 8. Duplicate NetworkType + NetworkID rows → grouped into one network
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_DuplicateNetworkTypeAndId_GroupedIntoOneNetwork()
        {
            var sut  = CreateSutWithProjections([("LAQN", "5"), ("LAQN", "5")]);
            var data = new QueryStringData { pollutantId = "1" };

            var result   = await InvokeAsync(sut, data);
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

            var result   = await InvokeAsync(sut, data);
            var networks = result[0].GetProperty("networks").EnumerateArray().ToList();

            Assert.Single(networks);
        }

        // =====================================================================
        // 10. Null pollutantID on document → document is excluded
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_NullPollutantIdOnDocument_IsFilteredOut()
        {
            var sut = CreateSutWithDocuments(
            [
                new StationDetailDocument { NetworkType = "UKEAP - Rural NO2 Network", NetworkID = "1", pollutantID = null }
            ]);
            var data = new QueryStringData { pollutantId = "129" };

            var result = await InvokeAsync(sut, data);

            Assert.Empty(result);
        }

        // =====================================================================
        // 11. All null NetworkTypes + non-AURN pollutant → empty list
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

            Assert.Contains(result,
                r => r.GetProperty("category").GetString() == "Near real-time data from Defra");
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
            var result = await sut.GetAtomPollutantDataSource(new QueryStringData { pollutantId = "36" });

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

            var sut = new AtomDataSelectionPollutantDataSource(_loggerMock.Object, _mongoFactoryMock.Object);
            await sut.GetAtomPollutantDataSource(new QueryStringData { pollutantId = "1" });

            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("GetAtomPollutantDataSource")),
                    It.IsAny<InvalidOperationException>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // =====================================================================
        // 15. Hydrocarbon pollutant only -> AurnCategory with Hydrocarbon network
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_HydrocarbonPollutantOnly_ReturnsHydrocarbonNetwork()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = "10" };

            var result  = await InvokeAsync(sut, data);
            var network = result[0].GetProperty("networks").EnumerateArray().Single();

            Assert.Equal("Near real-time data from Defra", result[0].GetProperty("category").GetString());
            Assert.Equal("Automatic Hydrocarbon Network", network.GetProperty("name").GetString());
            Assert.Equal("10", network.GetProperty("pollutantID").GetString());
        }

        // =====================================================================
        // 16. AURN + hydrocarbon pollutants -> two networks in one category,
        //     AURN first, hydrocarbon second
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_AurnAndHydrocarbonPollutants_ReturnsBothNetworksInOrder()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = "38,10,179" };

            var result   = await InvokeAsync(sut, data);
            var networks = result[0].GetProperty("networks").EnumerateArray().ToList();

            Assert.Single(result);
            Assert.Equal(2, networks.Count);

            Assert.Equal("Automatic Urban and Rural Network (AURN)", networks[0].GetProperty("name").GetString());
            Assert.Equal("38", networks[0].GetProperty("pollutantID").GetString());

            Assert.Equal("Automatic Hydrocarbon Network", networks[1].GetProperty("name").GetString());
            Assert.Equal("10,179", networks[1].GetProperty("pollutantID").GetString());
        }

        // =====================================================================
        // 17. Hydrocarbon networks never carry an "id" property
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_HydrocarbonNetwork_HasNoIdProperty()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = "22" };

            var result  = await InvokeAsync(sut, data);
            var network = result[0].GetProperty("networks").EnumerateArray().Single();

            Assert.False(network.TryGetProperty("id", out _));
        }

        // =====================================================================
        // 18. Every hydrocarbon pollutant ID maps to the hydrocarbon network
        // =====================================================================
        [Theory]
        [InlineData("10")] [InlineData("15")] [InlineData("22")] [InlineData("31")]
        [InlineData("35")] [InlineData("41")] [InlineData("43")] [InlineData("168")]
        [InlineData("175")] [InlineData("179")]
        public async Task GetAtomPollutantDataSource_EachHydrocarbonPollutantId_ProducesHydrocarbonNetwork(string pollutantId)
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = pollutantId };

            var result = await InvokeAsync(sut, data);

            Assert.Contains(
                result[0].GetProperty("networks").EnumerateArray(),
                n => n.GetProperty("name").GetString() == "Automatic Hydrocarbon Network");
        }

        // =====================================================================
        // 19. Pollutant IDs that are neither AURN nor hydrocarbon produce no
        //     near real-time category
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_UnknownPollutantId_ProducesNoNearRealTimeCategory()
        {
            var sut  = CreateSutWithProjections([]);
            var data = new QueryStringData { pollutantId = "129" };

            var result = await InvokeAsync(sut, data);

            Assert.DoesNotContain(result,
                r => r.GetProperty("category").GetString() == "Near real-time data from Defra");
        }

        // =====================================================================
        // 20. The Mongo filter excludes NetworkID = "10"
        // =====================================================================
        [Fact]
        public async Task GetAtomPollutantDataSource_BuildsFilterExcludingNetworkId10()
        {
            FilterDefinition<StationDetailDocument>? capturedFilter = null;

            var cursorMock = new Mock<IAsyncCursor<StationDetailDocument>>();
            cursorMock
                .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            cursorMock.Setup(c => c.Current).Returns([]);

            _collectionMock
                .Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<StationDetailDocument>>(),
                    It.IsAny<FindOptions<StationDetailDocument, StationDetailDocument>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<StationDetailDocument>,
                          FindOptions<StationDetailDocument, StationDetailDocument>,
                          CancellationToken>((f, _, _) => capturedFilter = f)
                .ReturnsAsync(cursorMock.Object);

            _mongoFactoryMock
                .Setup(f => f.GetCollection<StationDetailDocument>(
                    "aqie_atom_non_aurn_networks_station_details"))
                .Returns(_collectionMock.Object);

            var sut = new AtomDataSelectionPollutantDataSource(
                _loggerMock.Object, _mongoFactoryMock.Object);

            await sut.GetAtomPollutantDataSource(new QueryStringData { pollutantId = "22" });

            Assert.NotNull(capturedFilter);

            var registry   = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry;
            var serializer = registry.GetSerializer<StationDetailDocument>();
            var rendered   = capturedFilter!.Render(serializer, registry);

            Assert.Contains("NetworkID", rendered.ToString());
            Assert.Contains("$ne", rendered.ToString());
            Assert.Contains("10", rendered.ToString());
        }
    }
}