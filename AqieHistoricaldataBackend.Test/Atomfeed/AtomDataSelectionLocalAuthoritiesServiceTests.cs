using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using System.Net;
using Xunit;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDataSelectionLocalAuthoritiesServiceTests
    {
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

        // British National Grid coordinates for a valid UK location (central London approx)
        private const int ValidEasting = 530000;
        private const int ValidNorthing = 180000;

        public AtomDataSelectionLocalAuthoritiesServiceTests()
        {
            _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        }

        private AtomDataSelectionLocalAuthoritiesService CreateService()
        {
            return new AtomDataSelectionLocalAuthoritiesService(
                _loggerMock.Object,
                _httpClientFactoryMock.Object);
        }

        private HttpClient CreateMockHttpClient(
            string allLocalAuthoritiesJson,
            string singleDtDataJson)
        {
            var handlerMock = new Mock<HttpMessageHandler>();

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri.PathAndQuery.Contains("getLocalAuthorities")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(allLocalAuthoritiesJson)
                });

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.RequestUri.PathAndQuery.Contains("getSingleDTDataByYear")),
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

        private static string BuildAllLocalAuthoritiesJson(int laId, string region)
        {
            var obj = new LocalAuthorityData
            {
                LA_ID = laId,
                LA_ONS_ID = "E09000001",
                Unique_ID = "UID001",
                X_OS_Grid_Reference = ValidEasting,
                Y_OS_Grid_Reference = ValidNorthing,
                LA_REGION = region
            };
            var root = new LocalAuthoritiesRoot { data = new List<LocalAuthorityData> { obj } };
            return JsonConvert.SerializeObject(root);
        }

        private static string BuildSingleDtDataJson(int laId, string region)
        {
            var obj = new LocalAuthorityData
            {
                LA_ID = laId,
                LA_ONS_ID = "E09000001",
                Unique_ID = "UID001",
                X_OS_Grid_Reference = ValidEasting,
                Y_OS_Grid_Reference = ValidNorthing,
                LA_REGION = region
            };
            var root = new LocalAuthoritiesRoot { data = new List<LocalAuthorityData> { obj } };
            return JsonConvert.SerializeObject(root);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Happy-path tests
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_SingleId_ReturnsSingleResult()
        {
            // Arrange
            var allJson = BuildAllLocalAuthoritiesJson(1, "London");
            var dtJson = BuildSingleDtDataJson(1, "London");
            var client = CreateMockHttpClient(allJson, dtJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            var result = await service.GetAtomDataSelectionLocalAuthoritiesService("1");

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(1, result[0].LA_ID);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_MultipleIds_ReturnsAllResults()
        {
            // Arrange
            var allJson = JsonConvert.SerializeObject(new
            {
                data = new[]
                {
                    new { LA_ID = 1, LA_ONS_ID = "E1", Unique_ID = "U1", X_OS_Grid_Reference = ValidEasting, Y_OS_Grid_Reference = ValidNorthing, Region = "North" },
                    new { LA_ID = 2, LA_ONS_ID = "E2", Unique_ID = "U2", X_OS_Grid_Reference = ValidEasting, Y_OS_Grid_Reference = ValidNorthing, Region = "South" }
                }
            });

            var dtJson1 = JsonConvert.SerializeObject(new
            {
                data = new[]
                {
                    new { LA_ID = 1, LA_ONS_ID = "E1", Unique_ID = "U1", X_OS_Grid_Reference = ValidEasting, Y_OS_Grid_Reference = ValidNorthing, Region = "North" }
                }
            });

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri.PathAndQuery.Contains("getLocalAuthorities")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(allJson)
                });

            // Both LA IDs return the same single-item response for simplicity
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri.PathAndQuery.Contains("getSingleDTDataByYear")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(dtJson1)
                });

            var client = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://www.laqmportal.co.uk/")
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            var result = await service.GetAtomDataSelectionLocalAuthoritiesService("1, 2");

            // Assert – two LA IDs => two iterations, each returning one item
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_CoordinatesConverted_ToLatLong()
        {
            // Arrange
            var allJson = BuildAllLocalAuthoritiesJson(1, "London");
            var dtJson = BuildSingleDtDataJson(1, "London");
            var client = CreateMockHttpClient(allJson, dtJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            var result = await service.GetAtomDataSelectionLocalAuthoritiesService("1");

            // Assert – coordinates must be in WGS84 range
            var item = result[0];
            Assert.True(item.Latitude is >= 49.0 and <= 61.0,
                $"Latitude {item.Latitude} out of expected UK range");
            Assert.True(item.Longitude is >= -8.0 and <= 2.0,
                $"Longitude {item.Longitude} out of expected UK range");
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_RegionIsSetFromLookup_WhenIdMatches()
        {
            // Arrange
            var allJson = BuildAllLocalAuthoritiesJson(42, "East Midlands");
            var dtJson = BuildSingleDtDataJson(42, "OldRegion");
            var client = CreateMockHttpClient(allJson, dtJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            var result = await service.GetAtomDataSelectionLocalAuthoritiesService("42");

            // Assert – region should be overwritten from the lookup dictionary
            Assert.Equal("East Midlands", result[0].LA_REGION);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_RegionNotOverwritten_WhenIdNotInLookup()
        {
            // Arrange – lookup contains LA_ID 99, but we query LA_ID 1
            var allJson = BuildAllLocalAuthoritiesJson(99, "South West");
            var dtJson = BuildSingleDtDataJson(1, "OriginalRegion");
            var client = CreateMockHttpClient(allJson, dtJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            var result = await service.GetAtomDataSelectionLocalAuthoritiesService("1");

            // Assert – region stays as whatever Newtonsoft desitized it to
            Assert.Equal("OriginalRegion", result[0].LA_REGION);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_TrimsWhitespace_InRegionIds()
        {
            // Arrange
            var allJson = BuildAllLocalAuthoritiesJson(5, "London");
            var dtJson = BuildSingleDtDataJson(5, "London");
            var client = CreateMockHttpClient(allJson, dtJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act – IDs intentionally have surrounding spaces
            var result = await service.GetAtomDataSelectionLocalAuthoritiesService("  5  ");

            // Assert
            Assert.Single(result);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Edge-case: empty / null data in responses
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_EmptyDataList_ReturnsEmptyResult()
        {
            // Arrange – both endpoints return empty data arrays
            var emptyJson = JsonConvert.SerializeObject(new { data = Array.Empty<object>() });
            var client = CreateMockHttpClient(emptyJson, emptyJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            var result = await service.GetAtomDataSelectionLocalAuthoritiesService("1");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_NullDataInSingleDt_SkipsIteration()
        {
            // Arrange – allLocalAuthorities has data, but getSingleDTDataByYear returns null data
            var allJson = BuildAllLocalAuthoritiesJson(1, "London");
            var nullDataJson = JsonConvert.SerializeObject(new { data = (object)null });
            var client = CreateMockHttpClient(allJson, nullDataJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            var result = await service.GetAtomDataSelectionLocalAuthoritiesService("1");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_NullRootInAllLocalAuthorities_UsesEmptyLookup()
        {
            // Arrange – getLocalAuthorities returns null-root JSON, single DT has data
            var nullRootJson = "null";
            var dtJson = BuildSingleDtDataJson(1, "SomeRegion");

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri.PathAndQuery.Contains("getLocalAuthorities")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(nullRootJson)
                });

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri.PathAndQuery.Contains("getSingleDTDataByYear")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(dtJson)
                });

            var client = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://www.laqmportal.co.uk/")
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            var result = await service.GetAtomDataSelectionLocalAuthoritiesService("1");

            // Assert – region NOT overwritten (lookup empty), but data is still returned
            Assert.Single(result);
            Assert.Equal("SomeRegion", result[0].LA_REGION);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_NullDataPropertyInAllLocalAuthorities_UsesEmptyLookup()
        {
            // Arrange – getLocalAuthorities root is non-null but .data is null
            var noDataJson = JsonConvert.SerializeObject(new { data = (object)null });
            var dtJson = BuildSingleDtDataJson(1, "SomeRegion");
            var client = CreateMockHttpClient(noDataJson, dtJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            var result = await service.GetAtomDataSelectionLocalAuthoritiesService("1");

            // Assert
            Assert.Single(result);
            Assert.Equal("SomeRegion", result[0].LA_REGION);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Edge-case: HTTP failures → exception re-thrown
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_HttpException_LogsErrorAndRethrows()
        {
            // Arrange
            _httpClientFactoryMock
                .Setup(f => f.CreateClient("laqmAPI"))
                .Throws(new HttpRequestException("Connection refused"));

            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                service.GetAtomDataSelectionLocalAuthoritiesService("1"));

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Error in GetAtomDataSelectionStationBoundryService")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_GetLocalAuthoritiesThrows_LogsAndRethrows()
        {
            // Arrange – factory succeeds but the HTTP call throws
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Timeout"));

            var client = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://www.laqmportal.co.uk/")
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                service.GetAtomDataSelectionLocalAuthoritiesService("1"));

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Error in GetAtomDataSelectionStationBoundryService")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Edge-case: multiple IDs including whitespace variants
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_MultipleIdsWithSpaces_ParsedCorrectly()
        {
            // Arrange
            var allJson = JsonConvert.SerializeObject(new
            {
                data = new[]
                {
                    new { LA_ID = 10, LA_ONS_ID = "E10", Unique_ID = "U10", X_OS_Grid_Reference = ValidEasting, Y_OS_Grid_Reference = ValidNorthing, Region = "North West" },
                    new { LA_ID = 20, LA_ONS_ID = "E20", Unique_ID = "U20", X_OS_Grid_Reference = ValidEasting, Y_OS_Grid_Reference = ValidNorthing, Region = "Yorkshire" }
                }
            });

            var dtJsonId10 = JsonConvert.SerializeObject(new
            {
                data = new[]
                {
                    new { LA_ID = 10, LA_ONS_ID = "E10", Unique_ID = "U10", X_OS_Grid_Reference = ValidEasting, Y_OS_Grid_Reference = ValidNorthing, Region = "North West" }
                }
            });

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri.PathAndQuery.Contains("getLocalAuthorities")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(allJson)
                });

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri.PathAndQuery.Contains("getSingleDTDataByYear")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(dtJsonId10)
                });

            var client = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://www.laqmportal.co.uk/")
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            var result = await service.GetAtomDataSelectionLocalAuthoritiesService(" 10 , 20 ");

            // Assert – two IDs → two calls → two items in final list
            Assert.Equal(2, result.Count);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Logging: info messages are written for each LA
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_LogsSelectedLaId()
        {
            // Arrange
            var allJson = BuildAllLocalAuthoritiesJson(7, "South East");
            var dtJson = BuildSingleDtDataJson(7, "South East");
            var client = CreateMockHttpClient(allJson, dtJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            await service.GetAtomDataSelectionLocalAuthoritiesService("7");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Selected From frontend API LA ID: 7")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAtomDataSelectionLocalAuthoritiesService_LogsLaqmApiLaId()
        {
            // Arrange
            var allJson = BuildAllLocalAuthoritiesJson(3, "Wales");
            var dtJson = BuildSingleDtDataJson(3, "Wales");
            var client = CreateMockHttpClient(allJson, dtJson);
            _httpClientFactoryMock.Setup(f => f.CreateClient("laqmAPI")).Returns(client);

            var service = CreateService();

            // Act
            await service.GetAtomDataSelectionLocalAuthoritiesService("3");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("From LAQM API LA ID: 3")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}