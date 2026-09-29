using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Utils.Mongo;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomSiteFilterHelperTest
    {
        private readonly ILogger _logger = NullLogger.Instance;

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static SiteInfo Site(
            string? siteId = "S1",
            string? regionId = "1",
            params PollutantInfo[] pollutants) =>
            new()
            {
                SiteName = "Site " + siteId,
                LocalSiteId = siteId,
                AreaType = "Urban",
                SiteType = "Background",
                RegionId = regionId,
                ZoneRegion = "Zone",
                Latitude = "51.1",
                Longitude = "-0.1",
                Pollutants = pollutants.ToList()
            };

        private static PollutantInfo Pollutant(string? name, string? start = "01/01/2020", string? end = "31/12/2020") =>
            new() { Name = name, StartDate = start, EndDate = end };

        private static Mock<IMongoCollection<T>> SetupCollection<T>(
            Mock<IMongoDbClientFactory> factory, string collectionName, List<T> items)
        {
            var collection = new Mock<IMongoCollection<T>>();
            var cursor = new Mock<IAsyncCursor<T>>();

            cursor.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(items.Count > 0)
                  .ReturnsAsync(false);
            cursor.Setup(c => c.Current).Returns(items);

            collection.Setup(c => c.FindAsync(
                        It.IsAny<FilterDefinition<T>>(),
                        It.IsAny<FindOptions<T, T>>(),
                        It.IsAny<CancellationToken>()))
                      .ReturnsAsync(cursor.Object);

            factory.Setup(f => f.GetCollection<T>(collectionName)).Returns(collection.Object);
            return collection;
        }

        // ─── FilterSitesByPollutants ──────────────────────────────────────────

        [Fact]
        public void FilterSitesByPollutants_MapsKnownPollutant_AndKeepsMatchingSites()
        {
            var sites = new List<SiteInfo>
            {
                Site("S1", "1", Pollutant("Nitrogen dioxide"), Pollutant("Ozone")),
                Site("S2", "2", Pollutant("Carbon monoxide"))
            };

            var result = AtomSiteFilterHelper.FilterSitesByPollutants(sites, "NO2", _logger);

            result.Should().HaveCount(1);
            result[0].LocalSiteId.Should().Be("S1");
            result[0].Pollutants.Should().ContainSingle(p => p.Name == "Nitrogen dioxide");
            result[0].SiteName.Should().Be("Site S1");
            result[0].AreaType.Should().Be("Urban");
            result[0].SiteType.Should().Be("Background");
            result[0].ZoneRegion.Should().Be("Zone");
            result[0].Latitude.Should().Be("51.1");
            result[0].Longitude.Should().Be("-0.1");
            result[0].RegionId.Should().Be("1");
        }

        [Fact]
        public void FilterSitesByPollutants_HandlesNullPollutantList_AndNullPollutantName()
        {
            var siteWithNullList = Site("S1");
            siteWithNullList.Pollutants = null;

            var sites = new List<SiteInfo>
            {
                siteWithNullList,
                Site("S2", "1", Pollutant(null), Pollutant("Ozone"))
            };

            var result = AtomSiteFilterHelper.FilterSitesByPollutants(sites, "Ozone", _logger);

            result.Should().ContainSingle();
            result[0].LocalSiteId.Should().Be("S2");
        }

        [Fact]
        public void FilterSitesByPollutants_DeDuplicatesBySiteId()
        {
            var sites = new List<SiteInfo>
            {
                Site("S1", "1", Pollutant("Ozone")),
                Site("S1", "1", Pollutant("Ozone"))
            };

            var result = AtomSiteFilterHelper.FilterSitesByPollutants(sites, "Ozone", _logger);

            result.Should().ContainSingle();
        }

        [Fact]
        public void FilterSitesByPollutants_UnknownPollutant_IsUsedAsIs()
        {
            var sites = new List<SiteInfo> { Site("S1", "1", Pollutant("Some Custom Gas")) };

            var result = AtomSiteFilterHelper.FilterSitesByPollutants(sites, "Custom Gas", _logger);

            result.Should().ContainSingle();
        }

        // ─── FilterSitesByRegionId ────────────────────────────────────────────

        [Fact]
        public void FilterSitesByRegionId_ReturnsEmpty_WhenSitesNull()
        {
            AtomSiteFilterHelper.FilterSitesByRegionId(null!, "1").Should().BeEmpty();
        }

        [Fact]
        public void FilterSitesByRegionId_ReturnsEmpty_WhenSitesEmpty()
        {
            AtomSiteFilterHelper.FilterSitesByRegionId(new List<SiteInfo>(), "1").Should().BeEmpty();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void FilterSitesByRegionId_ReturnsAll_WhenRegionIdMissing(string? regionId)
        {
            var sites = new List<SiteInfo> { Site("S1", "1"), Site("S2", "2") };

            AtomSiteFilterHelper.FilterSitesByRegionId(sites, regionId!).Should().HaveCount(2);
        }

        [Fact]
        public void FilterSitesByRegionId_FiltersByCommaSeparatedIds_AndIgnoresNullRegionIds()
        {
            var sites = new List<SiteInfo>
            {
                Site("S1", " 1 "),
                Site("S2", "2"),
                Site("S3", "3"),
                Site("S4", null)
            };

            var result = AtomSiteFilterHelper.FilterSitesByRegionId(sites, "1, 3,,");

            result.Select(s => s.LocalSiteId).Should().BeEquivalentTo(new[] { "S1", "S3" });
        }

        // ─── FilterSitesByYearRanges ──────────────────────────────────────────

        [Fact]
        public void FilterSitesByYearRanges_ReturnsSitesWithPollutantsInRange()
        {
            var inRange = Site("S1", "1", Pollutant("Ozone", "01/01/2020", "31/12/2020"));
            var outOfRange = Site("S2", "1", Pollutant("Ozone", "01/01/2010", "31/12/2010"));
            var nullPollutants = Site("S3");
            nullPollutants.Pollutants = null;

            var result = AtomSiteFilterHelper.FilterSitesByYearRanges(
                new List<SiteInfo> { inRange, outOfRange, nullPollutants }, "2020");

            result.Should().ContainSingle().Which.LocalSiteId.Should().Be("S1");
        }

        // ─── SplitEnvironmentType ─────────────────────────────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SplitEnvironmentType_ReturnsNulls_WhenBlank(string? value)
        {
            var (area, site) = AtomSiteFilterHelper.SplitEnvironmentType(value);
            area.Should().BeNull();
            site.Should().BeNull();
        }

        [Fact]
        public void SplitEnvironmentType_SingleWord_ReturnsSiteTypeOnly()
        {
            var (area, site) = AtomSiteFilterHelper.SplitEnvironmentType(" Urban ");
            area.Should().BeNull();
            site.Should().Be("Urban");
        }

        [Fact]
        public void SplitEnvironmentType_MultipleWords_SplitsOnLastSpace()
        {
            var (area, site) = AtomSiteFilterHelper.SplitEnvironmentType("Urban Traffic Background");
            area.Should().Be("Urban Traffic");
            site.Should().Be("Background");
        }

        // ─── ParseYearRanges ──────────────────────────────────────────────────

        [Fact]
        public void ParseYearRanges_ParsesValidYears_AndSkipsInvalid()
        {
            var ranges = AtomSiteFilterHelper.ParseYearRanges("2020,abc,2021");

            ranges.Should().HaveCount(2);
            ranges[0].Start.Should().Be(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            ranges[0].End.Should().Be(new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc));
            ranges[1].Start.Year.Should().Be(2021);
        }

        [Fact]
        public void ParseYearRanges_ReturnsEmpty_WhenNoValidYears()
        {
            AtomSiteFilterHelper.ParseYearRanges("abc,def").Should().BeEmpty();
        }

        // ─── IsPollutantInYearRange ───────────────────────────────────────────

        [Fact]
        public void IsPollutantInYearRange_ReturnsFalse_WhenStartDateInvalid()
        {
            var ranges = AtomSiteFilterHelper.ParseYearRanges("2020");

            AtomSiteFilterHelper.IsPollutantInYearRange(Pollutant("Ozone", "not-a-date"), ranges)
                .Should().BeFalse();
        }

        [Fact]
        public void IsPollutantInYearRange_ReturnsTrue_WhenOverlapsWithEndDate()
        {
            var ranges = AtomSiteFilterHelper.ParseYearRanges("2020");

            AtomSiteFilterHelper.IsPollutantInYearRange(
                Pollutant("Ozone", "01/06/2019", "01/06/2020"), ranges).Should().BeTrue();
        }

        [Fact]
        public void IsPollutantInYearRange_ReturnsFalse_WhenEndDateBeforeRange()
        {
            var ranges = AtomSiteFilterHelper.ParseYearRanges("2020");

            AtomSiteFilterHelper.IsPollutantInYearRange(
                Pollutant("Ozone", "01/01/2018", "31/12/2018"), ranges).Should().BeFalse();
        }

        [Fact]
        public void IsPollutantInYearRange_ReturnsTrue_WhenNoEndDate_AndStartBeforeRangeEnd()
        {
            var ranges = AtomSiteFilterHelper.ParseYearRanges("2020");

            AtomSiteFilterHelper.IsPollutantInYearRange(
                Pollutant("Ozone", "01/01/2019", null), ranges).Should().BeTrue();
        }

        [Fact]
        public void IsPollutantInYearRange_ReturnsFalse_WhenNoEndDate_AndStartAfterRangeEnd()
        {
            var ranges = AtomSiteFilterHelper.ParseYearRanges("2020");

            AtomSiteFilterHelper.IsPollutantInYearRange(
                Pollutant("Ozone", "01/01/2030", "bad-date"), ranges).Should().BeFalse();
        }

        // ─── GetMappedPollutants ──────────────────────────────────────────────

        [Theory]
        [InlineData("Ozone", 1)]
        [InlineData("Fine particulate matter (PM2.5)", 4)]
        [InlineData("Particulate matter (PM10)", 4)]
        [InlineData("NO2", 1)]
        [InlineData("CO", 1)]
        [InlineData("SO2", 1)]
        [InlineData("NOx", 1)]
        [InlineData("NO", 1)]
        public void GetMappedPollutants_MapsAllKnownKeys(string key, int expectedCount)
        {
            AtomSiteFilterHelper.GetMappedPollutants(key, _logger).Should().HaveCount(expectedCount);
        }

        [Fact]
        public void GetMappedPollutants_TrimsNames_AndSupportsMultipleKeys()
        {
            var result = AtomSiteFilterHelper.GetMappedPollutants(" NO2 , CO ", _logger);

            result.Should().BeEquivalentTo(new[] { "Nitrogen dioxide", "Carbon monoxide" });
        }

        [Fact]
        public void GetMappedPollutants_IncludesUnknown_WhenIncludeUnknownsTrue()
        {
            var result = AtomSiteFilterHelper.GetMappedPollutants("Unknown", _logger, includeUnknowns: true);

            result.Should().ContainSingle().Which.Should().Be("Unknown");
        }

        [Fact]
        public void GetMappedPollutants_LogsWarning_WhenUnknown_AndIncludeUnknownsFalse()
        {
            var loggerMock = new Mock<ILogger>();

            var result = AtomSiteFilterHelper.GetMappedPollutants("Unknown", loggerMock.Object);

            result.Should().BeEmpty();
        }

        // ─── GetSiteInfoAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task GetSiteInfoAsync_GroupsDocumentsBySiteAndNetwork()
        {
            var factory = new Mock<IMongoDbClientFactory>();
            var docs = new List<StationDetailDocument>
            {
                new()
                {
                    SiteID = "S1", NetworkID = "N1", SiteName = "Site 1",
                    EnvironmentType = "Urban Background", Latitude = "51.1", Longitude = "-0.1",
                    NetworkType = "Non-AURN", Region = "South",
                    PollutantName = "Ozone", StartDate = "01/01/2020", EndDate = "31/12/2020",
                    pollutantID = "1"
                },
                new()
                {
                    SiteID = "S1", NetworkID = "N1", SiteName = "Site 1",
                    EnvironmentType = "Urban Background",
                    PollutantName = null, pollutantID = "1"
                },
                new()
                {
                    SiteID = "S2", NetworkID = "N2", SiteName = "Site 2",
                    EnvironmentType = null, PollutantName = "Nitrogen dioxide", pollutantID = "2"
                }
            };

            SetupCollection(factory, "aqie_atom_non_aurn_networks_station_details", docs);

            var result = await AtomSiteFilterHelper.GetSiteInfoAsync("1,2", "N1,N2", factory.Object);

            result.Should().HaveCount(2);

            var s1 = result.Single(s => s.LocalSiteId == "S1");
            s1.SiteName.Should().Be("Site 1");
            s1.AreaType.Should().Be("Urban");
            s1.SiteType.Should().Be("Background");
            s1.ZoneRegion.Should().Be("South");
            s1.NetworkType.Should().Be("Non-AURN");
            s1.Pollutants.Should().ContainSingle().Which.Name.Should().Be("Ozone");

            var s2 = result.Single(s => s.LocalSiteId == "S2");
            s2.AreaType.Should().BeNull();
            s2.SiteType.Should().BeNull();
        }

        [Fact]
        public async Task GetSiteInfoAsync_UsesPollutantFilterOnly_WhenNetworkIdEmpty()
        {
            var factory = new Mock<IMongoDbClientFactory>();
            SetupCollection(factory, "aqie_atom_non_aurn_networks_station_details",
                new List<StationDetailDocument>());

            var result = await AtomSiteFilterHelper.GetSiteInfoAsync("1", "", factory.Object);

            result.Should().BeEmpty();
        }

        // ─── ResolvePollutantNameAsync ────────────────────────────────────────

        [Fact]
        public async Task ResolvePollutantNameAsync_ReturnsCommaSeparatedNames_IgnoringEmpty()
        {
            var factory = new Mock<IMongoDbClientFactory>();
            var docs = new List<PollutantMasterDocument>
            {
                new() { pollutantID = "1", pollutantName = "Nitrogen dioxide" },
                new() { pollutantID = "2", pollutantName = "Ozone" },
                new() { pollutantID = "3", pollutantName = "" },
                new() { pollutantID = "4", pollutantName = null }
            };

            SetupCollection(factory, "aqie_atom_non_aurn_networks_pollutant_master", docs);

            var result = await AtomSiteFilterHelper.ResolvePollutantNameAsync("1,2,3,4", _logger, factory.Object);

            result.Should().Be("Nitrogen dioxide,Ozone");
        }

        [Fact]
        public async Task ResolvePollutantNameAsync_ReturnsEmpty_WhenNoMatches()
        {
            var factory = new Mock<IMongoDbClientFactory>();
            SetupCollection(factory, "aqie_atom_non_aurn_networks_pollutant_master",
                new List<PollutantMasterDocument>());

            var result = await AtomSiteFilterHelper.ResolvePollutantNameAsync("99", _logger, factory.Object);

            result.Should().BeEmpty();
        }
    }
}