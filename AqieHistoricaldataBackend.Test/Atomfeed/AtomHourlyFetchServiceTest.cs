using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq.Protected;
using Moq;
using Newtonsoft.Json.Linq;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using System.Net;
using System.Reflection;
using System.Text;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomHourlyFetchServiceTests
    {
        private readonly Mock<ILogger<AtomHourlyFetchService>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly AtomHourlyFetchService _service;

        public AtomHourlyFetchServiceTests()
        {
            _loggerMock = new Mock<ILogger<AtomHourlyFetchService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _service = new AtomHourlyFetchService(_loggerMock.Object, _httpClientFactoryMock.Object);
        }

        #region GetPollutantsToDisplay

        [Fact]
        public void GetPollutantsToDisplay_ReturnsFiltered_WhenValidFilter()
        {
            var result = InvokeGetPollutantsToDisplay("PM10");

            Assert.Single(result);
            Assert.Equal("PM10", result[0].PollutantName);
        }

        [Theory]
        [InlineData("Nitrogen dioxide")]
        [InlineData("PM10")]
        [InlineData("PM2.5")]
        [InlineData("Ozone")]
        [InlineData("Sulphur dioxide")]
        public void GetPollutantsToDisplay_ReturnsSingleMatch_ForEachKnownPollutant(string pollutantName)
        {
            var result = InvokeGetPollutantsToDisplay(pollutantName);

            Assert.Single(result);
            Assert.Equal(pollutantName, result[0].PollutantName);
        }

        [Fact]
        public void GetPollutantsToDisplay_ReturnsAll_WhenInvalidFilter()
        {
            var result = InvokeGetPollutantsToDisplay("Invalid");

            Assert.Equal(5, result.Count);
        }

        [Fact]
        public void GetPollutantsToDisplay_ReturnsAll_WhenEmptyFilter()
        {
            var result = InvokeGetPollutantsToDisplay(string.Empty);

            Assert.Equal(5, result.Count);
        }

        [Fact]
        public void GetPollutantsToDisplay_ReturnsAll_WhenNullFilter()
        {
            var result = InvokeGetPollutantsToDisplay(null);

            Assert.Equal(5, result.Count);
        }

        [Fact]
        public void GetPollutantsToDisplay_IsCaseSensitive_ReturnsAll_WhenWrongCase()
        {
            var result = InvokeGetPollutantsToDisplay("pm10");

            Assert.Equal(5, result.Count);
        }

        #endregion

        #region FetchAtomFeedAsync

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_OnHttpError()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var result = await InvokeFetchAtomFeedAsync("site", "2024");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_On304NotModified()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.NotModified));

            var result = await InvokeFetchAtomFeedAsync("site", "2024");

            Assert.Empty(result);
            _loggerMock.VerifyLog(LogLevel.Warning, "304 Not Modified", Times.Once());
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_On428PreconditionRequired()
        {
            SetupHttpClient(new HttpResponseMessage((HttpStatusCode)428)
            {
                Content = new StringContent("Precondition Required")
            });

            var result = await InvokeFetchAtomFeedAsync("site", "2024");

            Assert.Empty(result);
            _loggerMock.VerifyLog(LogLevel.Error, "428 Precondition Required", Times.Once());
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_On404NotFound()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Not Found")
            });

            var result = await InvokeFetchAtomFeedAsync("site", "2024");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_WhenHttpRequestExceptionThrown()
        {
            var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);

            var result = await InvokeFetchAtomFeedAsync("site", "2024");

            Assert.Empty(result);
            _loggerMock.VerifyLog(LogLevel.Error, "HTTP error fetching", Times.Once());
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsEmptyJArray_WhenGeneralExceptionThrown()
        {
            var handler = new ThrowingHttpMessageHandler(new InvalidOperationException("Unexpected"));
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);

            var result = await InvokeFetchAtomFeedAsync("site", "2024");

            Assert.Empty(result);
            _loggerMock.VerifyLog(LogLevel.Error, "Error fetching Atom feed", Times.Once());
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsJArray_WhenValidXmlResponse()
        {
            var xml = BuildXmlWithFeatureMembers(
                "<om:OM_Observation>" +
                "  <om:observedProperty xlink:href=\"http://dd.eionet.europa.eu/vocabulary/aq/pollutant/5\"/>" +
                "  <om:result><swe:DataArray><swe:values>2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,42</swe:values></swe:DataArray></om:result>" +
                "</om:OM_Observation>");

            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml, Encoding.UTF8, "application/xml")
            });

            var result = await InvokeFetchAtomFeedAsync("ABD", "2024");

            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ReturnsNull_WhenXmlLacksFeatureMember()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                      "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\"></gml:FeatureCollection>";

            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml, Encoding.UTF8, "application/xml")
            });

            // The service returns JObject["gml:featureMember"] as JArray which will be null
            // and ProcessAtomData handles a null JArray gracefully via GetAtomHourlydatafetch
            var result = await _service.GetAtomHourlydatafetch("ABD", "2024", "");

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region ExtractFinalData

        [Fact]
        public void ExtractFinalData_ReturnsParsedData_WhenValid()
        {
            var values = "2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,42@@2024-01-01T01:00,2024-01-01T02:00,Verified,Valid,43";
            var result = InvokeExtractFinalData(values, "PM10");

            Assert.Equal(2, result.Count);
            Assert.Equal("42", result[0].Value);
            Assert.Equal("43", result[1].Value);
            Assert.All(result, r => Assert.Equal("PM10", r.PollutantName));
        }

        [Fact]
        public void ExtractFinalData_IgnoresInvalidEntries()
        {
            var values = "invalid@@2024-01-01T01:00,2024-01-01T02:00,Verified,Valid,43";
            var result = InvokeExtractFinalData(values, "PM10");

            Assert.Single(result);
            Assert.Equal("43", result[0].Value);
        }

        [Fact]
        public void ExtractFinalData_MapsAllFieldsCorrectly()
        {
            var values = "2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,55";
            var result = InvokeExtractFinalData(values, "Ozone");

            Assert.Single(result);
            Assert.Equal("2024-01-01T00:00", result[0].StartTime);
            Assert.Equal("2024-01-01T01:00", result[0].EndTime);
            Assert.Equal("Verified", result[0].Verification);
            Assert.Equal("Valid", result[0].Validity);
            Assert.Equal("55", result[0].Value);
            Assert.Equal("Ozone", result[0].PollutantName);
        }

        [Fact]
        public void ExtractFinalData_StripsCarriageReturnAndNewline()
        {
            var values = "\r\n2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,99\r\n";
            var result = InvokeExtractFinalData(values, "PM2.5");

            Assert.Single(result);
            Assert.Equal("99", result[0].Value);
        }

        [Fact]
        public void ExtractFinalData_ReturnsEmpty_WhenAllEntriesInvalid()
        {
            var values = "bad@@alsoBad@@nope";
            var result = InvokeExtractFinalData(values, "PM10");

            Assert.Empty(result);
        }

        [Fact]
        public void ExtractFinalData_ReturnsEmpty_WhenValuesIsEmptyString()
        {
            var result = InvokeExtractFinalData(string.Empty, "PM10");

            Assert.Empty(result);
        }

        [Fact]
        public void ExtractFinalData_HandlesExactlyFiveParts()
        {
            var values = "A,B,C,D,E";
            var result = InvokeExtractFinalData(values, "Ozone");

            Assert.Single(result);
        }

        [Fact]
        public void ExtractFinalData_HandlesMoreThanFiveParts_TakesFirstFive()
        {
            var values = "A,B,C,D,E,ExtraField";
            var result = InvokeExtractFinalData(values, "Ozone");

            // Where(parts.Length >= 5) passes — only first 5 are mapped
            Assert.Single(result);
            Assert.Equal("E", result[0].Value);
        }

        #endregion

        #region ProcessAtomData

        [Fact]
        public void ProcessAtomData_HandlesMissingHref_Gracefully()
        {
            var pollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" }
            };

            var feature = new JObject
            {
                ["om:OM_Observation"] = new JObject
                {
                    ["om:observedProperty"] = new JObject()
                }
            };

            var features = new JArray { new JObject(), feature };
            var result = InvokeProcessAtomData(features, pollutants);

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_SkipsFirstElement()
        {
            // The loop starts at i = 1, so index 0 is always skipped
            var pollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" }
            };

            var featureAtIndex0 = BuildFeatureObject(
                "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/5",
                "2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,42");

            // Only one element at index 0 — loop body never runs
            var features = new JArray { featureAtIndex0 };
            var result = InvokeProcessAtomData(features, pollutants);

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ReturnsEmpty_WhenFeaturesIsEmpty()
        {
            var pollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" }
            };

            var result = InvokeProcessAtomData(new JArray(), pollutants);

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_SkipsFeature_WhenHrefDoesNotMatchAnyPollutant()
        {
            var pollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" }
            };

            var feature = BuildFeatureObject(
                "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/999",
                "2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,42");

            var features = new JArray { new JObject(), feature };
            var result = InvokeProcessAtomData(features, pollutants);

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_SkipsFeature_WhenValuesIsEmpty()
        {
            var pollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" }
            };

            var feature = new JObject
            {
                ["om:OM_Observation"] = new JObject
                {
                    ["om:observedProperty"] = new JObject { ["@xlink:href"] = "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/5" },
                    ["om:result"] = new JObject
                    {
                        ["swe:DataArray"] = new JObject
                        {
                            ["swe:values"] = ""
                        }
                    }
                }
            };

            var features = new JArray { new JObject(), feature };
            var result = InvokeProcessAtomData(features, pollutants);

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ReturnsData_WhenMatchingPollutantAndValuesExist()
        {
            var pollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" }
            };

            var feature = BuildFeatureObject(
                "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/5",
                "2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,42");

            var features = new JArray { new JObject(), feature };
            var result = InvokeProcessAtomData(features, pollutants);

            Assert.Single(result);
            Assert.Equal("42", result[0].Value);
            Assert.Equal("PM10", result[0].PollutantName);
        }

        [Fact]
        public void ProcessAtomData_HandlesMultipleMatchingFeatures()
        {
            var pollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" },
                new PollutantDetails { PollutantName = "Ozone", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/7" }
            };

            var feature1 = BuildFeatureObject(
                "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/5",
                "2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,42");

            var feature2 = BuildFeatureObject(
                "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/7",
                "2024-01-01T01:00,2024-01-01T02:00,Verified,Valid,88");

            var features = new JArray { new JObject(), feature1, feature2 };
            var result = InvokeProcessAtomData(features, pollutants);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void ProcessAtomData_ContinuesProcessing_WhenOneFeatureThrows()
        {
            var pollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" }
            };

            // A malformed feature that causes an exception followed by a valid one
            var badFeature = new JValue("not an object");
            var goodFeature = BuildFeatureObject(
                "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/5",
                "2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,99");

            var features = new JArray { new JObject(), badFeature, goodFeature };
            var result = InvokeProcessAtomData(features, pollutants);

            Assert.Single(result);
            Assert.Equal("99", result[0].Value);
            _loggerMock.VerifyLog(LogLevel.Error, "Error processing ProcessAtomData", Times.Once());
        }

        [Fact]
        public void ProcessAtomData_SkipsFeature_WhenValuesNodeIsMissing()
        {
            var pollutants = new List<PollutantDetails>
            {
                new PollutantDetails { PollutantName = "PM10", PollutantMasterUrl = "dd.eionet.europa.eu/vocabulary/aq/pollutant/5" }
            };

            var feature = new JObject
            {
                ["om:OM_Observation"] = new JObject
                {
                    ["om:observedProperty"] = new JObject { ["@xlink:href"] = "http://dd.eionet.europa.eu/vocabulary/aq/pollutant/5" }
                    // om:result is intentionally absent
                }
            };

            var features = new JArray { new JObject(), feature };
            var result = InvokeProcessAtomData(features, pollutants);

            Assert.Empty(result);
        }

        #endregion

        #region GetAtomHourlydatafetch (integration)

        [Fact]
        public async Task GetAtomHourlydatafetch_ReturnsEmpty_WhenFeedIsEmpty()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<gml:FeatureCollection xmlns:gml='gml'></gml:FeatureCollection>")
            };
            SetupHttpClient(response);

            var result = await _service.GetAtomHourlydatafetch("site", "2023", "");

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_HandlesHttpException_AndLogsError()
        {
            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var result = await _service.GetAtomHourlydatafetch("site", "2023", "");

            Assert.NotNull(result);
            Assert.Empty(result);
            _loggerMock.VerifyLog(LogLevel.Warning, "500", Times.Once());
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_ReturnsData_WhenValidXmlAndMatchingPollutant()
        {
            var xml = BuildXmlWithFeatureMembers(
                "<om:OM_Observation>" +
                "  <om:observedProperty xlink:href=\"http://dd.eionet.europa.eu/vocabulary/aq/pollutant/5\"/>" +
                "  <om:result><swe:DataArray><swe:values>2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,42</swe:values></swe:DataArray></om:result>" +
                "</om:OM_Observation>");

            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml, Encoding.UTF8, "application/xml")
            });

            var result = await _service.GetAtomHourlydatafetch("ABD", "2024", "PM10");

            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Equal("PM10", result[0].PollutantName);
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_ReturnsEmpty_WhenPollutantFilterDoesNotMatch()
        {
            var xml = BuildXmlWithFeatureMembers(
                "<om:OM_Observation>" +
                "  <om:observedProperty xlink:href=\"http://dd.eionet.europa.eu/vocabulary/aq/pollutant/5\"/>" +
                "  <om:result><swe:DataArray><swe:values>2024-01-01T00:00,2024-01-01T01:00,Verified,Valid,42</swe:values></swe:DataArray></om:result>" +
                "</om:OM_Observation>");

            SetupHttpClient(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml, Encoding.UTF8, "application/xml")
            });

            // "Ozone" filter → only Ozone pollutant, but feed only has PM10 → no match
            var result = await _service.GetAtomHourlydatafetch("ABD", "2024", "Ozone");

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region Helpers

        private List<PollutantDetails> InvokeGetPollutantsToDisplay(string filter)
        {
            return _service.GetType()
                .GetMethod("GetPollutantsToDisplay", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_service, new object[] { filter }) as List<PollutantDetails>;
        }

        private async Task<JArray> InvokeFetchAtomFeedAsync(string siteId, string year)
        {
            var method = _service.GetType()
                .GetMethod("FetchAtomFeedAsync", BindingFlags.NonPublic | BindingFlags.Instance);

            return await (Task<JArray>)method.Invoke(_service, new object[] { siteId, year });
        }

        private List<FinalData> InvokeExtractFinalData(string values, string pollutantName)
        {
            return _service.GetType()
                .GetMethod("ExtractFinalData", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_service, new object[] { values, pollutantName }) as List<FinalData>;
        }

        private List<FinalData> InvokeProcessAtomData(JArray features, List<PollutantDetails> pollutants)
        {
            return _service.GetType()
                .GetMethod("ProcessAtomData", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_service, new object[] { features, pollutants }) as List<FinalData>;
        }

        private void SetupHttpClient(HttpResponseMessage response)
        {
            var handler = new MockHttpMessageHandler(response);
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            _httpClientFactoryMock.Setup(f => f.CreateClient("Atomfeed")).Returns(client);
        }

        private static JObject BuildFeatureObject(string href, string values)
        {
            return new JObject
            {
                ["om:OM_Observation"] = new JObject
                {
                    ["om:observedProperty"] = new JObject { ["@xlink:href"] = href },
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

        private static string BuildXmlWithFeatureMembers(string featureMemberContent)
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                   "<gml:FeatureCollection " +
                   "  xmlns:gml=\"http://www.opengis.net/gml/3.2\" " +
                   "  xmlns:om=\"http://www.opengis.net/om/2.0\" " +
                   "  xmlns:swe=\"http://www.opengis.net/swe/2.0\" " +
                   "  xmlns:xlink=\"http://www.w3.org/1999/xlink\">" +
                   "  <gml:featureMember></gml:featureMember>" +
                   $"  <gml:featureMember>{featureMemberContent}</gml:featureMember>" +
                   "</gml:FeatureCollection>";
        }

        private sealed class MockHttpMessageHandler : HttpMessageHandler
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

        private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
        {
            private readonly Exception _exception;

            public ThrowingHttpMessageHandler(Exception exception)
            {
                _exception = exception;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw _exception;
            }
        }

        #endregion
    }

    internal static class LoggerMockExtensions
    {
        internal static void VerifyLog<T>(
            this Mock<ILogger<T>> loggerMock,
            LogLevel level,
            string containsMessage,
            Times times)
        {
            loggerMock.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString().Contains(containsMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                times);
        }
    }
}