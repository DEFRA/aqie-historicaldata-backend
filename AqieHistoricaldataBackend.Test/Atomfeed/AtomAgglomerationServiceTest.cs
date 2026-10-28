using System.Net;
using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AqieHistoricaldataBackend.Tests.Services
{
    public class AtomAgglomerationServiceTest
    {
        private readonly Mock<ILogger<AtomAgglomerationService>> _loggerMock = new();
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
        private readonly Mock<IAuthService> _authServiceMock = new();

        private AtomAgglomerationService CreateService(
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

            return new AtomAgglomerationService(
                _loggerMock.Object,
                _httpClientFactoryMock.Object,
                _authServiceMock.Object);
        }

        private static string BuildJson(params string?[] agglomerationIds)
        {
            var members = agglomerationIds.Select(a =>
                a is null
                    ? "{\"siteName\":\"S\"}"
                    : $"{{\"siteName\":\"S\",\"agglomerationRegion\":\"{a}\"}}");

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
        public async Task GetDistinctAgglomerations_ReturnsEmptyList_WhenNoMembersReturned()
        {
            var service = CreateService(json: "{\"member\":[]}");

            var result = await service.GetDistinctAgglomerations();

            Assert.NotNull(result);
            Assert.Empty(result);
            VerifyLog(LogLevel.Warning, Times.Once());
        }

        [Fact]
        public async Task GetDistinctAgglomerations_ReturnsEmptyList_WhenMemberPropertyMissing()
        {
            var service = CreateService(json: "{}");

            var result = await service.GetDistinctAgglomerations();

            Assert.Empty(result);
            VerifyLog(LogLevel.Warning, Times.Once());
        }

        [Fact]
        public async Task GetDistinctAgglomerations_MapsRegionNames_FromAgglomerationMaster()
        {
            var service = CreateService(json: BuildJson("1", "9"));

            var result = await service.GetDistinctAgglomerations();

            Assert.Equal(2, result.Count);
            Assert.Equal("1", result[0].AgglomerationId);
            Assert.Equal("Northern Ireland", result[0].AgglomerationRegion);
            Assert.Equal("9", result[1].AgglomerationId);
            Assert.Equal("Greater London", result[1].AgglomerationRegion);
            VerifyLog(LogLevel.Information, Times.Once());
        }

        [Fact]
        public async Task GetDistinctAgglomerations_GroupsIdsSharingTheSameRegion()
        {
            var service = CreateService(json: BuildJson("7", "8"));

            var result = await service.GetDistinctAgglomerations();

            var item = Assert.Single(result);
            Assert.Equal("7,8", item.AgglomerationId);
            Assert.Equal("North East", item.AgglomerationRegion);
        }

        [Fact]
        public async Task GetDistinctAgglomerations_OrdersIdsNumericallyWithinGroup()
        {
            var service = CreateService(json: BuildJson("19", "3", "18", "16", "17"));

            var result = await service.GetDistinctAgglomerations();

            var item = Assert.Single(result);
            Assert.Equal("3,16,17,18,19", item.AgglomerationId);
            Assert.Equal("North West & Merseyside", item.AgglomerationRegion);
        }

        [Fact]
        public async Task GetDistinctAgglomerations_RemovesDuplicates_IgnoringWhitespace()
        {
            var service = CreateService(json: BuildJson("6", " 6 ", "6"));

            var result = await service.GetDistinctAgglomerations();

            var item = Assert.Single(result);
            Assert.Equal("6", item.AgglomerationId);
            Assert.Equal("Scotland", item.AgglomerationRegion);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetDistinctAgglomerations_SkipsNullOrWhitespaceIds(string? agglomerationId)
        {
            var service = CreateService(json: BuildJson(agglomerationId));

            var result = await service.GetDistinctAgglomerations();

            Assert.Empty(result);
            VerifyLog(LogLevel.Information, Times.Once());
        }

        private static readonly string[] ExpectedOrderedGroups = ["1", "2,14,15,24", "999,ABC"];

        [Fact]
        public async Task GetDistinctAgglomerations_OrdersGroupsNumerically_AndPlacesNonNumericLast()
        {
            var service = CreateService(json: BuildJson("24", "ABC", "2", "15", "999", "14", "1"));

            var result = await service.GetDistinctAgglomerations();

            Assert.Equal(ExpectedOrderedGroups, result.Select(a => a.AgglomerationId).ToArray());
        }

        [Fact]
        public async Task GetDistinctAgglomerations_ReturnsFallbackName_WhenIdIsUnknown()
        {
            var service = CreateService(json: BuildJson("999"));

            var result = await service.GetDistinctAgglomerations();

            var item = Assert.Single(result);
            Assert.Equal("999", item.AgglomerationId);
            Assert.Equal("Not found in provided mapping", item.AgglomerationRegion);
        }

        [Fact]
        public async Task GetDistinctAgglomerations_ReturnsEmptyList_WhenHttpResponseIsUnsuccessful()
        {
            var service = CreateService(json: "{}", statusCode: HttpStatusCode.InternalServerError);

            var result = await service.GetDistinctAgglomerations();

            Assert.Empty(result);
            VerifyLog(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task GetDistinctAgglomerations_ReturnsEmptyList_WhenHttpCallThrows()
        {
            var service = CreateService(httpException: new HttpRequestException("network down"));

            var result = await service.GetDistinctAgglomerations();

            Assert.Empty(result);
            VerifyLog(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task GetDistinctAgglomerations_ReturnsEmptyList_WhenPayloadIsInvalidJson()
        {
            var service = CreateService(json: "not-json");

            var result = await service.GetDistinctAgglomerations();

            Assert.Empty(result);
            VerifyLog(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task GetDistinctAgglomerations_ReturnsEmptyList_WhenTokenRetrievalThrows()
        {
            var service = CreateService(tokenException: new InvalidOperationException("token failure"));

            var result = await service.GetDistinctAgglomerations();

            Assert.Empty(result);
            _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
            VerifyLog(LogLevel.Error, Times.Once());
        }

        [Fact]
        public async Task GetDistinctAgglomerations_PassesBearerTokenToRicardoClient()
        {
            var service = CreateService(json: BuildJson("22"), token: "bearer-abc");

            var result = await service.GetDistinctAgglomerations();

            Assert.Single(result);
            _authServiceMock.Verify(a => a.GetRicardoToken(), Times.Once);
            _httpClientFactoryMock.Verify(x => x.CreateClient("RicardoNewAPI"), Times.Once);
        }
    }
}