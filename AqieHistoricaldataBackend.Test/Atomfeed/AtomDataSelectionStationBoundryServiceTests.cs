using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionStationBoundryServiceTests
    {
        private readonly Mock<ILogger<AtomDataSelectionStationBoundryService>> _loggerMock;
        private readonly Mock<IAtomDataSelectionLocalAuthoritiesService> _localAuthServiceMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IHostEnvironment> _envMock;
        private readonly Mock<IFileProvider> _fileProviderMock;
        private readonly AtomDataSelectionStationBoundryService _service;

        // Simple polygon that covers a known test point (lon=0, lat=51) inside England
        private const string EnglandGeoJson = """
            {
              "type": "FeatureCollection",
              "features": [{
                "type": "Feature",
                "geometry": {
                  "type": "Polygon",
                  "coordinates": [[[-5,49],[2,49],[2,56],[-5,56],[-5,49]]]
                },
                "properties": {}
              }]
            }
            """;

        // Multi-feature GeoJSON to test union logic
        private const string MultiFeatureGeoJson = """
            {
              "type": "FeatureCollection",
              "features": [
                {
                  "type": "Feature",
                  "geometry": {
                    "type": "Polygon",
                    "coordinates": [[[-5,49],[2,49],[2,52],[-5,52],[-5,49]]]
                  },
                  "properties": {}
                },
                {
                  "type": "Feature",
                  "geometry": {
                    "type": "Polygon",
                    "coordinates": [[[-5,52],[2,52],[2,56],[-5,56],[-5,52]]]
                  },
                  "properties": {}
                }
              ]
            }
            """;

        // Minimal single-geometry GeoJSON
        private const string SingleGeomGeoJson = """
            {
              "type": "Polygon",
              "coordinates": [[[-5,49],[2,49],[2,56],[-5,56],[-5,49]]]
            }
            """;

        public AtomDataSelectionStationBoundryServiceTests()
        {
            _loggerMock = new Mock<ILogger<AtomDataSelectionStationBoundryService>>();
            _localAuthServiceMock = new Mock<IAtomDataSelectionLocalAuthoritiesService>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _fileProviderMock = new Mock<IFileProvider>();
            _envMock = new Mock<IHostEnvironment>();
            _envMock.SetupGet(e => e.ContentRootFileProvider).Returns(_fileProviderMock.Object);
            _envMock.SetupGet(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

            _service = new AtomDataSelectionStationBoundryService(
                _loggerMock.Object,
                _localAuthServiceMock.Object,
                _httpClientFactoryMock.Object,
                _envMock.Object);
        }

        // -------------------------------------------------------
        // Helper: write a temp GeoJSON file and set up file provider mock
        // -------------------------------------------------------
        private static string WriteTempGeoJson(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.geojson");
            File.WriteAllText(path, content);
            return path;
        }

        private void SetupFileProvider(string physicalPath, bool exists = true)
        {
            var fileInfoMock = new Mock<IFileInfo>();
            fileInfoMock.SetupGet(f => f.Exists).Returns(exists);
            fileInfoMock.SetupGet(f => f.PhysicalPath).Returns(physicalPath);
            _fileProviderMock
                .Setup(p => p.GetFileInfo(It.IsAny<string>()))
                .Returns(fileInfoMock.Object);
        }

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

        // ===================================================
        // 1. regiontype = unknown → empty list
        // ===================================================
        [Fact]
        public async Task UnknownRegionType_ReturnsEmpty()
        {
            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.1") }, "England", "Unknown");

            Assert.Empty(result);
        }

        // ===================================================
        // 2. Country – null / empty / whitespace region
        // ===================================================
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

        // ===================================================
        // 3. Country – region parses to zero valid entries
        // ===================================================
        [Fact]
        public async Task Country_RegionWithOnlyCommas_ReturnsEmpty()
        {
            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo>(), ",,,", "Country");

            Assert.Empty(result);
        }

        // ===================================================
        // 4. Country – GeoJSON file not resolvable → no boundaries → empty
        // ===================================================
        [Fact]
        public async Task Country_FileNotFound_ReturnsEmpty()
        {
            SetupFileProvider(physicalPath: null!, exists: false);

            var sites = new List<SiteInfo> { MakeSite("51.5", "0.1") };

            // Uses fresh country key so the static cache doesn't interfere
            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, "England_Missing_" + Guid.NewGuid(), "Country");

            Assert.Empty(result);
        }

        // ===================================================
        // 5. Country – site with null/empty lat or lon is skipped
        // ===================================================
        [Theory]
        [InlineData(null, "0.1")]
        [InlineData("51.5", null)]
        [InlineData("", "0.1")]
        [InlineData("51.5", "")]
        [InlineData("  ", "0.1")]
        [InlineData("notanumber", "0.1")]
        [InlineData("51.5", "notanumber")]
        public async Task Country_InvalidLatLon_SiteIsSkipped(string? lat, string? lon)
        {
            var tmpFile = WriteTempGeoJson(EnglandGeoJson);
            SetupFileProvider(tmpFile);

            var sites = new List<SiteInfo> { MakeSite(lat!, lon!) };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, "EnglandTest_" + Guid.NewGuid(), "Country");

            // Invalid coords → nothing passes the boundary check
            Assert.Empty(result);
            File.Delete(tmpFile);
        }

        // ===================================================
        // 6. Country – site outside boundary envelope → excluded
        // ===================================================
        [Fact]
        public async Task Country_SiteOutsideBoundary_IsExcluded()
        {
            var tmpFile = WriteTempGeoJson(EnglandGeoJson);
            SetupFileProvider(tmpFile);

            // Paris – clearly outside the test polygon
            var sites = new List<SiteInfo> { MakeSite("48.8566", "2.3522") };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, "EnglandOutside_" + Guid.NewGuid(), "Country");

            Assert.Empty(result);
            File.Delete(tmpFile);
        }

        // ===================================================
        // 7. Country – site inside boundary → included and country set
        // ===================================================
        [Fact]
        public async Task Country_SiteInsideBoundary_IsIncludedWithCountryName()
        {
            var tmpFile = WriteTempGeoJson(EnglandGeoJson);
            SetupFileProvider(tmpFile);

            var uniqueCountry = "EnglandInside_" + Guid.NewGuid();
            var sites = new List<SiteInfo> { MakeSite("51.5", "0.0") };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, uniqueCountry, "Country");

            Assert.Single(result);
            Assert.Equal(uniqueCountry, result[0].Country);
            File.Delete(tmpFile);
        }

        // ===================================================
        // 8. Country – multi-feature GeoJSON (union path)
        // ===================================================
        [Fact]
        public async Task Country_MultiFeatureGeoJson_SiteInsideUnion_IsIncluded()
        {
            var tmpFile = WriteTempGeoJson(MultiFeatureGeoJson);
            SetupFileProvider(tmpFile);

            var uniqueCountry = "MultiFeature_" + Guid.NewGuid();
            var sites = new List<SiteInfo> { MakeSite("53.0", "0.0") };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, uniqueCountry, "Country");

            Assert.Single(result);
            File.Delete(tmpFile);
        }

        // ===================================================
        // 9. Country – single-geometry GeoJSON fallback path
        // ===================================================
        [Fact]
        public async Task Country_SingleGeometryGeoJson_SiteInsideBoundary_IsIncluded()
        {
            var tmpFile = WriteTempGeoJson(SingleGeomGeoJson);
            SetupFileProvider(tmpFile);

            var uniqueCountry = "SingleGeom_" + Guid.NewGuid();
            var sites = new List<SiteInfo> { MakeSite("51.5", "0.0") };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, uniqueCountry, "Country");

            Assert.Single(result);
            File.Delete(tmpFile);
        }

        // ===================================================
        // 10. Country – lat/lon out-of-range values are rejected
        // ===================================================
        [Theory]
        [InlineData("91", "0")]
        [InlineData("-91", "0")]
        [InlineData("51", "181")]
        [InlineData("51", "-181")]
        public async Task Country_OutOfRangeCoordinates_SiteIsSkipped(string lat, string lon)
        {
            var tmpFile = WriteTempGeoJson(EnglandGeoJson);
            SetupFileProvider(tmpFile);

            var sites = new List<SiteInfo> { MakeSite(lat, lon) };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, "EnglandRange_" + Guid.NewGuid(), "Country");

            Assert.Empty(result);
            File.Delete(tmpFile);
        }

        // ===================================================
        // 11. Country – duplicate country names deduplicated (case-insensitive)
        // ===================================================
        [Fact]
        public async Task Country_DuplicateCountryNames_DeduplicatedCaseInsensitive()
        {
            var tmpFile = WriteTempGeoJson(EnglandGeoJson);
            SetupFileProvider(tmpFile);

            var uniqueKey = "EnglandDup_" + Guid.NewGuid();
            // Same key twice with different casing
            var region = $"{uniqueKey},{uniqueKey.ToUpperInvariant()}";
            var sites = new List<SiteInfo> { MakeSite("51.5", "0.0") };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, region, "Country");

            // Should load boundary only once; site still matched
            Assert.Single(result);
            File.Delete(tmpFile);
        }

        // ===================================================
        // 12. Country – country priority ordering
        // ===================================================
        [Fact]
        public void CountryPriority_KnownCountries_ReturnExpectedOrder()
        {
            var method = typeof(AtomDataSelectionStationBoundryService)
                .GetMethod("CountryPriority", BindingFlags.NonPublic | BindingFlags.Static)!;

            Assert.Equal(0, (int)method.Invoke(null, ["Northern Ireland"])!);
            Assert.Equal(1, (int)method.Invoke(null, ["Wales"])!);
            Assert.Equal(2, (int)method.Invoke(null, ["Scotland"])!);
            Assert.Equal(3, (int)method.Invoke(null, ["England"])!);
            Assert.Equal(4, (int)method.Invoke(null, ["Unknown"])!);
        }

        // ===================================================
        // 13. Country – regiontype case insensitivity
        // ===================================================
        [Theory]
        [InlineData("country")]
        [InlineData("COUNTRY")]
        [InlineData("Country")]
        public async Task Country_RegionTypeCaseInsensitive_EmptyListReturnedOnMissingBoundaries(string regionType)
        {
            SetupFileProvider(physicalPath: null!, exists: false);

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo>(), "England_Case_" + Guid.NewGuid(), regionType);

            // Boundary fails to load → empty
            Assert.Empty(result);
        }

        // ===================================================
        // 14. Country – empty sites list → empty result
        // ===================================================
        [Fact]
        public async Task Country_EmptySiteList_ReturnsEmpty()
        {
            var tmpFile = WriteTempGeoJson(EnglandGeoJson);
            SetupFileProvider(tmpFile);

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo>(), "EnglandEmpty_" + Guid.NewGuid(), "Country");

            Assert.Empty(result);
            File.Delete(tmpFile);
        }

        // ===================================================
        // 15. LocalAuthority – service returns null → empty
        // ===================================================
        [Fact]
        public async Task LocalAuthority_ServiceReturnsNull_ReturnsEmpty()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync((List<LocalAuthorityData>)null!);

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.0") }, "TestLA", "LocalAuthority");

            Assert.Empty(result);
        }

        // ===================================================
        // 16. LocalAuthority – service returns empty list → empty result
        // ===================================================
        [Fact]
        public async Task LocalAuthority_ServiceReturnsEmptyList_ReturnsEmpty()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData>());

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.0") }, "TestLA", "LocalAuthority");

            Assert.Empty(result);
        }

        // ===================================================
        // 17. LocalAuthority – LA with lat=0, lon=0 is skipped
        // ===================================================
        [Fact]
        public async Task LocalAuthority_LAWithZeroCoords_IsSkipped()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData> { MakeLA(0, 0) });

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.0") }, "TestLA", "LocalAuthority");

            Assert.Empty(result);
        }

        // ===================================================
        // 18. LocalAuthority – site with invalid lat/lon is skipped
        // ===================================================
        [Fact]
        public async Task LocalAuthority_SiteWithInvalidCoords_IsSkipped()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData> { MakeLA(51.5, 0.0) });

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("invalid", "0.0") }, "TestLA", "LocalAuthority");

            Assert.Empty(result);
        }

        // ===================================================
        // 19. LocalAuthority – site within 50 km → included, country set
        // ===================================================
        [Fact]
        public async Task LocalAuthority_SiteWithin50km_IsIncludedWithRegion()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData> { MakeLA(51.5, 0.0, "South East") });

            // Same point → distance = 0
            var sites = new List<SiteInfo> { MakeSite("51.5", "0.0") };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, "TestLA", "LocalAuthority");

            Assert.Single(result);
            Assert.Equal("South East", result[0].Country);
        }

        // ===================================================
        // 20. LocalAuthority – "London" region mapped to "England"
        // ===================================================
        [Fact]
        public async Task LocalAuthority_LondonRegion_MappedToEngland()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData> { MakeLA(51.5, 0.0, "London") });

            var sites = new List<SiteInfo> { MakeSite("51.5", "0.0") };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, "TestLA", "LocalAuthority");

            Assert.Single(result);
            Assert.Equal("England", result[0].Country);
        }

        // ===================================================
        // 21. LocalAuthority – "LONDON" (uppercase) also mapped to "England"
        // ===================================================
        [Fact]
        public async Task LocalAuthority_LondonCaseInsensitive_MappedToEngland()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData> { MakeLA(51.5, 0.0, "LONDON") });

            var sites = new List<SiteInfo> { MakeSite("51.5", "0.0") };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, "TestLA", "LocalAuthority");

            Assert.Single(result);
            Assert.Equal("England", result[0].Country);
        }

        // ===================================================
        // 22. LocalAuthority – site beyond 50 km is excluded
        // ===================================================
        [Fact]
        public async Task LocalAuthority_SiteBeyond50km_IsExcluded()
        {
            // LA near London; site near Edinburgh (~640 km away)
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(new List<LocalAuthorityData> { MakeLA(51.5, -0.1, "South East") });

            var sites = new List<SiteInfo> { MakeSite("55.9533", "-3.1883") };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, "TestLA", "LocalAuthority");

            Assert.Empty(result);
        }

        // ===================================================
        // 23. LocalAuthority – closest LA wins when multiple candidates
        // ===================================================
        [Fact]
        public async Task LocalAuthority_ClosestLAWins()
        {
            var las = new List<LocalAuthorityData>
            {
                MakeLA(51.5, 0.0, "Near"),       // ~0 km from site
                MakeLA(51.8, 0.0, "Far"),          // ~33 km from site
            };

            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(las);

            var sites = new List<SiteInfo> { MakeSite("51.5", "0.0") };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, "TestLA", "LocalAuthority");

            Assert.Single(result);
            Assert.Equal("Near", result[0].Country);
        }

        // ===================================================
        // 24. LocalAuthority – null LA entry in list is skipped
        // ===================================================
        [Fact]
        public async Task LocalAuthority_NullLAEntry_IsSkipped()
        {
            var las = new List<LocalAuthorityData> { null!, MakeLA(51.5, 0.0, "South East") };

            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ReturnsAsync(las);

            var sites = new List<SiteInfo> { MakeSite("51.5", "0.0") };

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                sites, "TestLA", "LocalAuthority");

            Assert.Single(result);
        }

        // ===================================================
        // 25. LocalAuthority – service throws → returns empty, logs error
        // ===================================================
        [Fact]
        public async Task LocalAuthority_ServiceThrows_ReturnsEmpty()
        {
            _localAuthServiceMock
                .Setup(s => s.GetAtomDataSelectionLocalAuthoritiesService(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Service error"));

            var result = await _service.GetAtomDataSelectionStationBoundryService(
                new List<SiteInfo> { MakeSite("51.5", "0.0") }, "TestLA", "LocalAuthority");

            Assert.Empty(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        // ===================================================
        // 26. TryToDouble – all supported type overloads
        // ===================================================
        [Theory]
        [InlineData(typeof(double), 1.5d, 1.5d)]
        [InlineData(typeof(float), 1.5f, 1.5d)]
        [InlineData(typeof(int), 10, 10d)]
        [InlineData(typeof(long), 100L, 100d)]
        public void TryToDouble_SupportedTypes_ReturnTrue(Type _, object value, double expected)
        {
            var method = typeof(AtomDataSelectionStationBoundryService)
                .GetMethod("TryToDouble", BindingFlags.NonPublic | BindingFlags.Static)!;

            var args = new object?[] { value, null };
            var returned = (bool)method.Invoke(null, args)!;

            Assert.True(returned);
            Assert.Equal(expected, (double)args[1]!);
        }

        [Fact]
        public void TryToDouble_DecimalValue_ReturnsTrueAndConverts()
        {
            var method = typeof(AtomDataSelectionStationBoundryService)
                .GetMethod("TryToDouble", BindingFlags.NonPublic | BindingFlags.Static)!;

            var args = new object?[] { 2.5m, null };
            var returned = (bool)method.Invoke(null, args)!;

            Assert.True(returned);
            Assert.Equal(2.5d, (double)args[1]!);
        }

        [Fact]
        public void TryToDouble_ValidString_ReturnsTrueAndConverts()
        {
            var method = typeof(AtomDataSelectionStationBoundryService)
                .GetMethod("TryToDouble", BindingFlags.NonPublic | BindingFlags.Static)!;

            var args = new object?[] { "3.14", null };
            var returned = (bool)method.Invoke(null, args)!;

            Assert.True(returned);
            Assert.Equal(3.14d, (double)args[1]!, 5);
        }

        [Fact]
        public void TryToDouble_InvalidString_ReturnsFalse()
        {
            var method = typeof(AtomDataSelectionStationBoundryService)
                .GetMethod("TryToDouble", BindingFlags.NonPublic | BindingFlags.Static)!;

            var args = new object?[] { "not-a-number", null };
            var returned = (bool)method.Invoke(null, args)!;

            Assert.False(returned);
        }

        [Fact]
        public void TryToDouble_NullValue_ReturnsFalse()
        {
            var method = typeof(AtomDataSelectionStationBoundryService)
                .GetMethod("TryToDouble", BindingFlags.NonPublic | BindingFlags.Static)!;

            var args = new object?[] { null, null };
            var returned = (bool)method.Invoke(null, args)!;

            Assert.False(returned);
        }

        // ===================================================
        // 27. HaversineMeters – same point → 0 distance
        // ===================================================
        [Fact]
        public void HaversineMeters_SamePoint_ReturnsZero()
        {
            var method = typeof(AtomDataSelectionStationBoundryService)
                .GetMethod("HaversineMeters", BindingFlags.NonPublic | BindingFlags.Static)!;

            var result = (double)method.Invoke(null, [51.5, 0.0, 51.5, 0.0])!;

            Assert.Equal(0d, result, 6);
        }

        // ===================================================
        // 28. HaversineMeters – London to Paris ~340 km
        // ===================================================
        [Fact]
        public void HaversineMeters_LondonToParis_ApproximatelyCorrect()
        {
            var method = typeof(AtomDataSelectionStationBoundryService)
                .GetMethod("HaversineMeters", BindingFlags.NonPublic | BindingFlags.Static)!;

            // London: 51.5074, -0.1278  Paris: 48.8566, 2.3522
            var result = (double)method.Invoke(null, [51.5074, -0.1278, 48.8566, 2.3522])!;

            // Expected ≈ 340,000 m; allow ±5 km tolerance
            Assert.InRange(result, 335_000d, 345_000d);
        }

        // ===================================================
        // 29. TryParseLatLon – valid input
        // ===================================================
        [Fact]
        public void TryParseLatLon_ValidInput_ReturnsTrueWithValues()
        {
            var method = typeof(AtomDataSelectionStationBoundryService)
                .GetMethod("TryParseLatLon", BindingFlags.NonPublic | BindingFlags.Static)!;

            var args = new object?[] { "51.5", "0.1", 0d, 0d };
            var returned = (bool)method.Invoke(null, args)!;

            Assert.True(returned);
            Assert.Equal(51.5d, (double)args[2]!);
            Assert.Equal(0.1d, (double)args[3]!);
        }

        // ===================================================
        // 30. TryParseLatLon – boundary values (exact ±90 / ±180)
        // ===================================================
        [Theory]
        [InlineData("90", "180", true)]
        [InlineData("-90", "-180", true)]
        [InlineData("90.0001", "0", false)]
        [InlineData("0", "180.0001", false)]
        public void TryParseLatLon_BoundaryValues_ReturnsExpected(string lat, string lon, bool expected)
        {
            var method = typeof(AtomDataSelectionStationBoundryService)
                .GetMethod("TryParseLatLon", BindingFlags.NonPublic | BindingFlags.Static)!;

            var args = new object?[] { lat, lon, 0d, 0d };
            var returned = (bool)method.Invoke(null, args)!;

            Assert.Equal(expected, returned);
        }
    }
}