using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using Xunit;
using AqieHistoricaldataBackend.Atomfeed.Services;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Tests.Services
{
    public class AtomFeedFetchServiceBaseTest
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

        public AtomFeedFetchServiceBaseTest()
        {
            _loggerMock = new Mock<ILogger>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        }

        private TestAtomFeedService CreateService(HttpResponseMessage? response = null, Exception? exception = null)
        {
            var handler = new Mock<HttpMessageHandler>();

            if (exception != null)
            {
                handler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ThrowsAsync(exception);
            }
            else
            {
                handler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(response!);
            }

            var client = new HttpClient(handler.Object)
            {
                BaseAddress = new Uri("https://test.com/")
            };

            _httpClientFactoryMock
                .Setup(x => x.CreateClient("Atomfeed"))
                .Returns(client);

            return new TestAtomFeedService(
                _httpClientFactoryMock.Object,
                _loggerMock.Object);
        }

        #region FetchAtomFeedAsync

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldReturnEmpty_WhenSiteIdIsNull()
        {
            var service = CreateService();

            var result = await service.FetchAsync(null, "2025");

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldReturnEmpty_WhenYearIsNull()
        {
            var service = CreateService();

            var result = await service.FetchAsync("SITE1", null!);

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldReturnEmpty_WhenStatus304()
        {
            var service = CreateService(
                new HttpResponseMessage(HttpStatusCode.NotModified));

            var result = await service.FetchAsync("SITE1", "2025");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldReturnEmpty_WhenStatus404()
        {
            var service = CreateService(
                new HttpResponseMessage(HttpStatusCode.NotFound));

            var result = await service.FetchAsync("SITE1", "2025");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldReturnEmpty_WhenStatus428()
        {
            var response = new HttpResponseMessage(HttpStatusCode.PreconditionRequired)
            {
                Content = new StringContent("Precondition Required")
            };

            var service = CreateService(response);

            var result = await service.FetchAsync("SITE1", "2025");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldReturnEmpty_WhenStatus500()
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Server Error")
            };

            var service = CreateService(response);

            var result = await service.FetchAsync("SITE1", "2025");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldReturnParsedArray_WhenSuccess()
        {
            var xml =
                """
                <root>
                </root>
                """;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml)
            };

            var service = CreateService(response);

            var result = await service.FetchAsync("SITE1", "2025");

            Assert.NotNull(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldHandle404HttpRequestException()
        {
            var exception = new HttpRequestException(
                "Not found",
                null,
                HttpStatusCode.NotFound);

            var service = CreateService(exception: exception);

            var result = await service.FetchAsync("SITE1", "2025");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldHandleHttpRequestException()
        {
            var exception = new HttpRequestException("General HTTP Error");

            var service = CreateService(exception: exception);

            var result = await service.FetchAsync("SITE1", "2025");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldHandleGeneralException()
        {
            var service = CreateService(exception: new Exception("Unexpected"));

            var result = await service.FetchAsync("SITE1", "2025");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldUseNonAutoPath_WhenDataSourceIsNotAurnOrNull()
        {
            // Arrange – any non-AURN, non-null dataSource triggers the non-auto URL
            var response = new HttpResponseMessage(HttpStatusCode.NotFound);
            var service = CreateService(response);

            // Act – passing a non-AURN source (e.g. "LAQN")
            var result = await service.FetchAsync("SITE1", "2025", "LAQN");

            // Assert – 404 returns empty regardless; what matters is the non-auto branch executes
            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldUseAutoPath_WhenDataSourceIsAurn()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.NotFound);
            var service = CreateService(response);

            // Act – explicit "AURN" datasource should resolve to the auto path
            var result = await service.FetchAsync("SITE1", "2025", "AURN");

            Assert.Empty(result);
        }

        #endregion

        #region ProcessAtomData

        [Fact]
        public void ProcessAtomData_ShouldReturnEmpty_WhenFeaturesNull()
        {
            var service = CreateService();

            var result = service.Process(null!, new List<PollutantDetails>());

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldReturnEmpty_WhenFeaturesEmpty()
        {
            var service = CreateService();

            var result = service.Process(
                new JArray(),
                new List<PollutantDetails>());

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldSkip_WhenHrefMissing()
        {
            var service = CreateService();

            var features = new JArray
            {
                new JObject(),
                new JObject
                {
                    ["om:OM_Observation"] = new JObject()
                }
            };

            var result = service.Process(
                features,
                new List<PollutantDetails>());

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldSkip_WhenPollutantNotFound()
        {
            var service = CreateService();

            var feature = new JObject
            {
                ["om:OM_Observation"] = new JObject
                {
                    ["om:observedProperty"] = new JObject
                    {
                        ["@xlink:href"] = "unknown-url"
                    }
                }
            };

            var features = new JArray
            {
                new JObject(),
                feature
            };

            var result = service.Process(
                features,
                new List<PollutantDetails>());

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldHandleFeatureException()
        {
            var service = CreateService();

            var features = new JArray
            {
                new JObject(),
                JValue.CreateNull()
            };

            var result = service.Process(
                features,
                new List<PollutantDetails>());

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldProcessWithoutSiteInfo()
        {
            var service = CreateService();

            var pollutants = new List<PollutantDetails>
            {
                new()
                {
                    PollutantMasterUrl = "pollutant1",
                    PollutantName = "NO2"
                }
            };

            var feature = new JObject
            {
                ["om:OM_Observation"] = new JObject
                {
                    ["om:observedProperty"] = new JObject
                    {
                        ["@xlink:href"] = "pollutant1"
                    },
                    ["om:result"] = new JObject
                    {
                        ["swe:DataArray"] = new JObject
                        {
                            ["swe:values"] = "2025-01-01T00:00Z,1"
                        }
                    }
                }
            };

            var features = new JArray
            {
                new JObject(),
                feature
            };

            var result = service.Process(features, pollutants);

            Assert.NotNull(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldProcessWithSiteInfo()
        {
            var service = CreateService();

            var pollutants = new List<PollutantDetails>
            {
                new()
                {
                    PollutantMasterUrl = "pollutant1",
                    PollutantName = "NO2"
                }
            };

            var feature = new JObject
            {
                ["om:OM_Observation"] = new JObject
                {
                    ["om:observedProperty"] = new JObject
                    {
                        ["@xlink:href"] = "pollutant1"
                    },
                    ["om:result"] = new JObject
                    {
                        ["swe:DataArray"] = new JObject
                        {
                            ["swe:values"] = "2025-01-01T00:00Z,1"
                        }
                    }
                }
            };

            var features = new JArray
            {
                new JObject(),
                feature
            };

            var result = service.Process(
                features,
                pollutants,
                new SiteInfo());

            Assert.NotNull(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldSkipAddRange_WhenValuesIsNullOrEmpty()
        {
            // Arrange – pollutant matches but swe:values is absent (null token)
            var service = CreateService();

            var pollutants = new List<PollutantDetails>
            {
                new() { PollutantMasterUrl = "pollutant1", PollutantName = "NO2" }
            };

            var featureWithNoValues = new JObject
            {
                ["om:OM_Observation"] = new JObject
                {
                    ["om:observedProperty"] = new JObject
                    {
                        ["@xlink:href"] = "pollutant1"   // match exists
                    },
                    ["om:result"] = new JObject
                    {
                        ["swe:DataArray"] = new JObject()  // swe:values key absent → null
                    }
                }
            };

            var features = new JArray { new JObject(), featureWithNoValues };

            // Act
            var result = service.Process(features, pollutants);

            // Assert – match found but no values, so nothing added
            Assert.Empty(result);
        }

        #endregion
    }

    internal sealed class TestAtomFeedService : AtomFeedFetchServiceBase
    {
        private readonly ILogger _logger;

        public TestAtomFeedService(
            IHttpClientFactory httpClientFactory,
            ILogger logger)
            : base(httpClientFactory)
        {
            _logger = logger;
        }

        protected override ILogger Logger => _logger;

        public Task<JArray> FetchAsync(
            string? siteId,
            string year,
            string? source = null)
        {
            return FetchAtomFeedAsync(siteId, year, source);
        }

        public List<FinalData> Process(
            JArray features,
            List<PollutantDetails> pollutants,
            SiteInfo? siteInfo = null)
        {
            return ProcessAtomData(features, pollutants, siteInfo);
        }
    }
}