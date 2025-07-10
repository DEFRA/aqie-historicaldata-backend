
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

public class AtomAnnualFetchServiceTests
{
    private readonly Mock<ILogger<AtomAnnualFetchService>> _loggerMock;
    private readonly Mock<IAtomDailyFetchService> _dailyFetchServiceMock;
    private readonly AtomAnnualFetchService _service;

    public AtomAnnualFetchServiceTests()
    {
        _loggerMock = new Mock<ILogger<AtomAnnualFetchService>>();
        _dailyFetchServiceMock = new Mock<IAtomDailyFetchService>();
        _service = new AtomAnnualFetchService(_loggerMock.Object, _dailyFetchServiceMock.Object);
    }

    [Fact]
    public async Task GetAtomAnnualdatafetch_ReturnsAnnualAverage_WhenDataIsValid()
    {
        var input = new List<FinalData>
        {
            new FinalData { ReportDate = "2024-01-01", Total = 10, DailyPollutantName = "NO2", DailyVerification = "Verified" },
            new FinalData { ReportDate = "2024-01-02", Total = 20, DailyPollutantName = "NO2", DailyVerification = "Verified" }
        };

        _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
            .ReturnsAsync(input);

        var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

        Assert.Single(result);
        Assert.Equal("2024", result[0].ReportDate);
        Assert.Equal("NO2", result[0].AnnualPollutantName);
        Assert.Equal("Verified", result[0].AnnualVerification);
        Assert.Equal(15, Convert.ToDecimal(result[0].Total));
    }

    [Fact]
    public async Task GetAtomAnnualdatafetch_ReturnsEmptyList_WhenInputIsEmpty()
    {
        var input = new List<FinalData>();

        _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
            .ReturnsAsync(input);

        var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAtomAnnualdatafetch_ReturnsZeroAverage_WhenAllTotalsAreZero()
    {
        var input = new List<FinalData>
        {
            new FinalData { ReportDate = "2024-01-01", Total = 0, DailyPollutantName = "O3", DailyVerification = "Unverified" },
            new FinalData { ReportDate = "2024-01-02", Total = 0, DailyPollutantName = "O3", DailyVerification = "Unverified" }
        };

        _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
            .ReturnsAsync(input);

        var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

        Assert.Single(result);
        Assert.Equal(0, Convert.ToDecimal(result[0].Total));
    }

    [Fact]
    public async Task GetAtomAnnualdatafetch_HandlesMixedZeroAndNonZeroTotals()
    {
        var input = new List<FinalData>
        {
            new FinalData { ReportDate = "2024-01-01", Total = 0, DailyPollutantName = "PM2.5", DailyVerification = "Verified" },
            new FinalData { ReportDate = "2024-01-02", Total = 30, DailyPollutantName = "PM2.5", DailyVerification = "Verified" }
        };

        _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
            .ReturnsAsync(input);

        var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

        Assert.Single(result);
        Assert.Equal(30, Convert.ToDecimal(result[0].Total));
    }

    [Fact]
    public async Task GetAtomAnnualdatafetch_ReturnsEmptyList_OnException()
    {
        var input = new List<FinalData>
        {
            new FinalData { ReportDate = "InvalidDate", Total = 10, DailyPollutantName = "CO", DailyVerification = "Verified" }
        };

        _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
            .ThrowsAsync(new Exception("Test exception"));

        var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

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
