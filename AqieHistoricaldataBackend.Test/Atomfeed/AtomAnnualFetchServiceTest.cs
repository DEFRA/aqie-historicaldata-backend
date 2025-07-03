
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
        var input = new List<Finaldata>
        {
            new Finaldata { ReportDate = "2024-01-01", Total = 10, DailyPollutantname = "NO2", DailyVerification = "Verified" },
            new Finaldata { ReportDate = "2024-01-02", Total = 20, DailyPollutantname = "NO2", DailyVerification = "Verified" }
        };

        _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<querystringdata>()))
            .ReturnsAsync(input);

        var result = await _service.GetAtomAnnualdatafetch(input, new querystringdata());

        Assert.Single(result);
        Assert.Equal("2024", result[0].ReportDate);
        Assert.Equal("NO2", result[0].AnnualPollutantname);
        Assert.Equal("Verified", result[0].AnnualVerification);
        Assert.Equal(15, Convert.ToDecimal(result[0].Total));
    }

    [Fact]
    public async Task GetAtomAnnualdatafetch_ReturnsEmptyList_WhenInputIsEmpty()
    {
        var input = new List<Finaldata>();

        _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<querystringdata>()))
            .ReturnsAsync(input);

        var result = await _service.GetAtomAnnualdatafetch(input, new querystringdata());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAtomAnnualdatafetch_ReturnsZeroAverage_WhenAllTotalsAreZero()
    {
        var input = new List<Finaldata>
        {
            new Finaldata { ReportDate = "2024-01-01", Total = 0, DailyPollutantname = "O3", DailyVerification = "Unverified" },
            new Finaldata { ReportDate = "2024-01-02", Total = 0, DailyPollutantname = "O3", DailyVerification = "Unverified" }
        };

        _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<querystringdata>()))
            .ReturnsAsync(input);

        var result = await _service.GetAtomAnnualdatafetch(input, new querystringdata());

        Assert.Single(result);
        Assert.Equal(0, Convert.ToDecimal(result[0].Total));
    }

    [Fact]
    public async Task GetAtomAnnualdatafetch_HandlesMixedZeroAndNonZeroTotals()
    {
        var input = new List<Finaldata>
        {
            new Finaldata { ReportDate = "2024-01-01", Total = 0, DailyPollutantname = "PM2.5", DailyVerification = "Verified" },
            new Finaldata { ReportDate = "2024-01-02", Total = 30, DailyPollutantname = "PM2.5", DailyVerification = "Verified" }
        };

        _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<querystringdata>()))
            .ReturnsAsync(input);

        var result = await _service.GetAtomAnnualdatafetch(input, new querystringdata());

        Assert.Single(result);
        Assert.Equal(30, Convert.ToDecimal(result[0].Total));
    }

    [Fact]
    public async Task GetAtomAnnualdatafetch_ReturnsEmptyList_OnException()
    {
        var input = new List<Finaldata>
        {
            new Finaldata { ReportDate = "InvalidDate", Total = 10, DailyPollutantname = "CO", DailyVerification = "Verified" }
        };

        _dailyFetchServiceMock.Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<querystringdata>()))
            .ThrowsAsync(new Exception("Test exception"));

        var result = await _service.GetAtomAnnualdatafetch(input, new querystringdata());

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
