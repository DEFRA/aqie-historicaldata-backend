using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Tests.Services
{
    public class AtomAnnualFetchServiceTests
    {
        private readonly Mock<ILogger<AtomAnnualFetchService>> _mockLogger;
        private readonly Mock<IAtomDailyFetchService> _mockDailyFetchService;
        private readonly AtomAnnualFetchService _service;

        public AtomAnnualFetchServiceTests()
        {
            _mockLogger = new Mock<ILogger<AtomAnnualFetchService>>();
            _mockDailyFetchService = new Mock<IAtomDailyFetchService>();
            _service = new AtomAnnualFetchService(_mockLogger.Object, _mockDailyFetchService.Object);
        }

        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsAnnualAverages_WhenValidDataProvided()
        {
            // Arrange
            var input = new List<Finaldata>
            {
                new Finaldata { ReportDate = "2025-01-01", DailyPollutantname = "NO2", DailyVerification = "Verified", Total = 10 },
                new Finaldata { ReportDate = "2025-01-02", DailyPollutantname = "NO2", DailyVerification = "Verified", Total = 20 }
            };

            _mockDailyFetchService.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<querystringdata>()))
                .Returns(input);

            // Act
            var result = await _service.GetAtomAnnualdatafetch(input, new querystringdata());

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
            // Arrange
            var input = new List<Finaldata>();

            _mockDailyFetchService.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<querystringdata>()))
                .Returns(input);

            // Act
            var result = await _service.GetAtomAnnualdatafetch(input, new querystringdata());

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsZeroAverage_WhenAllTotalsAreZero()
        {
            // Arrange
            var input = new List<Finaldata>
            {
                new Finaldata { ReportDate = "2025-01-01", DailyPollutantname = "O3", DailyVerification = "Unverified", Total = 0 },
                new Finaldata { ReportDate = "2025-01-02", DailyPollutantname = "O3", DailyVerification = "Unverified", Total = 0 }
            };

            _mockDailyFetchService.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<querystringdata>()))
                .Returns(input);

            // Act
            var result = await _service.GetAtomAnnualdatafetch(input, new querystringdata());

            // Assert
            Assert.Single(result);
            Assert.Equal(0, Convert.ToDecimal(result[0].Total));
        }

        [Fact]
        public async Task GetAtomAnnualdatafetch_IgnoresZeroTotalsInAverage()
        {
            // Arrange
            var input = new List<Finaldata>
            {
                new Finaldata { ReportDate = "2025-01-01", DailyPollutantname = "PM10", DailyVerification = "Verified", Total = 0 },
                new Finaldata { ReportDate = "2025-01-02", DailyPollutantname = "PM10", DailyVerification = "Verified", Total = 30 }
            };

            _mockDailyFetchService.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<querystringdata>()))
                .Returns(input);

            // Act
            var result = await _service.GetAtomAnnualdatafetch(input, new querystringdata());

            // Assert
            Assert.Single(result);
            Assert.Equal(30, Convert.ToDecimal(result[0].Total));
        }

        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsEmptyList_WhenExceptionThrown()
        {
            // Arrange
            var input = new List<Finaldata>
            {
                new Finaldata { ReportDate = "InvalidDate", DailyPollutantname = "CO", DailyVerification = "Verified", Total = 10 }
            };

            _mockDailyFetchService.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<querystringdata>()))
                .Throws(new Exception("Simulated failure"));

            // Act
            var result = await _service.GetAtomAnnualdatafetch(input, new querystringdata());

            // Assert
            Assert.Empty(result);
            _mockLogger.Verify(
 x => x.Log(
 LogLevel.Error,
 It.IsAny<EventId>(),
 It.Is<It.IsAnyType>((v, t) => true),
 It.IsAny<Exception>(),
 It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
 Times.AtLeastOnce);
        }
    }
}
