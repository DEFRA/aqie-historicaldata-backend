using Xunit;
using Moq;
using Moq.Protected;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.IO;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomHourlyFetchServiceTests
    {
        private readonly Mock<ILogger<AtomHourlyFetchService>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly AtomHourlyFetchService _service;

        public AtomHourlyFetchServiceTests()
        {
            _mockLogger = new Mock<ILogger<AtomHourlyFetchService>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _service = new AtomHourlyFetchService(_mockLogger.Object, _mockHttpClientFactory.Object);
        }

        private HttpClient CreateMockHttpClient(HttpResponseMessage response)
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            return new HttpClient(handler.Object);
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_ReturnsEmpty_WhenHttpFails()
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            var client = CreateMockHttpClient(response);
            _mockHttpClientFactory.Setup(f => f.CreateClient("Atomfeed")).Returns(client);

            var result = await _service.GetAtomHourlydatafetch("BEX", "2025", "PM10");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_ReturnsEmpty_WhenXmlIsMalformed()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<invalid><xml>", Encoding.UTF8, "application/xml")
            };

            var client = CreateMockHttpClient(response);
            _mockHttpClientFactory.Setup(f => f.CreateClient("Atomfeed")).Returns(client);

            var result = await _service.GetAtomHourlydatafetch("BEX", "2025", "PM10");

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomHourlydatafetch_Skips_WhenObservedPropertyMissing()
        {
            var xml = @"<gml:FeatureCollection xmlns:gml='gml' xmlns:om='om' xmlns:swe='swe'>
            <gml:featureMember>
                <om:OM_Observation>
                    <om:result>
                        <swe:DataArray>
                            <swe:values>2025-01-01T00:00,2025-01-01T01:00,1,1,40</swe:values>
                        </swe:DataArray>
                    </om:result>
                </om:OM_Observation>
            </gml:featureMember>
        </gml:FeatureCollection>";

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml, Encoding.UTF8, "application/xml")
            };

            var client = CreateMockHttpClient(response);
            _mockHttpClientFactory.Setup(f => f.CreateClient("Atomfeed")).Returns(client);

            var result = await _service.GetAtomHourlydatafetch("BEX", "2025", "PM10");

            Assert.Empty(result);
        }
    }
}