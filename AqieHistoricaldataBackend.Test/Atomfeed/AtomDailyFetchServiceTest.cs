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
    public class AtomDailyFetchServiceTests
    {
        private readonly Mock<ILogger<AtomDailyFetchService>> _mockLogger;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly AtomDailyFetchService _service;

        public AtomDailyFetchServiceTests()
        {
            _mockLogger = new Mock<ILogger<AtomDailyFetchService>>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _service = new AtomDailyFetchService(_mockLogger.Object, _mockHttpClientFactory.Object);
        }

        [Fact]
        public async Task GetAtomDailydatafetch_ReturnsCorrectAverage_WhenValidData()
        {
            var input = new List<Finaldata>
        {
            new Finaldata { StartTime = "2025-06-25T01:00:00", Pollutantname = "NO2", Verification = "Verified", Value = "10" },
            new Finaldata { StartTime = "2025-06-25T02:00:00", Pollutantname = "NO2", Verification = "Verified", Value = "20" },
            new Finaldata { StartTime = "2025-06-25T03:00:00", Pollutantname = "NO2", Verification = "Verified", Value = "30" },
        };

            var result = await _service.GetAtomDailydatafetch(input, new querystringdata());

            Assert.Single(result);
            Assert.Equal("NO2", result[0].DailyPollutantname);
            Assert.Equal(20, result[0].Total);
        }

        [Fact]
        public async Task GetAtomDailydatafetch_ExcludesInvalidValues_AndCalculatesAverage()
        {
            var input = new List<Finaldata>
        {
            new Finaldata { StartTime = "2025-06-25T01:00:00", Pollutantname = "PM10", Verification = "Verified", Value = "-99" },
            new Finaldata { StartTime = "2025-06-25T02:00:00", Pollutantname = "PM10", Verification = "Verified", Value = "50" },
            new Finaldata { StartTime = "2025-06-25T03:00:00", Pollutantname = "PM10", Verification = "Verified", Value = "70" },
            new Finaldata { StartTime = "2025-06-25T04:00:00", Pollutantname = "PM10", Verification = "Verified", Value = "80" },
        };

            var result = await _service.GetAtomDailydatafetch(input, new querystringdata());

            Assert.Single(result);
            Assert.InRange(result[0].Total, 66.66m, 66.68m); // Average = 66.67
        }

        [Fact]
        public async Task GetAtomDailydatafetch_ReturnsZero_WhenValidValuesBelowThreshold()
        {
            var input = new List<Finaldata>
        {
            new Finaldata { StartTime = "2025-06-25T01:00:00", Pollutantname = "O3", Verification = "Verified", Value = "-99" },
            new Finaldata { StartTime = "2025-06-25T02:00:00", Pollutantname = "O3", Verification = "Verified", Value = "10" },
            new Finaldata { StartTime = "2025-06-25T03:00:00", Pollutantname = "O3", Verification = "Verified", Value = "-99" },
            new Finaldata { StartTime = "2025-06-25T04:00:00", Pollutantname = "O3", Verification = "Verified", Value = "-99" },
        };

            var result = await _service.GetAtomDailydatafetch(input, new querystringdata());

            Assert.Single(result);
            Assert.Equal(0, result[0].Total);
        }

        [Fact]
        public async Task GetAtomDailydatafetch_ReturnsEmptyList_WhenInputIsEmpty()
        {
            var result = await _service.GetAtomDailydatafetch(new List<Finaldata>(), new querystringdata());

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAtomDailydatafetch_HandlesException_AndReturnsEmptyList()
        {
            var input = new List<Finaldata>
        {
            new Finaldata { StartTime = "invalid-date", Pollutantname = "CO", Verification = "Verified", Value = "10" }
        };

            var result = await _service.GetAtomDailydatafetch(input, new querystringdata());

            Assert.Empty(result);
            // _mockLogger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce);
            _mockLogger.Verify(
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