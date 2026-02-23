using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using Xunit;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionLocalAuthoritiesServiceTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

        private const int ValidEasting = 530000;
        private const int ValidNorthing = 180000;

        public AtomDataSelectionLocalAuthoritiesServiceTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        }

        private AtomDataSelectionLocalAuthoritiesService CreateService() =>
            new AtomDataSelectionLocalAuthoritiesService(
                _loggerMock.Object,
                _httpClientFactoryMock.Object);

        private HttpClient CreateMockHttpClient(
            string allLocalAuthoritiesJson,
            string singleDtDataJson)
        {
            var handlerMock = new Mock<HttpMessageHandler>();

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri!.PathAndQuery.Contains("getLocalAuthorities")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(allLocalAuthoritiesJson)
                });

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri!.PathAndQuery.Contains("getSingleDTDataByYear")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(singleDtDataJson)
                });

            return new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://www.laqmportal.co.uk/")
            };
        }

        /// <summary>
        /// Builds a JSON object that matches the spaced-key format returned by the real LAQM API,
        /// e.g. "LA ID", "X OS Grid Reference", "Region".
        /// </summary>
        private static string BuildLaItemJson(int laId, string laOnsId, string uniqueId, int easting, int northing, string region)
        {
            var obj = new JObject
            {
                ["Region"] = region,
                ["LA ID"] = laId,
                ["LA ONS ID"] = laOnsId,
                ["Unique ID"] = uniqueId,
                ["X OS Grid Reference"] = easting,
                ["Y OS Grid Reference"] = northing
            };
            return obj.ToString();
        }

        private static string BuildAllLocalAuthoritiesJson(int laId, string region) =>
            JsonConvert.SerializeObject(new
            {
                data = new[] { JObject.Parse(BuildLaItemJson(laId, "E09000001", "UID001", ValidEasting, ValidNorthing, region)) }
            });

        private static string BuildSingleDtDataJson(int laId, string region) =>
            JsonConvert.SerializeObject(new
            {
                data = new[] { JObject.Parse(BuildLaItemJson(laId, "E09000001", "UID001", ValidEasting, ValidNorthing, region)) }
            });

        private static JObject LaItem(int laId, string laOnsId, string uniqueId, string region) =>
            JObject.Parse(BuildLaItemJson(laId, laOnsId, uniqueId, ValidEasting, ValidNorthing, region));

        // ──────────────────────────────────────────────────────────────────────
        // Happy-path
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_SingleId_ReturnsSingleResult()
        {
            var client = CreateMockHttpClient(
                BuildAllLocalAuthoritiesJson(1, "London"),
                BuildSingleDtDataJson(1, "London"));
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("1");

            Assert.Single(result);
            Assert.Equal(1, result[0].LA_ID);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_MultipleIds_ReturnsAllResults()
        {
            var allJson = JsonConvert.SerializeObject(new
            {
                data = new[]
                {
                    LaItem(1, "E1", "U1", "North"),
                    LaItem(2, "E2", "U2", "South")
                }
            });

            var dtJson = BuildSingleDtDataJson(1, "North");
            var client = CreateMockHttpClient(allJson, dtJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("1, 2");

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_CoordinatesConverted_ToLatLong()
        {
            var client = CreateMockHttpClient(
                BuildAllLocalAuthoritiesJson(1, "London"),
                BuildSingleDtDataJson(1, "London"));
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("1");

            Assert.True(result[0].Latitude is >= 49.0 and <= 61.0,
                $"Latitude {result[0].Latitude} out of expected UK range");
            Assert.True(result[0].Longitude is >= -8.0 and <= 2.0,
                $"Longitude {result[0].Longitude} out of expected UK range");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Region lookup
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_RegionIsSetFromLookup_WhenIdMatches()
        {
            var client = CreateMockHttpClient(
                BuildAllLocalAuthoritiesJson(42, "East Midlands"),
                BuildSingleDtDataJson(42, "OldRegion"));
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("42");

            Assert.Equal("East Midlands", result[0].LA_REGION);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_RegionNotOverwritten_WhenIdNotInLookup()
        {
            // Lookup has LA_ID 99; querying LA_ID 1 – region should stay as deserialized
            var client = CreateMockHttpClient(
                BuildAllLocalAuthoritiesJson(99, "South West"),
                BuildSingleDtDataJson(1, "OriginalRegion"));
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("1");

            Assert.Equal("OriginalRegion", result[0].LA_REGION);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_DuplicateLaIdInLookup_UsesLastEntry()
        {
            // Duplicate LA_IDs in allLocalAuthorities — GroupBy(...).Last() must win
            var duplicateAllJson = JsonConvert.SerializeObject(new
            {
                data = new[]
                {
                    LaItem(5, "E5a", "U5a", "First Region"),
                    LaItem(5, "E5b", "U5b", "Last Region")
                }
            });

            var client = CreateMockHttpClient(duplicateAllJson, BuildSingleDtDataJson(5, "OldRegion"));
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("5");

            Assert.Equal("Last Region", result[0].LA_REGION);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Whitespace / input parsing
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_TrimsWhitespace_InRegionIds()
        {
            var client = CreateMockHttpClient(
                BuildAllLocalAuthoritiesJson(5, "London"),
                BuildSingleDtDataJson(5, "London"));
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("  5  ");

            Assert.Single(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_MultipleIdsWithSpaces_ParsedCorrectly()
        {
            var allJson = JsonConvert.SerializeObject(new
            {
                data = new[]
                {
                    LaItem(10, "E10", "U10", "North West"),
                    LaItem(20, "E20", "U20", "Yorkshire")
                }
            });

            var client = CreateMockHttpClient(allJson, BuildSingleDtDataJson(10, "North West"));
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService(" 10 , 20 ");

            Assert.Equal(2, result.Count);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Null / empty response bodies
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_EmptyDataList_ReturnsEmptyResult()
        {
            var emptyJson = JsonConvert.SerializeObject(new { data = Array.Empty<object>() });
            var client = CreateMockHttpClient(emptyJson, emptyJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("1");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_NullDataInSingleDt_SkipsIteration()
        {
            var nullDataJson = JsonConvert.SerializeObject(new { data = (object?)null });
            var client = CreateMockHttpClient(
                BuildAllLocalAuthoritiesJson(1, "London"),
                nullDataJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("1");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_NullRootInAllLocalAuthorities_UsesEmptyLookup()
        {
            // getLocalAuthorities returns JSON "null" → deserialized root is null → ?? empty dict
            var handlerMock = new Mock<HttpMessageHandler>();

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri!.PathAndQuery.Contains("getLocalAuthorities")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null")
                });

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri!.PathAndQuery.Contains("getSingleDTDataByYear")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildSingleDtDataJson(1, "SomeRegion"))
                });

            var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://www.laqmportal.co.uk/") };
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("1");

            Assert.Single(result);
            Assert.Equal("SomeRegion", result[0].LA_REGION);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_NullDataPropertyInAllLocalAuthorities_UsesEmptyLookup()
        {
            // Root is non-null but .data == null → ?. produces null → ?? empty dict
            var noDataJson = JsonConvert.SerializeObject(new { data = (object?)null });
            var client = CreateMockHttpClient(noDataJson, BuildSingleDtDataJson(1, "SomeRegion"));
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("1");

            Assert.Single(result);
            Assert.Equal("SomeRegion", result[0].LA_REGION);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_NullRootInSingleDt_SkipsIteration()
        {
            // getSingleDTDataByYear returns JSON "null" → deserialized root is null → data check fails → skip
            var handlerMock = new Mock<HttpMessageHandler>();

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri!.PathAndQuery.Contains("getLocalAuthorities")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildAllLocalAuthoritiesJson(1, "London"))
                });

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri!.PathAndQuery.Contains("getSingleDTDataByYear")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null")
                });

            var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://www.laqmportal.co.uk/") };
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var result = await CreateService().GetAtomDataSelectionLocalAuthoritiesService("1");

            Assert.Empty(result);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Exception / error paths
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_HttpClientFactoryThrows_LogsErrorAndRethrows()
        {
            _httpClientFactoryMock
                .Setup(f => f.CreateClient("laqmAPI"))
                .Throws(new HttpRequestException("Connection refused"));

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                CreateService().GetAtomDataSelectionLocalAuthoritiesService("1"));

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) =>
                        v.ToString()!.Contains("Error in GetAtomDataSelectionStationBoundryService")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_GetLocalAuthoritiesHttpCallThrows_LogsAndRethrows()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Timeout"));

            var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://www.laqmportal.co.uk/") };
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                CreateService().GetAtomDataSelectionLocalAuthoritiesService("1"));

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) =>
                        v.ToString()!.Contains("Error in GetAtomDataSelectionStationBoundryService")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_GetSingleDtHttpCallThrows_LogsAndRethrows()
        {
            // getLocalAuthorities succeeds; getSingleDTDataByYear throws
            var handlerMock = new Mock<HttpMessageHandler>();

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri!.PathAndQuery.Contains("getLocalAuthorities")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(BuildAllLocalAuthoritiesJson(1, "London"))
                });

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri!.PathAndQuery.Contains("getSingleDTDataByYear")),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Bad Gateway"));

            var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://www.laqmportal.co.uk/") };
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                CreateService().GetAtomDataSelectionLocalAuthoritiesService("1"));

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) =>
                        v.ToString()!.Contains("Error in GetAtomDataSelectionStationBoundryService")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Logging assertions
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_LogsSelectedLaId()
        {
            var client = CreateMockHttpClient(
                BuildAllLocalAuthoritiesJson(7, "South East"),
                BuildSingleDtDataJson(7, "South East"));
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            await CreateService().GetAtomDataSelectionLocalAuthoritiesService("7");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) =>
                        v.ToString()!.Contains("Selected From frontend API LA ID: 7")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_LogsLaqmApiLaId()
        {
            var client = CreateMockHttpClient(
                BuildAllLocalAuthoritiesJson(3, "Wales"),
                BuildSingleDtDataJson(3, "Wales"));
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            await CreateService().GetAtomDataSelectionLocalAuthoritiesService("3");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) =>
                        v.ToString()!.Contains("From LAQM API LA ID: 3")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_MultipleItems_LogsEachLaId()
        {
            // Two items in the getSingleDT response → two info log entries for "From LAQM API"
            var allJson = BuildAllLocalAuthoritiesJson(1, "London");

            var dtJson = JsonConvert.SerializeObject(new
            {
                data = new[]
                {
                    LaItem(1, "E1", "U1", "London"),
                    LaItem(2, "E2", "U2", "London")
                }
            });

            var client = CreateMockHttpClient(allJson, dtJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            await CreateService().GetAtomDataSelectionLocalAuthoritiesService("1");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("From LAQM API LA ID:")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2));
        }

        // ──────────────────────────────────────────────────────────────────────
        // Model class coverage
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void LocalAuthorityData_DefaultCoordinates_AreZero()
        {
            var la = new LocalAuthorityData();

            Assert.Equal(0.0, la.Latitude);
            Assert.Equal(0.0, la.Longitude);
        }

        [Fact]
        public void LocalAuthorityData_Properties_SetAndGet()
        {
            var la = new LocalAuthorityData
            {
                LA_ID = 99,
                LA_ONS_ID = "E99",
                Unique_ID = "UIDX",
                X_OS_Grid_Reference = ValidEasting,
                Y_OS_Grid_Reference = ValidNorthing,
                LA_REGION = "Test Region",
                Latitude = 51.5,
                Longitude = -0.1
            };

            Assert.Equal(99, la.LA_ID);
            Assert.Equal("E99", la.LA_ONS_ID);
            Assert.Equal("UIDX", la.Unique_ID);
            Assert.Equal(ValidEasting, la.X_OS_Grid_Reference);
            Assert.Equal(ValidNorthing, la.Y_OS_Grid_Reference);
            Assert.Equal("Test Region", la.LA_REGION);
            Assert.Equal(51.5, la.Latitude);
            Assert.Equal(-0.1, la.Longitude);
        }

        [Fact]
        public void LocalAuthoritiesRoot_Properties_SetAndGet()
        {
            var root = new LocalAuthoritiesRoot
            {
                data = new List<LocalAuthorityData>
                {
                    new LocalAuthorityData { LA_ID = 1 }
                }
            };

            Assert.Single(root.data);
            Assert.Equal(1, root.data[0].LA_ID);
        }

        [Fact]
        public void LocalAuthority_Properties_SetAndGet()
        {
            var la = new LocalAuthority { Id = "1", Name = "Test Authority" };

            Assert.Equal("1", la.Id);
            Assert.Equal("Test Authority", la.Name);
        }

        [Fact]
        public void Root_Properties_SetAndGet()
        {
            var root = new Root
            {
                filters = new Filters { search_terms = "PM10", search_year = 2023, items_per_page = "100" },
                pagination = new Pagination { totalRows = 50, totalPages = 1, pagenum = "1" },
                data = new List<DataItem> { new DataItem { LA_ID = 1, Unique_ID = "U1", X_OS_Grid_Reference = 100, Y_OS_Grid_Reference = 200 } },
                region = "London",
                info = new List<Info> { new Info { result = 1, result_code = 200 } }
            };

            Assert.Equal("PM10", root.filters.search_terms);
            Assert.Equal(2023, root.filters.search_year);
            Assert.Equal("100", root.filters.items_per_page);
            Assert.Equal(50, root.pagination.totalRows);
            Assert.Equal(1, root.pagination.totalPages);
            Assert.Equal("1", root.pagination.pagenum);
            Assert.Single(root.data);
            Assert.Equal(1, root.data[0].LA_ID);
            Assert.Equal("U1", root.data[0].Unique_ID);
            Assert.Equal(100, root.data[0].X_OS_Grid_Reference);
            Assert.Equal(200, root.data[0].Y_OS_Grid_Reference);
            Assert.Equal("London", root.region);
            Assert.Equal(1, root.info[0].result);
            Assert.Equal(200, root.info[0].result_code);
        }
    }
}