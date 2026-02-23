using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using AtomModel = AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    /// <summary>
    /// Focused unit tests for <see cref="AtomHistoryService.GetHistoryexceedencedata"/>
    /// targeting 100% branch and line coverage for that method.
    /// </summary>
    public class AtomHistoryService_GetHistoryexceedencedata_Tests
    {
        // ── Shared mocks ─────────────────────────────────────────────────────

        private readonly Mock<ILogger<AtomHistoryService>> _loggerMock = new();
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
        private readonly Mock<IAtomHourlyFetchService> _hourlyServiceMock = new();
        private readonly Mock<IAtomDailyFetchService> _dailyServiceMock = new();
        private readonly Mock<IAtomAnnualFetchService> _annualServiceMock = new();
        private readonly Mock<IAWSS3BucketService> _s3ServiceMock = new();
        private readonly Mock<IAtomDataSelectionService> _dataSelectionServiceMock = new();
        private readonly Mock<IAtomDataSelectionJobStatus> _dataSelectionJobStatusMock = new();
        private readonly Mock<IAtomDataSelectionEmailJobService> _dataSelectionEmailJobServiceMock = new();
        private readonly Mock<IHistoryexceedenceService> _exceedenceServiceMock = new();

        // ── Factory helper ────────────────────────────────────────────────────

        private AtomHistoryService CreateService()
        {
            // Provide a stub HttpClient so the factory never throws for unrelated calls
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok")
                });

            _httpClientFactoryMock
                .Setup(f => f.CreateClient("Atomfeed"))
                .Returns(new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") });

            return new AtomHistoryService(
                _loggerMock.Object,
                _httpClientFactoryMock.Object,
                _hourlyServiceMock.Object,
                _dailyServiceMock.Object,
                _annualServiceMock.Object,
                _s3ServiceMock.Object,
                _dataSelectionServiceMock.Object,
                _dataSelectionJobStatusMock.Object,
                _dataSelectionEmailJobServiceMock.Object,
                _exceedenceServiceMock.Object);
        }

        // ── Happy-path: returns whatever the inner service returns ────────────

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsServiceResult_WhenSuccessful()
        {
            var data = new QueryStringData { SiteId = "ABC1", Year = "2023" };
            var expected = new { PollutantName = "NO2", hourlyCount = "2" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ReturnsAsync((object)expected); // For anonymous types or objects

            var service = CreateService();
            dynamic result = await service.GetHistoryexceedencedata(data);

            Assert.Equal(expected, (object)result);
        }

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsStringResult_WhenServiceReturnsString()
        {
            var data = new QueryStringData { SiteId = "ABC1", Year = "2023" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ReturnsAsync((object)"some-string-result"); // For string results

            var service = CreateService();
            var result = await service.GetHistoryexceedencedata(data);

            Assert.Equal("some-string-result", (string)result);
        }

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsNullResult_WhenServiceReturnsNull()
        {
            var data = new QueryStringData { SiteId = "ABC1", Year = "2023" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ReturnsAsync((object?)null); // For null results

            var service = CreateService();
            var result = await service.GetHistoryexceedencedata(data);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsListResult_WhenServiceReturnsList()
        {
            var data = new QueryStringData { SiteId = "SITE2", Year = "2022" };
            var expected = new System.Collections.Generic.List<dynamic>
            {
                new { PollutantName = "PM10", hourlyCount = "0" },
                new { PollutantName = "Ozone", hourlyCount = "n/a" }
            };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ReturnsAsync((object)expected); // For lists or other objects

            var service = CreateService();
            dynamic result = await service.GetHistoryexceedencedata(data);

            Assert.Equal(2, ((System.Collections.Generic.List<dynamic>)result).Count);
        }

        // ── Passes through the exact QueryStringData instance ─────────────────

        [Fact]
        public async Task GetHistoryexceedencedata_PassesDataToInnerService()
        {
            var data = new QueryStringData { SiteId = "VERIFY_SITE", Year = "2021" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ReturnsAsync("ok");

            var service = CreateService();
            await service.GetHistoryexceedencedata(data);

            _exceedenceServiceMock.Verify(s => s.GetHistoryexceedencedata(data), Times.Once);
        }

        // ── Exception branch: returns "Failure" ───────────────────────────────

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsFailure_WhenExceptionIsThrown()
        {
            var data = new QueryStringData { SiteId = "ERR", Year = "2023" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ThrowsAsync(new Exception("inner failure"));

            var service = CreateService();
            var result = await service.GetHistoryexceedencedata(data);

            Assert.Equal("Failure", (string)result);
        }

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsFailure_WhenInvalidOperationExceptionIsThrown()
        {
            var data = new QueryStringData { SiteId = "ERR2", Year = "2023" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ThrowsAsync(new InvalidOperationException("invalid op"));

            var service = CreateService();
            var result = await service.GetHistoryexceedencedata(data);

            Assert.Equal("Failure", (string)result);
        }

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsFailure_WhenHttpRequestExceptionIsThrown()
        {
            var data = new QueryStringData { SiteId = "HTTP_ERR", Year = "2023" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ThrowsAsync(new HttpRequestException("http error"));

            var service = CreateService();
            var result = await service.GetHistoryexceedencedata(data);

            Assert.Equal("Failure", (string)result);
        }

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsFailure_WhenTaskCanceledExceptionIsThrown()
        {
            var data = new QueryStringData { SiteId = "TIMEOUT", Year = "2023" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ThrowsAsync(new TaskCanceledException("timed out"));

            var service = CreateService();
            var result = await service.GetHistoryexceedencedata(data);

            Assert.Equal("Failure", (string)result);
        }

        // ── Exception branch: logger is called with both Message and StackTrace ─

        [Fact]
        public async Task GetHistoryexceedencedata_LogsErrorMessage_WhenExceptionIsThrown()
        {
            var data = new QueryStringData { SiteId = "LOG_ERR", Year = "2023" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ThrowsAsync(new Exception("log-test-error"));

            var service = CreateService();
            await service.GetHistoryexceedencedata(data);

            // The service logs two separate LogError calls (Message + StackTrace)
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("log-test-error")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetHistoryexceedencedata_LogsExactlyTwice_WhenExceptionIsThrown()
        {
            var data = new QueryStringData { SiteId = "LOG_COUNT", Year = "2023" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ThrowsAsync(new Exception("double-log"));

            var service = CreateService();
            await service.GetHistoryexceedencedata(data);

            // One log for ex.Message, one for ex.StackTrace
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task GetHistoryexceedencedata_DoesNotLog_WhenSuccessful()
        {
            var data = new QueryStringData { SiteId = "NO_LOG", Year = "2023" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ReturnsAsync("ok");

            var service = CreateService();
            await service.GetHistoryexceedencedata(data);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        // ── Null / minimal QueryStringData inputs ─────────────────────────────

        [Fact]
        public async Task GetHistoryexceedencedata_HandlesEmptySiteId_WhenSuccessful()
        {
            var data = new QueryStringData { SiteId = string.Empty, Year = "2023" };

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ReturnsAsync("empty-site");

            var service = CreateService();
            var result = await service.GetHistoryexceedencedata(data);

            Assert.Equal("empty-site", (string)result);
        }

        [Fact]
        public async Task GetHistoryexceedencedata_HandlesNullProperties_WhenSuccessful()
        {
            var data = new QueryStringData(); // all properties null

            _exceedenceServiceMock
                .Setup(s => s.GetHistoryexceedencedata(data))
                .ReturnsAsync("null-props");

            var service = CreateService();
            var result = await service.GetHistoryexceedencedata(data);

            Assert.Equal("null-props", (string)result);
        }
    }
}