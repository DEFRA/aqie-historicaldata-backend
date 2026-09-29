using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Services.GeoBoundary;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.IO;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionStationBoundryServiceTests
    {
        private readonly Mock<ILogger<AtomDataSelectionStationBoundryService>> _loggerMock;
        private readonly Mock<IAtomDataSelectionLocalAuthoritiesService> _localAuthServiceMock;
        private readonly Mock<IGeoBoundaryProvider> _boundaryProviderMock;
        private readonly AtomDataSelectionStationBoundryService _service;
   
        public AtomDataSelectionStationBoundryServiceTests()
        {
            _loggerMock = new Mock<ILogger<AtomDataSelectionStationBoundryService>>();
            _localAuthServiceMock = new Mock<IAtomDataSelectionLocalAuthoritiesService>();
            _boundaryProviderMock = new Mock<IGeoBoundaryProvider>();

            // Default: no boundaries found
            _boundaryProviderMock
                .Setup(p => p.GetMany(It.IsAny<IEnumerable<string>>()))
                .Returns(new List<CountryBoundary>());

            _service = new AtomDataSelectionStationBoundryService(
                _loggerMock.Object,
                _localAuthServiceMock.Object,
                _boundaryProviderMock.Object);
        }

        // Helper: build an in-memory boundary instead of reading a GeoJSON file
        private static CountryBoundary MakeBoundary(string name, string wktPolygon)
        {
            var geometry = new WKTReader(NtsGeometryServices.Instance).Read(wktPolygon);
            geometry.SRID = 4326;

            return new CountryBoundary(name, geometry);
        }

        private void SetupBoundaries(params CountryBoundary[] boundaries) =>
            _boundaryProviderMock
                .Setup(p => p.GetMany(It.IsAny<IEnumerable<string>>()))
                .Returns(boundaries.ToList());

        private static SiteInfo MakeSite(string lat, string lon, string? country = null) =>
            new()
            {
                SiteName = "Test",
                Latitude = lat,
                Longitude = lon,
                Country = country ?? string.Empty
            };

        private static LocalAuthorityData MakeLA(double lat, double lon, string region = "South East") =>
            new()
            {
                Latitude = lat,
                Longitude = lon,
                LA_REGION = region
            };

        [Fact]
        public async Task UnknownRegionType_ReturnsEmpty()
        {
            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, "England", "Unknown");

            Assert.Empty(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Country_NullOrWhitespaceRegion_ReturnsEmpty(string? region)
        {
            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo>(), region!, "Country");

            Assert.Empty(result);
        }

        [Fact]
        public async Task Country_NoBoundariesFound_ReturnsEmpty()
        {
            SetupBoundaries(); // none

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, "England", "Country");

            Assert.Empty(result);
        }

        [Fact]
        public async Task Country_SiteInsideBoundary_IsReturnedWithCountrySet()
        {
            SetupBoundaries(MakeBoundary("England",
                "POLYGON((-5 49, 2 49, 2 56, -5 56, -5 49))"));

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, "England", "Country");

            var site = Assert.Single(result);
            Assert.Equal("England", site.Country);
        }

        [Fact]
        public async Task Country_SiteOutsideBoundary_IsFiltered()
        {
            SetupBoundaries(MakeBoundary("England",
                "POLYGON((-5 49, 2 49, 2 56, -5 56, -5 49))"));

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("10.0", "10.0") }, "England", "Country");

            Assert.Empty(result);
        }

        [Fact]
        public async Task LocalAuthority_NullResult_ReturnsEmpty()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync((List<LocalAuthorityData>)null!);

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, "Kent", "LocalAuthority");

            Assert.Empty(result);
        }

        [Fact]
        public async Task LocalAuthority_NearbySite_IsReturned()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData> { MakeLA(51.5, 0.1) });

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, "Kent", "LocalAuthority");

            var site = Assert.Single(result);
            Assert.Equal("South East", site.Country);
        }

        [Fact]
        public async Task LocalAuthority_LondonRegion_MapsToEngland()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData> { MakeLA(51.5, 0.1, "London") });

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, "London", "LocalAuthority");

            Assert.Equal("England", Assert.Single(result).Country);
        }

        [Fact]
        public async Task LocalAuthority_FarAwaySite_IsFiltered()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData> { MakeLA(51.5, 0.1) });

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("55.0", "-3.0") }, "Kent", "LocalAuthority");

            Assert.Empty(result);
        }

        [Fact]
        public async Task Country_Exception_IsLoggedAndReturnsEmpty()
        {
            _boundaryProviderMock
                .Setup(p => p.GetMany(It.IsAny<IEnumerable<string>>()))
                .Throws(new InvalidOperationException("boom"));

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, "England", "Country");

            Assert.Empty(result);
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.Once);
        }

        [Fact]
        public async Task Country_OnlySeparators_ReturnsEmpty()
        {
            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, " , , ", "Country");

            Assert.Empty(result);
        }

        [Fact]
        public async Task Country_MultipleCountries_AreDeduplicatedAndPrioritised()
        {
            List<string>? captured = null;
            _boundaryProviderMock
                .Setup(p => p.GetMany(It.IsAny<IEnumerable<string>>()))
                .Callback<IEnumerable<string>>(c => captured = c.ToList())
                .Returns(new List<CountryBoundary>());

            await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo>(), "England, england , Scotland,Wales,Northern Ireland,Mars", "Country");

            Assert.Equal(
                new[] { "Northern Ireland", "Wales", "Scotland", "England", "Mars" },
                captured);
        }

        [Fact]
        public async Task Country_FirstBoundaryEnvelopeHitButNotCovered_FallsThroughToNext()
        {
            // Point (0.1, 51.5) is inside the envelope of the C-shaped polygon but not inside it
            SetupBoundaries(
                MakeBoundary("Scotland", "POLYGON((-5 49, 2 49, 2 50, -4 50, -4 55, 2 55, 2 56, -5 56, -5 49))"),
                MakeBoundary("England", "POLYGON((-1 51, 1 51, 1 52, -1 52, -1 51))"));

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, "Scotland,England", "Country");

            Assert.Equal("England", Assert.Single(result).Country);
        }

        [Fact]
        public async Task Country_LargeSiteCount_UsesParallelPathAndReturnsAllMatches()
        {
            SetupBoundaries(MakeBoundary("England", "POLYGON((-5 49, 2 49, 2 56, -5 56, -5 49))"));

            var sites = Enumerable.Range(0, 2000).Select(_ => MakeSite("51.5", "0.1")).ToList();

            var result = await _service.GetAtomDataSelectionStationBoundryService(sites, "England", "Country");

            Assert.Equal(2000, result.Count);
            Assert.All(result, s => Assert.Equal("England", s.Country));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "0.1")]
        [InlineData("   ", "0.1")]
        [InlineData("abc", "0.1")]
        [InlineData("51.5", "xyz")]
        [InlineData("95.0", "0.1")]   // lat out of range
        [InlineData("51.5", "200.0")] // lon out of range
        public async Task Country_InvalidLatLon_IsSkipped(string? lat, string? lon)
        {
            SetupBoundaries(MakeBoundary("England", "POLYGON((-5 49, 2 49, 2 56, -5 56, -5 49))"));

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite(lat!, lon!) }, "England", "Country");

            Assert.Empty(result);
        }

        [Fact]
        public async Task LocalAuthority_NullAndZeroCoordinateEntries_AreSkipped()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData> { null!, MakeLA(0d, 0d), MakeLA(51.5, 0.1) });

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, "Kent", "LocalAuthority");

            Assert.Equal("South East", Assert.Single(result).Country);
        }

        [Fact]
        public async Task LocalAuthority_ChoosesNearestCandidate()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData>
                {
                    MakeLA(51.54, 0.1, "Far"),
                    MakeLA(51.5001, 0.1, "Near")
                });

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, "Kent", "LocalAuthority");

            Assert.Equal("Near", Assert.Single(result).Country);
        }

        [Fact]
        public async Task LocalAuthority_InvalidSiteLatLon_IsSkipped()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData> { MakeLA(51.5, 0.1) });

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("not-a-number", "0.1") }, "Kent", "LocalAuthority");

            Assert.Empty(result);
        }

        // Optional, only if you must cover it:
        [Theory]
        [InlineData(1.5d, true, 1.5d)]
        [InlineData(1.5f, true, 1.5d)]
        [InlineData(2, true, 2d)]
        [InlineData(3L, true, 3d)]
        [InlineData("4.5", true, 4.5d)]
        [InlineData("abc", false, 0d)]
        [InlineData(null, false, 0d)]
        public void TryToDouble_HandlesAllSupportedTypes(object? input, bool expected, double expectedResult)
        {
            var method = typeof(AtomDataSelectionStationBoundryService)
                .GetMethod("TryToDouble", BindingFlags.NonPublic | BindingFlags.Static)!;

            var args = new object?[] { input, 0d };
            var ok = (bool)method.Invoke(null, args)!;

            Assert.Equal(expected, ok);
            Assert.Equal(expectedResult, (double)args[1]!);
        }
    }
}