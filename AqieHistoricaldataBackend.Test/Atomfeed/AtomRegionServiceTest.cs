using System.Net;
using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AqieHistoricaldataBackend.Tests.Services
{
    public class AtomRegionServiceTest
    {
        private readonly Mock<ILogger<AtomRegionService>> _loggerMock = new();
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
        private readonly Mock<IAuthService> _authServiceMock = new();

        private AtomRegionService CreateService(
            string? json = null,
            HttpStatusCode statusCode = HttpStatusCode.OK,
            Exception? httpException = null,
            Exception? tokenException = null,
            string token = "test-token")
        {
            if (tokenException is not null)
            {
                _authServiceMock.Setup(a => a.GetRicardoToken()).ThrowsAsync(tokenException);
            }
            else
            {
                _authServiceMock.Setup(a => a.GetRicardoToken()).ReturnsAsync(token);
            }

            var handler = new Mock<HttpMessageHandler>();
            var setup = handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());

            if (httpException is not null)
            {
                setup.ThrowsAsync(httpException);
            }
            else
            {
                setup.ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(json ?? "{}")
                });
            }

            var client = new HttpClient(handler.Object)
            {
                BaseAddress = new Uri("https://ricardo.test/")
            };

            _httpClientFactoryMock
                .Setup(x => x.CreateClient("RicardoNewAPI"))
                .Returns(client);

            return new AtomRegionService(
                _loggerMock.Object,
                _httpClientFactoryMock.Object,
                _authServiceMock.Object);
        }

        private static string BuildJson(params string?[] zoneRegions)
        {
            var members = zoneRegions.Select(z =>
                z is null
                    ? "{\"siteName\":\"S\"}"
                    : $"{{\"siteName\":\"S\",\"zoneRegion\":\"{z}\"}}");

            return "{\"member\":[" + string.Join(",", members) + "]}";
        }

        private void VerifyLog(LogLevel level, Times times) =>
            _loggerMock.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                times);

        [Fact]
        public async Task GetDistinctRegions_ReturnsEmptyList_WhenNoMembersReturned()
        {
            var service = CreateService(json: "{\"member\":[]}");

            var result = await service.GetDistinctRegions();

            Assert.NotNull(result);
            Assert.Empty(result);
            VerifyLog(LogLevel.Warning, Times.Once());
        }

        [Fact]
        public async Task GetDistinctRegions_ReturnsEmptyList_WhenMemberPropertyMissing()
        {
            var service = CreateService(json: "{}");

            var result = await service.GetDistinctRegions();

            Assert.Empty(result);
            VerifyLog(LogLevel.Warning, Times.Once());
        }

        [Fact]
        public async Task GetDistinctRegions_MapsRegionNames_FromRegionMaster()
        {
            var service = CreateService(json: BuildJson("1", "15"));

            var result = await service.GetDistinctRegions();

            Assert.Equal(2, result.Count);
            Assert.Equal("1", result[0].RegionId);
            Assert.Equal("Northern Ireland", result[0].RegionName);
            Assert.Equal("15", result[1].RegionId);
            Assert.Equal("Greater London", result[1].RegionName);
            VerifyLog(LogLevel.Information, Times.Once());
        }

        [Fact]
        public async Task GetDistinctRegions_RemovesDuplicates_IgnoringCaseAndWhitespace()
        {
            var service = CreateService(json: BuildJson("7", " 7 ", "7"));

            var result = await service.GetDistinctRegions();

            var region = Assert.Single(result);
            Assert.Equal("7", region.RegionId);
            Assert.Equal("Eastern", region.RegionName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetDistinctRegions_SkipsNullOrWhitespaceRegionIds(string? regionId)
        {
            var service = CreateService(json: BuildJson(regionId));

            var result = await service.GetDistinctRegions();

            Assert.Empty(result);
            VerifyLog(LogLevel.Information, Times.Once());
        }

        private static readonly string[] ExpectedOrderedRegionIds = ["1", "2", "10", "ABC"];

        [Fact]
        public async Task GetDistinctRegions_OrdersNumericallyAndPlacesNonNumericLast()
        {
            var service = CreateService(json: BuildJson("10", "ABC", "2", "1"));

            var result = await service.GetDistinctRegions();

            Assert.Equal(ExpectedOrderedRegionIds, result.Select(r => r.RegionId).ToArray());
        }

        [Fact]
        public async Task GetDistinctRegions_ReturnsFallbackName_WhenRegionIdIsUnknown()
        {
            var service = CreateService(json: BuildJson("999"));

            var result = await service.GetDistinctRegions();

            var region = Assert.Single(result);
            Assert.Equal("999", region.RegionId);
            Assert.Equal("Not found in provided mapping", region.RegionName);
        }

        [Fact]
        public async Task GetDistinctRegions_ReturnsEmptyList_WhenHttpResponseIsUnsuccessful()
        {
            var service = CreateService(json: "{}", statusCode: HttpStatusCode.InternalServerError);

            var result = await service.GetDistinctRegions();

            Assert.Empty(result);
            VerifyLog(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task GetDistinctRegions_ReturnsEmptyList_WhenHttpCallThrows()
        {
            var service = CreateService(httpException: new HttpRequestException("network down"));

            var result = await service.GetDistinctRegions();

            Assert.Empty(result);
            VerifyLog(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task GetDistinctRegions_ReturnsEmptyList_WhenPayloadIsInvalidJson()
        {
            var service = CreateService(json: "not-json");

            var result = await service.GetDistinctRegions();

            Assert.Empty(result);
            VerifyLog(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task GetDistinctRegions_ReturnsEmptyList_WhenTokenRetrievalThrows()
        {
            var service = CreateService(tokenException: new InvalidOperationException("token failure"));

            var result = await service.GetDistinctRegions();

            Assert.Empty(result);
            _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
            VerifyLog(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task GetDistinctRegions_PassesBearerTokenToRicardoClient()
        {
            var service = CreateService(json: BuildJson("6"), token: "bearer-abc");

            var result = await service.GetDistinctRegions();

            Assert.Single(result);
            _authServiceMock.Verify(a => a.GetRicardoToken(), Times.Once);
            _httpClientFactoryMock.Verify(x => x.CreateClient("RicardoNewAPI"), Times.Once);
        }
    }
}