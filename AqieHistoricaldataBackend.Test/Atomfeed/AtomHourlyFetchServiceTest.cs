using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq.Protected;
using Moq;
using Newtonsoft.Json.Linq;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using System.Net;
using System.Reflection;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomHourlyFetchServiceTests
    {
        private readonly Mock<ILogger<AtomHourlyFetchService>> _loggerMock = new();
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
        private readonly AtomHourlyFetchService _service;

        public AtomHourlyFetchServiceTests()
        {
            _loggerMock = new Mock<ILogger<AtomHourlyFetchService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _service = new AtomHourlyFetchService(_loggerMock.Object, _httpClientFactoryMock.Object);
        }

        [Fact]
        public void GetPollutantsToDisplay_ReturnsFiltered_WhenValidFilter()
        {
            var result = _service.GetType()
                .GetMethod("GetPollutantsToDisplay", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_service, new object[] { "PM10" }) as List<PollutantDetails>;

            Assert.Single(result);
            Assert.Equal("PM10", result[0].PollutantName);
        }

        [Fact]
        public void GetPollutantsToDisplay_ReturnsAll_WhenInvalidFilter()
        {
            var result = _service.GetType()
                .GetMethod("GetPollutantsToDisplay", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_service, new object[] { "Invalid" }) as List<PollutantDetails>;

            Assert.Equal(5, result.Count);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_OnHttpError()
        {
            var clientMock = new Mock<HttpMessageHandler>();
            clientMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var client = new HttpClient(clientMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);


            var method = _service.GetType()
             .GetMethod("FetchAtomFeedAsync", BindingFlags.NonPublic | BindingFlags.Instance);

            var task = (Task<JArray>)method.Invoke(_service, new object[] { "site", "2024" });
            var result = await task;


            Assert.Empty(result);
        }

        [Fact]
        public void ExtractFinalData_ReturnsParsedData_WhenValid()
        {
            var values = "2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,42@@2024-01-01T01:00,2024-01-01T02:00,Verified,Valid,43";
            var result = _service.GetType()
                .GetMethod("ExtractFinalData", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_service, new object[] { values, "PM10" }) as List<FinalData>;

            Assert.Equal(2, result.Count);
            Assert.Equal("42", result[0].Value);
        }

        [Fact]
        public void ExtractFinalData_IgnoresInvalidEntries()
        {
            var values = "invalid@@2024-01-01T01:00,2024-01-01T02:00,Verified,Valid,43";
            var result = _service.GetType()
                .GetMethod("ExtractFinalData", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_service, new object[] { values, "PM10" }) as List<FinalData>;

            Assert.Single(result);
        }

        [Fact]
        public void ProcessAtomData_HandlesMissingHref_Gracefully()
        {
            var pollutants = new List<PollutantDetails>
        {
            new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/5" }
        };

            var feature = new JObject
            {
                ["om:OM_Observation"] = new JObject
                {
                    ["om:observedProperty"] = new JObject()
                }
            };

            var features = new JArray { new JObject(), feature };
            var result = _service.GetType()
                .GetMethod("ProcessAtomData", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_service, new object[] { features, pollutants }) as List<FinalData>;

            Assert.Empty(result);
        }
       
        [Fact]
        public async Task GetAtomHourlydatafetch_ReturnsEmpty_WhenFeedIsEmpty()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<gml:FeatureCollection xmlns:gml='gml'></gml:FeatureCollection>")
            };
            var handler = new MockHttpMessageHandler(response);
            var client = new HttpClient(handler);
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);

            // Act
            var result = await _service.GetAtomHourlydatafetch("site", "2023", "");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }       

        [Fact]
        public async Task GetAtomHourlydatafetch_HandlesHttpException_AndLogsError()
        {
            // Arrange
            var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            var client = new HttpClient(handler);
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);

            // Act
            var result = await _service.GetAtomHourlydatafetch("site", "2023", "");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error fetching Atom feed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        // Helper for mocking HttpClient
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage _response;

            public MockHttpMessageHandler(HttpResponseMessage response)
            {
                _response = response;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
            }
        }
    }
}