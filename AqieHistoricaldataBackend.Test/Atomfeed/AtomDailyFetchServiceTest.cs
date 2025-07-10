
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AtomModel = AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

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
    public async Task GetAtomDailydatafetch_ReturnsAverage_WhenValidData()
    {
        var input = new List<AtomModel.FinalData>
        {
            new AtomModel.FinalData { StartTime = "2025-07-01T01:00:00", PollutantName = "NO2", Verification = "Verified", Value = "10" },
            new AtomModel.FinalData { StartTime = "2025-07-01T02:00:00", PollutantName = "NO2", Verification = "Verified", Value = "20" },
            new AtomModel.FinalData { StartTime = "2025-07-01T03:00:00", PollutantName = "NO2", Verification = "Verified", Value = "-99" },
            new AtomModel.FinalData { StartTime = "2025-07-01T04:00:00", PollutantName = "NO2", Verification = "Verified", Value = "30" }
        };

        var result = await _service.GetAtomDailydatafetch(input, new AtomModel.QueryStringData());

        Assert.Single(result);
        Assert.Equal(20, result[0].Total);
    }

    [Fact]
    public async Task GetAtomDailydatafetch_ReturnsZero_WhenInsufficientValidData()
    {
        var input = new List<AtomModel.FinalData>
        {
            new AtomModel.FinalData { StartTime = "2025-07-01T01:00:00", PollutantName = "NO2", Verification = "Verified", Value = "-99" },
            new AtomModel.FinalData { StartTime = "2025-07-01T02:00:00", PollutantName = "NO2", Verification = "Verified", Value = "20" }
        };

        var result = await _service.GetAtomDailydatafetch(input, new AtomModel.QueryStringData());

        Assert.Single(result);
        Assert.Equal(0, result[0].Total);
    }

    [Fact]
    public async Task GetAtomDailydatafetch_ReturnsZero_WhenAllInvalidValues()
    {
        var input = new List<AtomModel.FinalData>
        {
            new AtomModel.FinalData { StartTime = "2025-07-01T01:00:00", PollutantName = "NO2", Verification = "Verified", Value = "-99" },
            new AtomModel.FinalData { StartTime = "2025-07-01T02:00:00", PollutantName = "NO2", Verification = "Verified", Value = "-99" }
        };

        var result = await _service.GetAtomDailydatafetch(input, new AtomModel.QueryStringData());

        Assert.Single(result);
        Assert.Equal(0, result[0].Total);
    }

    [Fact]
    public async Task GetAtomDailydatafetch_ReturnsEmpty_WhenInputIsEmpty()
    {
        var result = await _service.GetAtomDailydatafetch(new List<AtomModel.FinalData>(), new AtomModel.QueryStringData());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAtomDailydatafetch_HandlesException_Gracefully()
    {
        var input = new List<AtomModel.FinalData>
        {
            new AtomModel.FinalData { StartTime = "invalid-date", PollutantName = "NO2", Verification = "Verified", Value = "10" }
        };

        var result = await _service.GetAtomDailydatafetch(input, new AtomModel.QueryStringData());

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
