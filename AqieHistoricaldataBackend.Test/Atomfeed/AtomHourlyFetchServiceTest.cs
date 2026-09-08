using System.Net;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using Xunit;
using AqieHistoricaldataBackend.Atomfeed.Services;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomHourlyFetchServiceTests
    {
        private readonly Mock<ILogger<AtomHourlyFetchService>> _loggerMock = new();
        private readonly Mock<IHttpClientFactory> _factoryMock = new();

        private const string ValidAtomXml = """
<?xml version="1.0" encoding="utf-8"?>
<gml:FeatureCollection xmlns:gml="http://www.opengis.net/gml/3.2"
      xmlns:om="http://www.opengis.net/om/2.0"
      xmlns:swe="http://www.opengis.net/swe/2.0"
      xmlns:xlink="http://www.w3.org/1999/xlink">

    <gml:featureMember>
      <dummy></dummy>
    </gml:featureMember>

    <gml:featureMember>
      <om:OM_Observation>
        <om:observedProperty xlink:href="8" />
        <om:result>
          <swe:DataArray>
            <swe:values>
2025-01-01T00:00:00Z,2025-01-01T01:00:00Z,V,Y,12.5
            </swe:values>
          </swe:DataArray>
        </om:result>
      </om:OM_Observation>
    </gml:featureMember>

</gml:FeatureCollection>
""";

        private AtomHourlyFetchService CreateService(
            HttpResponseMessage? response = null,
            Exception? exception = null)
        {
            var handler = new Mock<HttpMessageHandler>();

            if (exception != null)
            {
                handler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ThrowsAsync(exception);
            }
            else
            {
                handler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(response!);
            }

            var client = new HttpClient(handler.Object)
            {
                BaseAddress = new Uri("https://unit-test/")
            };

            _factoryMock.Setup(x => x.CreateClient("Atomfeed"))
                .Returns(client);

            return new AtomHourlyFetchService(
                _loggerMock.Object,
                _factoryMock.Object);
        }

        #region GetAtomHourlydatafetch

        [Fact]
        public async Task GetAtomHourlydatafetch_ShouldReturnEmpty_WhenSiteIdEmpty()
        {
            var service = CreateService(
                new HttpResponseMessage(HttpStatusCode.OK));

            var result =
                await service.GetAtomHourlydatafetch(
                    string.Empty,
                    "2025",
                    "PM10");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_ShouldReturnEmpty_WhenYearEmpty()
        {
            var service = CreateService(
                new HttpResponseMessage(HttpStatusCode.OK));

            var result =
                await service.GetAtomHourlydatafetch(
                    "SITE1",
                    string.Empty,
                    "PM10");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_ShouldReturnEmpty_When404Returned()
        {
            var service = CreateService(
                new HttpResponseMessage(HttpStatusCode.NotFound));

            var result =
                await service.GetAtomHourlydatafetch(
                    "SITE1",
                    "2025",
                    "PM10");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_ShouldReturnEmpty_When304Returned()
        {
            var service = CreateService(
                new HttpResponseMessage(HttpStatusCode.NotModified));

            var result =
                await service.GetAtomHourlydatafetch(
                    "SITE1",
                    "2025",
                    "PM10");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_ShouldReturnEmpty_When428Returned()
        {
            var service = CreateService(
                new HttpResponseMessage(HttpStatusCode.PreconditionRequired)
                {
                    Content = new StringContent("428")
                });

            var result =
                await service.GetAtomHourlydatafetch(
                    "SITE1",
                    "2025",
                    "PM10");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_ShouldReturnEmpty_When500Returned()
        {
            var service = CreateService(
                new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("error")
                });

            var result =
                await service.GetAtomHourlydatafetch(
                    "SITE1",
                    "2025",
                    "PM10");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_ShouldMapAtomData_WhenPollutantFound()
        {
            var service = CreateService(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ValidAtomXml)
                });

            var result =
                await service.GetAtomHourlydatafetch(
                    "SITE1",
                    "2025",
                    "Nitrogen dioxide");

            Assert.Single(result);

            var record = result.First();

            Assert.Equal("Nitrogen dioxide", record.PollutantName);
            Assert.Equal("2025-01-01T00:00:00Z", record.StartTime);
            Assert.Equal("2025-01-01T01:00:00Z", record.EndTime);
            Assert.Equal("V", record.Verification);
            Assert.Equal("Y", record.Validity);
            Assert.Equal("12.5", record.Value);
        }

        [Theory]
        [InlineData("Nitrogen dioxide")]
        [InlineData("PM10")]
        [InlineData("PM2.5")]
        [InlineData("Ozone")]
        [InlineData("Sulphur dioxide")]
        public async Task GetAtomHourlydatafetch_ShouldSupportConfiguredPollutants(
            string pollutant)
        {
            var service = CreateService(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ValidAtomXml)
                });

            var result =
                await service.GetAtomHourlydatafetch(
                    "SITE1",
                    "2025",
                    pollutant);

            Assert.NotNull(result);
        }

        #endregion

        #region Private Method Reflection

        [Fact]
        public void GetPollutantsToDisplay_ShouldReturnExactMatch()
        {
            var method =
                typeof(AtomHourlyFetchService)
                    .GetMethod(
                        "GetPollutantsToDisplay",
                        BindingFlags.NonPublic | BindingFlags.Static);

            var result =
                (List<PollutantDetails>)method!
                    .Invoke(null, new object[] { "PM10" })!;

            Assert.Single(result);
            Assert.Equal("PM10", result[0].PollutantName);
            Assert.Equal("5", result[0].PollutantMasterUrl);
        }

        [Fact]
        public void GetPollutantsToDisplay_ShouldReturnAll_WhenUnknown()
        {
            var method =
                typeof(AtomHourlyFetchService)
                    .GetMethod(
                        "GetPollutantsToDisplay",
                        BindingFlags.NonPublic | BindingFlags.Static);

            var result =
                (List<PollutantDetails>)method!
                    .Invoke(null, new object[] { "UNKNOWN" })!;

            Assert.Equal(5, result.Count);
        }

        [Fact]
        public void GetPollutantsToDisplay_ShouldReturnAll_WhenCaseDoesNotMatch()
        {
            var method =
                typeof(AtomHourlyFetchService)
                    .GetMethod(
                        "GetPollutantsToDisplay",
                        BindingFlags.NonPublic | BindingFlags.Static);

            var result =
                (List<PollutantDetails>)method!
                    .Invoke(null, new object[] { "pm10" })!;

            Assert.Equal(5, result.Count);
        }

        [Fact]
        public void GetPollutantsToDisplay_ShouldReturnAll_WhenEmpty()
        {
            var method =
                typeof(AtomHourlyFetchService)
                    .GetMethod(
                        "GetPollutantsToDisplay",
                        BindingFlags.NonPublic | BindingFlags.Static);

            var result =
                (List<PollutantDetails>)method!
                    .Invoke(null, new object[] { "" })!;

            Assert.Equal(5, result.Count);
        }

        #endregion
    }

    public class AtomFeedFetchServiceBaseTests
    {
        private readonly Mock<ILogger> _logger = new();
        private readonly Mock<IHttpClientFactory> _factory = new();

        private TestAtomFeedService CreateService(
            HttpResponseMessage? response = null,
            Exception? exception = null)
        {
            var handler = new Mock<HttpMessageHandler>();

            if (exception != null)
            {
                handler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ThrowsAsync(exception);
            }
            else
            {
                handler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(response!);
            }

            var client = new HttpClient(handler.Object)
            {
                BaseAddress = new Uri("https://unit-test/")
            };

            _factory.Setup(x => x.CreateClient("Atomfeed"))
                .Returns(client);

            return new TestAtomFeedService(
                _factory.Object,
                _logger.Object);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldHandleHttpRequestException404()
        {
            var service = CreateService(
                exception: new HttpRequestException(
                    "404",
                    null,
                    HttpStatusCode.NotFound));

            var result =
                await service.FetchAsync(
                    "SITE",
                    "2025");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldHandleHttpRequestException()
        {
            var service = CreateService(
                exception: new HttpRequestException(
                    "network error"));

            var result =
                await service.FetchAsync(
                    "SITE",
                    "2025");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldHandleGeneralException()
        {
            var service = CreateService(
                exception: new InvalidOperationException(
                    "boom"));

            var result =
                await service.FetchAsync(
                    "SITE",
                    "2025");

            Assert.Empty(result);
        }

        [Fact]
        public async Task FetchAtomFeedAsync_ShouldUseNonAutoPath()
        {
            HttpRequestMessage? request = null;

            var handler = new Mock<HttpMessageHandler>();

            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>(
                    (r, _) => request = r)
                .ReturnsAsync(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "<root/>")
                    });

            var client = new HttpClient(handler.Object)
            {
                BaseAddress = new Uri("https://test/")
            };

            var factory = new Mock<IHttpClientFactory>();

            factory.Setup(x => x.CreateClient("Atomfeed"))
                .Returns(client);

            var service =
                new TestAtomFeedService(
                    factory.Object,
                    Mock.Of<ILogger>());

            await service.FetchAsync(
                "SITE1",
                "2025",
                "NON_AURN");

            Assert.Contains(
                "non-auto",
                request!.RequestUri!.ToString());
        }

        [Fact]
        public void ProcessAtomData_ShouldReturnEmpty_ForNullFeatures()
        {
            var service = CreateService();

            var result =
                service.Process(
                    null!,
                    new List<PollutantDetails>());

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldReturnEmpty_ForEmptyFeatures()
        {
            var service = CreateService();

            var result =
                service.Process(
                    new JArray(),
                    new List<PollutantDetails>());

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldSkipMissingHref()
        {
            var service = CreateService();

            var features =
                new JArray
                {
                    new JObject(),
                    JObject.Parse("""
                    {
                      "om:OM_Observation": {}
                    }
                    """)
                };

            var result =
                service.Process(
                    features,
                    new List<PollutantDetails>());

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldSkipUnknownPollutant()
        {
            var service = CreateService();

            var features =
                BuildFeature(
                    "9999",
                    "A,B,C,D,E");

            var result =
                service.Process(
                    features,
                    Pollutants());

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldSkipEmptyValues()
        {
            var service = CreateService();

            var features =
                BuildFeature(
                    "8",
                    "");

            var result =
                service.Process(
                    features,
                    Pollutants());

            Assert.Empty(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldProcessValues()
        {
            var service = CreateService();

            var features =
                BuildFeature(
                    "8",
                    "A,B,C,D,E");

            var result =
                service.Process(
                    features,
                    Pollutants());

            Assert.Single(result);
        }

        [Fact]
        public void ProcessAtomData_ShouldProcessValues_WithSiteInfo()
        {
            var service = CreateService();

            var site =
                new SiteInfo
                {
                    SiteName = "Site1",
                    AreaType = "Urban",
                    SiteType = "Traffic",
                    ZoneRegion = "London",
                    Country = "UK"
                };

            var features =
                BuildFeature(
                    "8",
                    "A,B,C,D,E");

            var result =
                service.Process(
                    features,
                    Pollutants(),
                    site);

            Assert.Single(result);
            Assert.Equal("Site1", result[0].SiteName);
            Assert.Equal("UrbanTraffic", result[0].SiteType);
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

            var result =
                service.Process(
                    features,
                    Pollutants());

            Assert.Empty(result);
        }

        private static List<PollutantDetails> Pollutants()
        {
            return new()
            {
                new PollutantDetails
                {
                    PollutantName = "Nitrogen dioxide",
                    PollutantMasterUrl = "8"
                }
            };
        }

        private static JArray BuildFeature(
            string href,
            string values)
        {
            return new JArray
            {
                new JObject(),
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
                }
            };
        }
    }

    public class AtomFeedHelperTests
    {
        [Fact]
        public void ParseXmlStreamToFeatureArray_ShouldReturnFeatures()
        {
            var xml =
                """
                <root xmlns:gml="http://www.opengis.net/gml">
                  <gml:FeatureCollection>
                    <gml:featureMember/>
                    <gml:featureMember/>
                  </gml:FeatureCollection>
                </root>
                """;

            using var stream =
                new MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes(xml));

            var result =
                AtomFeedHelper
                    .ParseXmlStreamToFeatureArray(stream);

            Assert.NotNull(result);
        }

        [Fact]
        public void ParseXmlStreamToFeatureArray_ShouldReturnEmpty()
        {
            using var stream =
                new MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes("<root/>"));

            var result =
                AtomFeedHelper
                    .ParseXmlStreamToFeatureArray(stream);

            Assert.Empty(result);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("8", "8")]
        [InlineData("http://test/8", "8")]
        public void ExtractPollutantId_ShouldReturnExpected(
            string? value,
            string? expected)
        {
            var result =
                AtomFeedHelper.ExtractPollutantId(value);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SplitSweValues_ShouldReturnOnlyValidRows()
        {
            var result =
                AtomFeedHelper.SplitSweValues(
                    "A,B,C,D,E@@1,2@@F,G,H,I,J")
                .ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void ToFinalData_ShouldMapValues()
        {
            var rows =
                AtomFeedHelper.SplitSweValues(
                    "A,B,C,D,E");

            var result =
                AtomFeedHelper.ToFinalData(
                    rows,
                    "Nitrogen dioxide");

            Assert.Single(result);
            Assert.Equal("Nitrogen dioxide", result[0].PollutantName);
        }

        [Fact]
        public void ToFinalData_ShouldMapSiteInfo()
        {
            var rows =
                AtomFeedHelper.SplitSweValues(
                    "A,B,C,D,E");

            var site =
                new SiteInfo
                {
                    SiteName = "Site1",
                    AreaType = "Urban",
                    SiteType = "Traffic",
                    ZoneRegion = "London",
                    Country = "UK"
                };

            var result =
                AtomFeedHelper.ToFinalData(
                    rows,
                    "Nitrogen dioxide",
                    site);

            Assert.Single(result);
            Assert.Equal("Site1", result[0].SiteName);
            Assert.Equal("UrbanTraffic", result[0].SiteType);
            Assert.Equal("London", result[0].Region);
            Assert.Equal("UK", result[0].Country);
        }
    }

    internal sealed class TestAtomFeedService
        : AtomFeedFetchServiceBase
    {
        private readonly ILogger _logger;

        public TestAtomFeedService(
            IHttpClientFactory factory,
            ILogger logger)
            : base(factory)
        {
            _logger = logger;
        }

        protected override ILogger Logger => _logger;

        public Task<JArray> FetchAsync(
            string siteId,
            string year,
            string? dataSource = null)
        {
            return FetchAtomFeedAsync(
                siteId,
                year,
                dataSource);
        }

        public List<FinalData> Process(
            JArray features,
            List<PollutantDetails> pollutants,
            SiteInfo? siteInfo = null)
        {
            return ProcessAtomData(
                features,
                pollutants,
                siteInfo);
        }
    }
}