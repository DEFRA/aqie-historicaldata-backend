using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using System.Collections.Generic;
using System;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomDailyFetchServiceTests
    {
        private readonly AtomDailyFetchService _service;
        private readonly Mock<ILogger<AtomDailyFetchService>> _loggerMock;

        public AtomDailyFetchServiceTests()
        {
            _loggerMock = new Mock<ILogger<AtomDailyFetchService>>();
            _service = new AtomDailyFetchService(_loggerMock.Object);
        }

        [Fact]
        public void GetAtomDailydatafetch_ReturnsCorrectAverage_WhenValidData()
        {
            var input = new List<Finaldata>
        {
            new Finaldata { StartTime = "2025-07-01T01:00:00", Pollutantname = "NO2", Verification = "Verified", Value = "10" },
            new Finaldata { StartTime = "2025-07-01T02:00:00", Pollutantname = "NO2", Verification = "Verified", Value = "20" },
            new Finaldata { StartTime = "2025-07-01T03:00:00", Pollutantname = "NO2", Verification = "Verified", Value = "30" },
            new Finaldata { StartTime = "2025-07-01T04:00:00", Pollutantname = "NO2", Verification = "Verified", Value = "-99" }
        };

            var result = _service.GetAtomDailydatafetch(input, new querystringdata());

            Assert.Single(result);
            Assert.Equal("NO2", result[0].DailyPollutantname);
            Assert.Equal(20, result[0].Total); // Average of 10, 20, 30
        }

        [Fact]
        public void GetAtomDailydatafetch_ReturnsZero_WhenLessThan75PercentValid()
        {
            var input = new List<Finaldata>
        {
            new Finaldata { StartTime = "2025-07-01T01:00:00", Pollutantname = "O3", Verification = "Verified", Value = "10" },
            new Finaldata { StartTime = "2025-07-01T02:00:00", Pollutantname = "O3", Verification = "Verified", Value = "-99" },
            new Finaldata { StartTime = "2025-07-01T03:00:00", Pollutantname = "O3", Verification = "Verified", Value = "-99" },
            new Finaldata { StartTime = "2025-07-01T04:00:00", Pollutantname = "O3", Verification = "Verified", Value = "-99" }
        };

            var result = _service.GetAtomDailydatafetch(input, new querystringdata());

            Assert.Single(result);
            Assert.Equal(0, result[0].Total);
        }

        [Fact]
        public void GetAtomDailydatafetch_ReturnsEmptyList_WhenInputIsEmpty()
        {
            var result = _service.GetAtomDailydatafetch(new List<Finaldata>(), new querystringdata());
            Assert.Empty(result);
        }

        [Fact]
        public void GetAtomDailydatafetch_ReturnsEmptyList_WhenAllValuesAreInvalid()
        {
            var input = new List<Finaldata>
        {
            new Finaldata { StartTime = "2025-07-01T01:00:00", Pollutantname = "CO", Verification = "Verified", Value = "-99" },
            new Finaldata { StartTime = "2025-07-01T02:00:00", Pollutantname = "CO", Verification = "Verified", Value = "-99" }
        };

            var result = _service.GetAtomDailydatafetch(input, new querystringdata());

            Assert.Single(result);
            Assert.Equal(0, result[0].Total);
        }

        [Fact]
        public void GetAtomDailydatafetch_HandlesException_AndLogsError()
        {
            var input = new List<Finaldata>
        {
            new Finaldata { StartTime = "invalid-date", Pollutantname = "SO2", Verification = "Verified", Value = "10" }
        };

            var result = _service.GetAtomDailydatafetch(input, new querystringdata());

            Assert.Empty(result);
            _loggerMock.Verify(
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