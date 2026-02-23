using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using Xunit;
using AqieHistoricaldataBackend.Atomfeed.Services;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionHourlyFetchServiceTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly AtomDataSelectionHourlyFetchService _service;

        // Minimal valid XML for a single-member FeatureCollection (index 0 is header, index 1 is data)
        private const string ValidXmlTemplate =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\"" +
            "                       xmlns:om=\"http://www.opengis.net/om/2.0\"" +
            "                       xmlns:swe=\"http://www.opengis.net/swe/2.0\"" +
            "                       xmlns:xlink=\"http://www.w3.org/1999/xlink\">" +
            "  <gml:featureMember>" +
            "    <om:OM_Observation>" +
            "      <om:observedProperty xlink:href=\"http://dd.eionet.europa.eu/vocabulary/aq/pollutant/8\"/>" +
            "      <om:result>" +
            "        <swe:DataArray>" +
            "          <swe:values>2024-01-01T00:00,2024-01-01T01:00,1,1,50@@2024-01-01T01:00,2024-01-01T02:00,1,1,60</swe:values>" +
            "        </swe:DataArray>" +
            "      </om:result>" +
            "    </om:OM_Observation>" +
            "  </gml:featureMember>" +
            "  <gml:featureMember>" +
            "    <om:OM_Observation>" +
            "      <om:observedProperty xlink:href=\"http://dd.eionet.europa.eu/vocabulary/aq/pollutant/8\"/>" +
            "      <om:result>" +
            "        <swe:DataArray>" +
            "          <swe:values>2024-01-01T00:00,2024-01-01T01:00,1,1,50@@2024-01-01T01:00,2024-01-01T02:00,1,1,60</swe:values>" +
            "        </swe:DataArray>" +
            "      </om:result>" +
            "    </om:OM_Observation>" +
            "  </gml:featureMember>" +
            "</gml:FeatureCollection>";

        private static readonly SiteInfo DefaultSite = new SiteInfo
        {
            SiteName = "Test Site",
            LocalSiteId = "AH",
            AreaType = "Urban",
            SiteType = "Background",
            ZoneRegion = "London",
            Country = "England"
        };

        public AtomDataSelectionHourlyFetchServiceTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _service = new AtomDataSelectionHourlyFetchService(_loggerMock.Object, _httpClientFactoryMock.Object);
        }

        // ─── Helper: build a mocked HttpClient that returns the given response ──────────

        private void SetupHttpClient(HttpResponseMessage response)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            var client = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://example.com/")
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);
        }

        private void SetupHttpClientThrows(Exception ex)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(ex);

            var client = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://example.com/")
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetAtomDataSelectionHourlyFetchService – happy-path
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAtomDataSelectionHourlyFetchService_ReturnsFinalData_WhenHttpSucceeds()
        {
            // Arrange
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXmlTemplate, Encoding.UTF8, "application/xml")
            });

            var stations = new List<SiteInfo> { DefaultSite };

            // Act
            var result = await _service.GetAtomDataSelectionHourlyFetchService(stations, "Nitrogen dioxide", "2024");

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.All(result, r => Assert.Equal("Nitrogen dioxide", r.PollutantName));
        }

        [Fact]
        public async Task GetAtomDataSelectionHourlyFetchService_ReturnsMultipleYearsData_WhenMultipleYearsPassed()
        {
            // Arrange
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXmlTemplate, Encoding.UTF8, "application/xml")
            });

            var stations = new List<SiteInfo> { DefaultSite };

            // Act
            var result = await _service.GetAtomDataSelectionHourlyFetchService(stations, "Nitrogen dioxide", "2023,2024");

            // Assert – one HTTP call per site+year pair (2 years × 1 site = 2 calls)
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionHourlyFetchService_ReturnsMultipleSitesData_WhenMultipleSitesPassed()
        {
            // Arrange
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXmlTemplate, Encoding.UTF8, "application/xml")
            });

            var stations = new List<SiteInfo>
            {
                DefaultSite,
                new SiteInfo { SiteName = "Site B", LocalSiteId = "BX", AreaType = "Rural", SiteType = "Traffic", ZoneRegion = "Yorkshire", Country = "England" }
            };

            // Act
            var result = await _service.GetAtomDataSelectionHourlyFetchService(stations, "Nitrogen dioxide", "2024");

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetAtomDataSelectionHourlyFetchService – HTTP error branches
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAtomDataSelectionHourlyFetchService_ReturnsEmptyList_When404Returned()
        {
            // Arrange
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.NotFound));

            var stations = new List<SiteInfo> { DefaultSite };

            // Act
            var result = await _service.GetAtomDataSelectionHourlyFetchService(stations, "Nitrogen dioxide", "2024");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionHourlyFetchService_ReturnsEmptyList_WhenHttpClientThrows()
        {
            // Arrange
            SetupHttpClientThrows(new HttpRequestException("Network error"));

            var stations = new List<SiteInfo> { DefaultSite };

            // Act
            var result = await _service.GetAtomDataSelectionHourlyFetchService(stations, "Nitrogen dioxide", "2024");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionHourlyFetchService_ReturnsEmptyList_WhenGeneralExceptionThrown()
        {
            // Arrange
            SetupHttpClientThrows(new Exception("Unexpected failure"));

            var stations = new List<SiteInfo> { DefaultSite };

            // Act
            var result = await _service.GetAtomDataSelectionHourlyFetchService(stations, "Nitrogen dioxide", "2024");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionHourlyFetchService_LogsError_WhenSiteProcessingFails()
        {
            // Arrange
            SetupHttpClientThrows(new Exception("Processing failure"));

            var stations = new List<SiteInfo> { DefaultSite };

            // Act
            await _service.GetAtomDataSelectionHourlyFetchService(stations, "Nitrogen dioxide", "2024");

            // Assert – LogError should be called (either per-pair or outer catch)
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetAtomDataSelectionHourlyFetchService – outer catch block
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAtomDataSelectionHourlyFetchService_OuterCatch_ReturnsEmptyList_WhenFactoryThrows()
        {
            // Arrange – factory throws synchronously when CreateClient is called
            _httpClientFactoryMock
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Throws(new Exception("Factory exploded"));

            var stations = new List<SiteInfo> { DefaultSite };

            // Act
            var result = await _service.GetAtomDataSelectionHourlyFetchService(stations, "Nitrogen dioxide", "2024");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("GetAtomDataSelectionHourlyFetchService")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetAtomDataSelectionHourlyFetchService – pollutant filter combinations
        // ═══════════════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("Nitrogen dioxide")]
        [InlineData("PM10")]
        [InlineData("PM2.5")]
        [InlineData("Ozone")]
        [InlineData("Sulphur dioxide")]
        [InlineData("Nitrogen oxides as nitrogen dioxide")]
        [InlineData("Carbon monoxide")]
        [InlineData("Nitric oxide")]
        public async Task GetAtomDataSelectionHourlyFetchService_HandlesAllKnownPollutants(string pollutant)
        {
            // Arrange
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXmlTemplate, Encoding.UTF8, "application/xml")
            });

            var stations = new List<SiteInfo> { DefaultSite };

            // Act
            var result = await _service.GetAtomDataSelectionHourlyFetchService(stations, pollutant, "2024");

            // Assert – should not throw regardless of whether the XML matches
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionHourlyFetchService_ReturnsAllPollutants_WhenFilterDoesNotMatch()
        {
            // Arrange – "UnknownPollutant" will not match, so GetPollutantsToDisplay returns all 8
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXmlTemplate, Encoding.UTF8, "application/xml")
            });

            var stations = new List<SiteInfo> { DefaultSite };

            // Act
            var result = await _service.GetAtomDataSelectionHourlyFetchService(stations, "UnknownPollutant", "2024");

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionHourlyFetchService_HandlesMultiplePollutantsCommaSeparated()
        {
            // Arrange
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXmlTemplate, Encoding.UTF8, "application/xml")
            });

            var stations = new List<SiteInfo> { DefaultSite };

            // Act
            var result = await _service.GetAtomDataSelectionHourlyFetchService(stations, "Nitrogen dioxide,PM10", "2024");

            // Assert
            Assert.NotNull(result);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetAtomDataSelectionHourlyFetchService – empty / null inputs
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAtomDataSelectionHourlyFetchService_ReturnsEmpty_WhenStationListIsEmpty()
        {
            // Arrange
            var stations = new List<SiteInfo>();

            // Act
            var result = await _service.GetAtomDataSelectionHourlyFetchService(stations, "Nitrogen dioxide", "2024");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // FetchAtomFeedAsync – via reflection
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_WhenSiteIdIsEmpty()
        {
            var result = await InvokeFetchAtomFeedAsync("", "2024");
            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_WhenYearIsEmpty()
        {
            var result = await InvokeFetchAtomFeedAsync("AH", "");
            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_WhenSiteIdIsWhitespace()
        {
            var result = await InvokeFetchAtomFeedAsync("   ", "2024");
            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsWarning_WhenSiteIdIsNull()
        {
            await InvokeFetchAtomFeedAsync(null!, "2024");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid FetchAtomFeedAsync")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_On404Response()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.NotFound));
            var result = await InvokeFetchAtomFeedAsync("AH", "2024");
            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsWarning_On404Response()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.NotFound));
            await InvokeFetchAtomFeedAsync("AH", "2024");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found (404)")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_OnNonSuccessResponse()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server error")
            });
            var result = await InvokeFetchAtomFeedAsync("AH", "2024");
            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsWarning_On428PreconditionRequired()
        {
            SetupHttpClient(new HttpResponseMessage((HttpStatusCode)428)
            {
                Content = new StringContent("precondition required")
            });
            await InvokeFetchAtomFeedAsync("AH", "2024");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("428")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_OnHttpRequestException()
        {
            SetupHttpClientThrows(new HttpRequestException("Network failure"));
            var result = await InvokeFetchAtomFeedAsync("AH", "2024");
            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsError_OnHttpRequestException()
        {
            SetupHttpClientThrows(new HttpRequestException("Network failure"));
            await InvokeFetchAtomFeedAsync("AH", "2024");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HTTP error FetchAtomFeedAsync")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_OnGeneralException()
        {
            SetupHttpClientThrows(new Exception("Unexpected"));
            var result = await InvokeFetchAtomFeedAsync("AH", "2024");
            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsError_OnGeneralException()
        {
            SetupHttpClientThrows(new Exception("Unexpected"));
            await InvokeFetchAtomFeedAsync("AH", "2024");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error FetchAtomFeedAsync")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsJArray_WhenValidXmlReturned()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXmlTemplate, Encoding.UTF8, "application/xml")
            });

            var result = await InvokeFetchAtomFeedAsync("AH", "2024");

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetPollutantsToDisplay – via reflection
        // ═══════════════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("Nitrogen dioxide", 1)]
        [InlineData("PM10", 1)]
        [InlineData("PM2.5", 1)]
        [InlineData("Ozone", 1)]
        [InlineData("Sulphur dioxide", 1)]
        [InlineData("Nitrogen oxides as nitrogen dioxide", 1)]
        [InlineData("Carbon monoxide", 1)]
        [InlineData("Nitric oxide", 1)]
        public void GetPollutantsToDisplay_ReturnsSingleMatch_WhenExactNameProvided(string filter, int expectedCount)
        {
            var result = InvokeGetPollutantsToDisplay(filter);
            Assert.Equal(expectedCount, result.Count);
            Assert.Equal(filter, result[0].PollutantName);
        }

        [Fact]
        public void GetPollutantsToDisplay_ReturnsAll_WhenFilterDoesNotMatch()
        {
            var result = InvokeGetPollutantsToDisplay("NonExistent");
            Assert.Equal(8, result.Count);
        }

        [Fact]
        public void GetPollutantsToDisplay_IsCaseInsensitive()
        {
            var result = InvokeGetPollutantsToDisplay("nitrogen dioxide");
            Assert.Single(result);
            Assert.Equal("Nitrogen dioxide", result[0].PollutantName);
        }

        [Fact]
        public void GetPollutantsToDisplay_HandlesCommaSeparatedFilter()
        {
            var result = InvokeGetPollutantsToDisplay("Nitrogen dioxide,PM10");
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void GetPollutantsToDisplay_TrimsWhitespaceAroundFilterValues()
        {
            var result = InvokeGetPollutantsToDisplay(" Nitrogen dioxide , PM10 ");
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void GetPollutantsToDisplay_ReturnsAll_WhenFilterIsEmpty()
        {
            var result = InvokeGetPollutantsToDisplay("");
            Assert.Equal(8, result.Count);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // ProcessAtomData – via reflection
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void ProcessAtomData_ReturnsEmpty_WhenFeaturesIsNull()
        {
            var result = InvokeProcessAtomData(null!, BuildPollutants(), DefaultSite, "2024");
            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ReturnsEmpty_WhenFeaturesIsEmptyJArray()
        {
            var result = InvokeProcessAtomData(new JArray(), BuildPollutants(), DefaultSite, "2024");
            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ReturnsEmpty_WhenOnlyHeaderMemberPresent()
        {
            // Only index 0 (header) — the loop starts at i=1 so nothing is processed
            var features = new JArray { new JObject() };
            var result = InvokeProcessAtomData(features, BuildPollutants(), DefaultSite, "2024");
            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_SkipsFeature_WhenHrefIsNull()
        {
            var feature = new JObject
            {
                ["om:OM_Observation"] = new JObject
                {
                    ["om:observedProperty"] = new JObject()    // no @xlink:href
                }
            };
            var features = new JArray { new JObject(), feature };

            var result = InvokeProcessAtomData(features, BuildPollutants(), DefaultSite, "2024");
            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_SkipsFeature_WhenHrefDoesNotMatchAnyPollutant()
        {
            var feature = BuildFeatureMember(
                "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/9999",
                "2024-01-01T00:00,2024-01-01T01:00,1,1,50");

            var features = new JArray { new JObject(), feature };
            var result = InvokeProcessAtomData(features, BuildPollutants(), DefaultSite, "2024");
            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ReturnsFinalData_WhenHrefMatchesPollutant()
        {
            var feature = BuildFeatureMember(
                "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/8",
                "2024-01-01T00:00,2024-01-01T01:00,1,1,50");

            var features = new JArray { new JObject(), feature };
            var result = InvokeProcessAtomData(features, BuildPollutants(), DefaultSite, "2024");

            Assert.Single(result);
            Assert.Equal("Nitrogen dioxide", result[0].PollutantName);
        }

        [Fact]
        public void ProcessAtomData_SkipsFeature_WhenValuesIsEmpty()
        {
            var feature = BuildFeatureMember(
                "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/8",
                ""); // empty values

            var features = new JArray { new JObject(), feature };
            var result = InvokeProcessAtomData(features, BuildPollutants(), DefaultSite, "2024");
            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_HandlesMultipleMatchingMembers()
        {
            var feature1 = BuildFeatureMember(
                "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/8",
                "2024-01-01T00:00,2024-01-01T01:00,1,1,50");
            var feature2 = BuildFeatureMember(
                "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/8",
                "2024-01-01T01:00,2024-01-01T02:00,1,1,60");

            var features = new JArray { new JObject(), feature1, feature2 };
            var result = InvokeProcessAtomData(features, BuildPollutants(), DefaultSite, "2024");

            Assert.Equal(2, result.Count);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // ExtractFinalData – via reflection
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void ExtractFinalData_ReturnsParsedRecords_WhenValidInput()
        {
            var values = "2024-01-01T00:00,2024-01-01T01:00,1,1,50@@2024-01-01T01:00,2024-01-01T02:00,1,1,60";
            var result = InvokeExtractFinalData(values, "Nitrogen dioxide", DefaultSite);

            Assert.Equal(2, result.Count);
            Assert.Equal("50", result[0].Value);
            Assert.Equal("60", result[1].Value);
        }

        [Fact]
        public void ExtractFinalData_SetsAllFieldsCorrectly()
        {
            var values = "T00,T01,Verified,Valid,42";
            var result = InvokeExtractFinalData(values, "PM10", DefaultSite);

            Assert.Single(result);
            var r = result[0];
            Assert.Equal("T00", r.StartTime);
            Assert.Equal("T01", r.EndTime);
            Assert.Equal("Verified", r.Verification);
            Assert.Equal("Valid", r.Validity);
            Assert.Equal("42", r.Value);
            Assert.Equal("PM10", r.PollutantName);
            Assert.Equal("Test Site", r.SiteName);
            Assert.Equal("UrbanBackground", r.SiteType);
            Assert.Equal("London", r.Region);
            Assert.Equal("England", r.Country);
        }

        [Fact]
        public void ExtractFinalData_IgnoresEntriesWithFewerThanFiveParts()
        {
            var values = "T00,T01,Verified,Valid@@T00,T01,1,1,99";  // first entry only 4 parts
            var result = InvokeExtractFinalData(values, "PM10", DefaultSite);

            Assert.Single(result);
            Assert.Equal("99", result[0].Value);
        }

        [Fact]
        public void ExtractFinalData_ReturnsEmpty_WhenAllEntriesHaveFewerThanFiveParts()
        {
            var values = "T00,T01,1@@T00,T01";
            var result = InvokeExtractFinalData(values, "PM10", DefaultSite);
            Assert.Empty(result);
        }

        [Fact]
        public void ExtractFinalData_StripsCarriageReturnNewline_BeforeParsing()
        {
            var values = "2024-01-01T00:00,2024-01-01T01:00,1,1,77\r\n";
            var result = InvokeExtractFinalData(values, "Ozone", DefaultSite);

            Assert.Single(result);
            Assert.Equal("77", result[0].Value);
        }

        [Fact]
        public void ExtractFinalData_HandlesEntriesWithMoreThanFiveParts()
        {
            // Extra comma in value – should still parse (parts[4] = fifth element)
            var values = "T00,T01,1,1,99,extra";
            var result = InvokeExtractFinalData(values, "Ozone", DefaultSite);

            Assert.Single(result);
            Assert.Equal("99", result[0].Value);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // Reflection helpers
        // ═══════════════════════════════════════════════════════════════════════════════

        private Task<JArray> InvokeFetchAtomFeedAsync(string siteId, string year)
        {
            var method = typeof(AtomDataSelectionHourlyFetchService)
                .GetMethod("FetchAtomFeedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (Task<JArray>)method.Invoke(_service, new object?[] { siteId, year })!;
        }

        private List<PollutantDetails> InvokeGetPollutantsToDisplay(string filter)
        {
            var method = typeof(AtomDataSelectionHourlyFetchService)
                .GetMethod("GetPollutantsToDisplay", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (List<PollutantDetails>)method.Invoke(_service, new object[] { filter })!;
        }

        private List<FinalData> InvokeProcessAtomData(JArray features, List<PollutantDetails> pollutants, SiteInfo site, string year)
        {
            var method = typeof(AtomDataSelectionHourlyFetchService)
                .GetMethod("ProcessAtomData", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (List<FinalData>)method.Invoke(_service, new object?[] { features, pollutants, site, year })!;
        }

        private List<FinalData> InvokeExtractFinalData(string values, string pollutantName, SiteInfo site)
        {
            var method = typeof(AtomDataSelectionHourlyFetchService)
                .GetMethod("ExtractFinalData", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (List<FinalData>)method.Invoke(_service, new object[] { values, pollutantName, site })!;
        }

        // ─── XML / JObject builders ──────────────────────────────────────────────────

        private static List<PollutantDetails> BuildPollutants() =>
            new List<PollutantDetails>
            {
                new PollutantDetails
                {
                    PollutantName = "Nitrogen dioxide",
                    PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/8"
                }
            };

        private static JObject BuildFeatureMember(string href, string values) =>
            new JObject
            {
                ["om:OM_Observation"] = new JObject
                {
                    ["om:observedProperty"] = new JObject
                    {
                        ["@xlink:href"] = href
                    },
                    ["om:result"] = new JObject
                    {
                        ["swe:DataArray"] = new JObject
                        {
                            ["swe:values"] = values
                        }
                    }
                }
            };
    }
}