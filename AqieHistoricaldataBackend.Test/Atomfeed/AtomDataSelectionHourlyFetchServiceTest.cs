using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using AqieHistoricaldataBackend.Atomfeed.Services;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionHourlyFetchServiceTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly AtomDataSelectionHourlyFetchService _service;

        // ── XML fixtures ─────────────────────────────────────────────────────────────

        /// <summary>Two featureMembers: index 0 skipped by ProcessAtomData (loop starts at i=1),
        /// index 1 contains a valid NO2 observation with two @@-delimited data rows.</summary>
        private const string ValidXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\"" +
            "                       xmlns:om=\"http://www.opengis.net/om/2.0\"" +
            "                       xmlns:swe=\"http://www.opengis.net/swe/2.0\"" +
            "                       xmlns:xlink=\"http://www.w3.org/1999/xlink\">" +
            "  <gml:featureMember><om:OM_Observation/></gml:featureMember>" +
            "  <gml:featureMember>" +
            "    <om:OM_Observation>" +
            "      <om:observedProperty xlink:href=\"http://dd.eionet.europa.eu/vocabulary/aq/pollutant/8\"/>" +
            "      <om:result><swe:DataArray>" +
            "        <swe:values>2024-01-01T00:00,2024-01-01T01:00,1,1,50@@2024-01-01T01:00,2024-01-01T02:00,1,1,60</swe:values>" +
            "      </swe:DataArray></om:result>" +
            "    </om:OM_Observation>" +
            "  </gml:featureMember>" +
            "</gml:FeatureCollection>";

        /// <summary>Index 1 has no observedProperty href → covers the IsNullOrEmpty(href) continue branch.
        /// Index 2 has a pollutant URL that matches no known pollutant → covers the match == null branch.</summary>
        private const string XmlNullHrefAndNoMatch =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\"" +
            "                       xmlns:om=\"http://www.opengis.net/om/2.0\"" +
            "                       xmlns:swe=\"http://www.opengis.net/swe/2.0\"" +
            "                       xmlns:xlink=\"http://www.w3.org/1999/xlink\">" +
            "  <gml:featureMember><om:OM_Observation/></gml:featureMember>" +
            "  <gml:featureMember><om:OM_Observation/></gml:featureMember>" +
            "  <gml:featureMember>" +
            "    <om:OM_Observation>" +
            "      <om:observedProperty xlink:href=\"http://dd.eionet.europa.eu/vocabulary/aq/pollutant/999\"/>" +
            "      <om:result><swe:DataArray><swe:values>a,b,c,d,e</swe:values></swe:DataArray></om:result>" +
            "    </om:OM_Observation>" +
            "  </gml:featureMember>" +
            "</gml:FeatureCollection>";

        /// <summary>Index 1 has a matching NO2 pollutant URL but an empty swe:values element
        /// → covers the IsNullOrEmpty(values) branch in ProcessAtomData.</summary>
        private const string XmlEmptyValues =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\"" +
            "                       xmlns:om=\"http://www.opengis.net/om/2.0\"" +
            "                       xmlns:swe=\"http://www.opengis.net/swe/2.0\"" +
            "                       xmlns:xlink=\"http://www.w3.org/1999/xlink\">" +
            "  <gml:featureMember><om:OM_Observation/></gml:featureMember>" +
            "  <gml:featureMember>" +
            "    <om:OM_Observation>" +
            "      <om:observedProperty xlink:href=\"http://dd.eionet.europa.eu/vocabulary/aq/pollutant/8\"/>" +
            "      <om:result><swe:DataArray><swe:values></swe:values></swe:DataArray></om:result>" +
            "    </om:OM_Observation>" +
            "  </gml:featureMember>" +
            "</gml:FeatureCollection>";

        /// <summary>Index 1 has a valid NO2 observation where one row has &lt;5 parts and one has ≥5 parts
        /// → covers both sides of the Where(parts.Length >= 5) filter in ExtractFinalData.</summary>
        private const string XmlInsufficientParts =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\"" +
            "                       xmlns:om=\"http://www.opengis.net/om/2.0\"" +
            "                       xmlns:swe=\"http://www.opengis.net/swe/2.0\"" +
            "                       xmlns:xlink=\"http://www.w3.org/1999/xlink\">" +
            "  <gml:featureMember><om:OM_Observation/></gml:featureMember>" +
            "  <gml:featureMember>" +
            "    <om:OM_Observation>" +
            "      <om:observedProperty xlink:href=\"http://dd.eionet.europa.eu/vocabulary/aq/pollutant/8\"/>" +
            "      <om:result><swe:DataArray>" +
            "        <swe:values>a,b,c@@2024-01-01T00:00,2024-01-01T01:00,1,1,50</swe:values>" +
            "      </swe:DataArray></om:result>" +
            "    </om:OM_Observation>" +
            "  </gml:featureMember>" +
            "</gml:FeatureCollection>";

        /// <summary>FeatureCollection with no gml:featureMember element
        /// → covers the featureCollection?["gml:featureMember"] as JArray ?? new JArray() null-coalescing branch.</summary>
        private const string XmlNoFeatureMembers =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\"/>";

        // ── Shared test data ──────────────────────────────────────────────────────────

        private static readonly SiteInfo DefaultSite = new()
        {
            SiteName = "Test Site",
            LocalSiteId = "AH",
            AreaType = "Urban",
            SiteType = "Background",
            ZoneRegion = "London",
            Country = "England"
        };

        private static QueryStringData AurnData => new() { dataSource = "AURN" };
        private static QueryStringData NonAurnData => new() { dataSource = "NonAURN" };

        // ── Constructor ───────────────────────────────────────────────────────────────

        public AtomDataSelectionHourlyFetchServiceTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _service = new AtomDataSelectionHourlyFetchService(
                _loggerMock.Object, _httpClientFactoryMock.Object);
        }

        // ── Test helpers ──────────────────────────────────────────────────────────────

        private void SetupHttpClient(HttpResponseMessage response)
        {
            // Capture the XML once so we can re-create a fresh HttpResponseMessage for
            // every SendAsync call. A single HttpResponseMessage can only have its content
            // stream read ONCE; reusing it across parallel calls causes subsequent reads
            // to return empty content, making multi-site / multi-year tests fail.
            var statusCode = response.StatusCode;
            string? xmlBody = null;
            if (response.Content is StringContent sc)
                xmlBody = sc.ReadAsStringAsync().GetAwaiter().GetResult();

            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((_, _) =>
                {
                    var freshResponse = new HttpResponseMessage(statusCode);
                    if (xmlBody is not null)
                        freshResponse.Content = new StringContent(
                            xmlBody, Encoding.UTF8, "application/xml");
                    return Task.FromResult(freshResponse);
                });

            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://example.com/") };
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);
        }

        private void SetupHttpClientCapturingUrl(HttpResponseMessage response, out List<string> capturedUrls)
        {
            var statusCode = response.StatusCode;
            var urls = new List<string>();
            capturedUrls = urls;

            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
                {
                    urls.Add(req.RequestUri?.ToString() ?? "");
                    return Task.FromResult(new HttpResponseMessage(statusCode));
                });

            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://example.com/") };
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);
        }

        private void SetupHttpClientThrows(Exception ex)
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(ex);

            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://example.com/") };
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);
        }

        /// <summary>Invokes the private FetchAtomFeedAsync via reflection.</summary>
        private Task<JArray> InvokeFetchAtomFeedAsync(
            string? siteId, string year, string? dataSource = "AURN")
        {
            var method = typeof(AtomDataSelectionHourlyFetchService)
                .GetMethod("FetchAtomFeedAsync",
                    BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (Task<JArray>)method.Invoke(_service, [siteId, year, dataSource])!;
        }

        /// <summary>Invokes the private ProcessAtomData via reflection.</summary>
        private List<FinalData> InvokeProcessAtomData(
            JArray features, List<PollutantDetails> pollutants, SiteInfo site)
        {
            var method = typeof(AtomDataSelectionHourlyFetchService)
                .GetMethod("ProcessAtomData",
                    BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (List<FinalData>)method.Invoke(_service, [features, pollutants, site])!;
        }

        private static List<PollutantDetails> No2Pollutants() =>
        [
            new PollutantDetails
            {
                PollutantName = "Nitrogen dioxide",
                PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/8"
            }
        ];

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetAtomDataSelectionHourlyFetchService – happy path
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAtomData_ReturnsData_WhenAurnHttpSucceeds()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXml, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            result.Should().NotBeEmpty();
            result.Should().AllSatisfy(r => r.PollutantName.Should().Be("Nitrogen dioxide"));
        }

        [Fact]
        public async Task GetAtomData_ReturnsData_WhenNonAurnHttpSucceeds()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXml, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", NonAurnData);

            result.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetAtomData_UsesAutoUrl_WhenDataSourceIsAURN()
        {
            SetupHttpClientCapturingUrl(
                new HttpResponseMessage(HttpStatusCode.NotFound), out var urls);

            await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            urls.Should().ContainMatch("*auto/GB_Fixed*");
            urls.Should().NotContainMatch("*non-auto*");
        }

        [Fact]
        public async Task GetAtomData_UsesNonAutoUrl_WhenDataSourceIsNotAURN()
        {
            SetupHttpClientCapturingUrl(
                new HttpResponseMessage(HttpStatusCode.NotFound), out var urls);

            await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", NonAurnData);

            urls.Should().ContainMatch("*non-auto*");
        }

        [Fact]
        public async Task GetAtomData_AggregatesData_ForMultipleYears()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXml, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2023,2024", AurnData);

            // 2 years × 2 rows each = 4 results
            result.Should().HaveCount(4);
        }

        [Fact]
        public async Task GetAtomData_AggregatesData_ForMultipleSites()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXml, Encoding.UTF8, "application/xml")
            });

            var siteB = new SiteInfo
            {
                SiteName = "Site B", LocalSiteId = "BX",
                AreaType = "Rural", SiteType = "Traffic",
                ZoneRegion = "Yorkshire", Country = "England"
            };

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite, siteB], "Nitrogen dioxide", "2024", AurnData);

            result.Should().HaveCount(4); // 2 sites × 2 rows each
        }

        [Fact]
        public async Task GetAtomData_PopulatesSiteFields_FromSiteInfo()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXml, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            var first = result.First();
            first.SiteName.Should().Be("Test Site");
            first.SiteType.Should().Be("UrbanBackground");
            first.Region.Should().Be("London");
            first.Country.Should().Be("England");
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetAtomDataSelectionHourlyFetchService – empty / edge inputs
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAtomData_ReturnsEmpty_WhenStationListIsEmpty()
        {
            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [], "Nitrogen dioxide", "2024", AurnData);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAtomData_ReturnsEmpty_WhenLocalSiteIdIsNull()
        {
            // Covers the IsNullOrWhiteSpace(siteID) guard in FetchAtomFeedAsync
            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [new SiteInfo { LocalSiteId = null }], "Nitrogen dioxide", "2024", AurnData);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAtomData_ReturnsEmpty_WhenYearIsWhitespace()
        {
            // " ".Split(',') → [" "] which is whitespace → IsNullOrWhiteSpace guard in FetchAtomFeedAsync
            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", " ", AurnData);

            result.Should().BeEmpty();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetAtomDataSelectionHourlyFetchService – outer catch block
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAtomData_OuterCatch_ReturnsEmpty_WhenFilteryearIsNull()
        {
            // null.Split(',') throws NullReferenceException before the parallel loop,
            // falling through to the outer catch block.
            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", null!, AurnData);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAtomData_OuterCatch_LogsError_WhenFilteryearIsNull()
        {
            await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", null!, AurnData);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("Error in GetAtomDataSelectionHourlyFetchService")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetAtomDataSelectionHourlyFetchService – HTTP error branches (inner catch)
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAtomData_ReturnsEmpty_When404Returned()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.NotFound));

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAtomData_ReturnsEmpty_WhenNonSuccessStatusReturned()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("error")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAtomData_ReturnsEmpty_WhenHttpRequestExceptionThrown()
        {
            SetupHttpClientThrows(new HttpRequestException("Network error"));

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAtomData_ReturnsEmpty_WhenGeneralExceptionThrown()
        {
            SetupHttpClientThrows(new InvalidOperationException("Unexpected"));

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAtomData_LogsError_WhenSiteProcessingFails()
        {
            // The exception is swallowed inside FetchAtomFeedAsync's own catch block,
            // which logs "Error FetchAtomFeedAsync fetching Atom feed...".
            // The Parallel.ForEachAsync "Error processing site" path is unreachable from
            // the public API because FetchAtomFeedAsync never lets exceptions escape.
            SetupHttpClientThrows(new Exception("Processing failure"));

            await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("Error FetchAtomFeedAsync")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetAtomData_ReturnsEmpty_WhenFactoryThrows()
        {
            _httpClientFactoryMock
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Throws(new Exception("Factory exploded"));

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            result.Should().BeEmpty();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetAtomDataSelectionHourlyFetchService – pollutant filter combinations
        // ═══════════════════════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("Nitrogen dioxide")]
        [InlineData("Particulate matter")]
        [InlineData("Fine particulate matter")]
        [InlineData("Ozone")]
        [InlineData("Sulphur dioxide")]
        [InlineData("Nitrogen oxides as nitrogen dioxide")]
        [InlineData("Carbon monoxide")]
        [InlineData("Nitric oxide")]
        public async Task GetAtomData_HandlesAllKnownPollutants_WithoutThrowing(string pollutant)
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXml, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], pollutant, "2024", AurnData);

            result.Should().NotBeNull();
        }

        [Fact]
        public async Task GetAtomData_ReturnsAllPollutants_WhenFilterDoesNotMatch()
        {
            // "UnknownPollutant" matches nothing → GetPollutantsToDisplay returns all 8
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXml, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "UnknownPollutant", "2024", AurnData);

            // NO2 URL still matches one of the 8 default pollutants
            result.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetAtomData_HandlesNullPollutantFilter()
        {
            // null filter → Split produces [""] → no match → returns all pollutants
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXml, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], null!, "2024", AurnData);

            result.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetAtomData_HandlesMultiplePollutantsCommaSeparated()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ValidXml, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide,Ozone", "2024", AurnData);

            result.Should().NotBeEmpty();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // GetAtomDataSelectionHourlyFetchService – XML content branches
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task GetAtomData_ReturnsEmpty_WhenFeatureMemberIsMissing()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(XmlNoFeatureMembers, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAtomData_ReturnsEmpty_WhenPollutantUrlDoesNotMatch()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(XmlNullHrefAndNoMatch, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAtomData_ReturnsEmpty_WhenValuesElementIsEmpty()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(XmlEmptyValues, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAtomData_FiltersOutRows_WithFewerThanFiveParts()
        {
            // "a,b,c" has 3 parts → filtered; valid row has 5 parts → kept
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(XmlInsufficientParts, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomDataSelectionHourlyFetchService(
                [DefaultSite], "Nitrogen dioxide", "2024", AurnData);

            result.Should().HaveCount(1);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // FetchAtomFeedAsync – null / empty guard (via reflection)
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmpty_WhenSiteIdIsNull()
        {
            var result = await InvokeFetchAtomFeedAsync(null, "2024");
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmpty_WhenSiteIdIsEmpty()
        {
            var result = await InvokeFetchAtomFeedAsync("", "2024");
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmpty_WhenSiteIdIsWhitespace()
        {
            var result = await InvokeFetchAtomFeedAsync("   ", "2024");
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmpty_WhenYearIsEmpty()
        {
            var result = await InvokeFetchAtomFeedAsync("AH", "");
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsWarning_WhenSiteIdIsNullOrYearIsEmpty()
        {
            await InvokeFetchAtomFeedAsync(null, "2024");

            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("Invalid FetchAtomFeedAsync")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // FetchAtomFeedAsync – HTTP status branches (via reflection)
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmpty_On404Response()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.NotFound));
            var result = await InvokeFetchAtomFeedAsync("AH", "2024");
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsWarning_On404Response()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.NotFound));
            await InvokeFetchAtomFeedAsync("AH", "2024");

            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("not found (404)")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmpty_OnNonSuccessResponse()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server error")
            });
            var result = await InvokeFetchAtomFeedAsync("AH", "2024");
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsWarning_OnNonSuccessResponse()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server error")
            });
            await InvokeFetchAtomFeedAsync("AH", "2024");

            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmpty_On428Response()
        {
            SetupHttpClient(new HttpResponseMessage((HttpStatusCode)428)
            {
                Content = new StringContent("precondition required")
            });
            var result = await InvokeFetchAtomFeedAsync("AH", "2024");
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsError_On428Response()
        {
            SetupHttpClient(new HttpResponseMessage((HttpStatusCode)428)
            {
                Content = new StringContent("precondition required")
            });
            await InvokeFetchAtomFeedAsync("AH", "2024");

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("428")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // FetchAtomFeedAsync – exception catch branches (via reflection)
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmpty_OnHttpRequestException_With404StatusCode()
        {
            // Covers catch (HttpRequestException ex) when (ex.StatusCode == NotFound)
            SetupHttpClientThrows(
                new HttpRequestException("not found", null, HttpStatusCode.NotFound));
            var result = await InvokeFetchAtomFeedAsync("AH", "2024");
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsWarning_OnHttpRequestException_With404StatusCode()
        {
            SetupHttpClientThrows(
                new HttpRequestException("not found", null, HttpStatusCode.NotFound));
            await InvokeFetchAtomFeedAsync("AH", "2024");

            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("not found (404)")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmpty_OnHttpRequestException_NonNotFound()
        {
            // Covers catch (HttpRequestException ex) – generic branch
            SetupHttpClientThrows(new HttpRequestException("Network failure"));
            var result = await InvokeFetchAtomFeedAsync("AH", "2024");
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsError_OnHttpRequestException_NonNotFound()
        {
            SetupHttpClientThrows(new HttpRequestException("Network failure"));
            await InvokeFetchAtomFeedAsync("AH", "2024");

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("HTTP error FetchAtomFeedAsync")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmpty_OnGeneralException()
        {
            SetupHttpClientThrows(new InvalidOperationException("Unexpected"));
            var result = await InvokeFetchAtomFeedAsync("AH", "2024");
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task FetchAtomFeedAsync_LogsError_OnGeneralException()
        {
            SetupHttpClientThrows(new InvalidOperationException("Unexpected"));
            await InvokeFetchAtomFeedAsync("AH", "2024");

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("Error FetchAtomFeedAsync")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_WhenFeatureMemberIsAbsent()
        {
            // Covers featureCollection?["gml:featureMember"] as JArray ?? new JArray()
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(XmlNoFeatureMembers, Encoding.UTF8, "application/xml")
            });
            var result = await InvokeFetchAtomFeedAsync("AH", "2024");
            result.Should().BeEmpty();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // ProcessAtomData – via reflection (exception branch inside the for-loop)
        // ═══════════════════════════════════════════════════════════════════════════════

        [Fact]
        public void ProcessAtomData_ReturnsEmpty_WhenFeaturesIsEmpty()
        {
            var result = InvokeProcessAtomData(new JArray(), No2Pollutants(), DefaultSite);
            result.Should().BeEmpty();
        }

        [Fact]
        public void ProcessAtomData_LogsError_WhenFeatureCausesException()
        {
            // A JValue at index 1 (not a JObject) causes a property access exception
            // inside the for-loop, triggering the catch block that calls LogError.
            var badFeatures = new JArray
            {
                new JObject(), // index 0 – skipped
                new JValue("bad-token") // index 1 – accessing ["om:OM_Observation"] throws
            };

            InvokeProcessAtomData(badFeatures, No2Pollutants(), DefaultSite);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("Error processing ProcessAtomData feature member")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}