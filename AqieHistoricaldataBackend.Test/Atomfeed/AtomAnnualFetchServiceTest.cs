using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomAnnualFetchServiceTests
    {
        private readonly Mock<ILogger<AtomAnnualFetchService>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IAtomDailyFetchService> _dailyFetchServiceMock;
        private readonly AtomAnnualFetchService _service;

        public AtomAnnualFetchServiceTests()
        {
            _loggerMock = new Mock<ILogger<AtomAnnualFetchService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _dailyFetchServiceMock = new Mock<IAtomDailyFetchService>();
            _service = new AtomAnnualFetchService(_loggerMock.Object, _httpClientFactoryMock.Object, _dailyFetchServiceMock.Object);
        }

        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsAnnualAverage_WhenDataIsValid()
        {
            // Arrange
            var input = new List<Finaldata> { new Finaldata { ReportDate = "2025-01-01", Total = 10 } };
            var query = new querystringdata();

            var dailyData = new List<Finaldata>
        {
            new Finaldata { ReportDate = "2025-01-01", DailyPollutantname = "NO2", DailyVerification = "Verified", Total = 10 },
            new Finaldata { ReportDate = "2025-02-01", DailyPollutantname = "NO2", DailyVerification = "Verified", Total = 20 }
        };

            _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, query)).ReturnsAsync(dailyData);

            // Act
            var result = await _service.GetAtomAnnualdatafetch(input, query);

            // Assert
            Assert.Single(result);
            Assert.Equal("2025", result[0].ReportDate);
            Assert.Equal("NO2", result[0].AnnualPollutantname);
            Assert.Equal("Verified", result[0].AnnualVerification);
            Assert.Equal(15, Convert.ToDecimal(result[0].Total));
        }

        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsEmptyList_WhenInputIsEmpty()
        {
            var input = new List<Finaldata>();
            var query = new querystringdata();

            _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, query)).ReturnsAsync(new List<Finaldata>());

            var result = await _service.GetAtomAnnualdatafetch(input, query);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsZeroAverage_WhenAllTotalsAreZero()
        {
            var input = new List<Finaldata>();
            var query = new querystringdata();

            var dailyData = new List<Finaldata>
        {
            new Finaldata { ReportDate = "2025-01-01", DailyPollutantname = "SO2", DailyVerification = "Verified", Total = 0 },
            new Finaldata { ReportDate = "2025-02-01", DailyPollutantname = "SO2", DailyVerification = "Verified", Total = 0 }
        };

            _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, query)).ReturnsAsync(dailyData);

            var result = await _service.GetAtomAnnualdatafetch(input, query);

            Assert.Single(result);
            Assert.Equal(0, Convert.ToDecimal(result[0].Total));
        }

        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsCorrectAverage_WhenMixedZeroAndNonZeroTotals()
        {
            var input = new List<Finaldata>();
            var query = new querystringdata();

            var dailyData = new List<Finaldata>
        {
            new Finaldata { ReportDate = "2025-01-01", DailyPollutantname = "CO", DailyVerification = "Verified", Total = 0 },
            new Finaldata { ReportDate = "2025-02-01", DailyPollutantname = "CO", DailyVerification = "Verified", Total = 30 }
        };

            _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, query)).ReturnsAsync(dailyData);

            var result = await _service.GetAtomAnnualdatafetch(input, query);

            Assert.Single(result);
            Assert.Equal(30, Convert.ToDecimal(result[0].Total));
        }

        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsEmptyList_OnException()
        {
            var input = new List<Finaldata>();
            var query = new querystringdata();

            _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, query)).ThrowsAsync(new Exception("Service error"));

            var result = await _service.GetAtomAnnualdatafetch(input, query);

            Assert.Empty(result);

            _loggerMock.Verify(
                                 x => x.Log(
                                 LogLevel.Error,
                                 It.IsAny<EventId>(),
                                 It.Is<It.IsAnyType>((v, t) => true),
                                 It.IsAny<Exception>(),
                                 It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                                 Times.AtLeastOnce);
        }
    }
}