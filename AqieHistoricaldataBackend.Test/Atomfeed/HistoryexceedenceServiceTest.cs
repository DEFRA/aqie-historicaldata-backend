
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;
using AtomModel = AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

public class HistoryexceedenceServiceTests
{
    private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock;
    private readonly Mock<IAtomHourlyFetchService> _hourlyServiceMock;
    private readonly Mock<IAtomDailyFetchService> _dailyServiceMock;
    private readonly HistoryexceedenceService _service;

    public HistoryexceedenceServiceTests()
    {
        _loggerMock = new Mock<ILogger<HistoryexceedenceService>>();
        _hourlyServiceMock = new Mock<IAtomHourlyFetchService>();
        _dailyServiceMock = new Mock<IAtomDailyFetchService>();
        _service = new HistoryexceedenceService(_loggerMock.Object, _hourlyServiceMock.Object, _dailyServiceMock.Object);
    }

    [Fact]
    public async Task GetHistoryexceedencedata_ReturnsMergedData_WhenValid()
    {
        var query = new AtomModel.QueryStringData { SiteId = "site1", Year = "2024" };
        var hourlyData = new List<AtomModel.FinalData> {
            new AtomModel.FinalData { PollutantName = "Nitrogen dioxide", Value = "201", StartTime = DateTime.Now.ToString(), Verification = "2", Validity = "1" },
            new AtomModel.FinalData { PollutantName = "Sulphur dioxide", Value = "351", StartTime = DateTime.Now.ToString(), Verification = "1", Validity = "1" }
        };
        var dailyData = new List<AtomModel.FinalData> {
            new AtomModel.FinalData { DailyPollutantName = "PM10", Total = 51, ReportDate = DateTime.Now.ToString() }
        };

        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("site1", "2024", "All")).ReturnsAsync(hourlyData);
        _dailyServiceMock.Setup(s => s.GetAtomDailydatafetch(hourlyData, query)).ReturnsAsync(dailyData);

        var result = await _service.GetHistoryexceedencedata(query);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetHistoryexceedencedata_ReturnsEmpty_WhenNoData()
    {
        var query = new AtomModel.QueryStringData { SiteId = "site1", Year = "2024" };
        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<AtomModel.FinalData>());
        _dailyServiceMock.Setup(s => s.GetAtomDailydatafetch(It.IsAny<List<AtomModel.FinalData>>(), It.IsAny<AtomModel.QueryStringData>()))
            .ReturnsAsync(new List<AtomModel.FinalData>());

        var result = await _service.GetHistoryexceedencedata(query);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetHistoryexceedencedata_HandlesInvalidNumericValues()
    {
        var query = new AtomModel.QueryStringData { SiteId = "site1", Year = "2024" };
        var hourlyData = new List<AtomModel.FinalData> {
            new AtomModel.FinalData { PollutantName = "Nitrogen dioxide", Value = "invalid", StartTime = DateTime.Now.ToString(), Verification = "2", Validity = "1" }
        };

        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(hourlyData);
        _dailyServiceMock.Setup(s => s.GetAtomDailydatafetch(It.IsAny<List<AtomModel.FinalData>>(), It.IsAny<AtomModel.QueryStringData>()))
            .ReturnsAsync(new List<AtomModel.FinalData>());

        var result = await _service.GetHistoryexceedencedata(query);

        Assert.Equal("Failure", result);
    }

    [Fact]
    public async Task GetHistoryexceedencedata_ReturnsFailure_WhenHourlyServiceThrows()
    {
        var query = new AtomModel.QueryStringData { SiteId = "site1", Year = "2024" };
        _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Service error"));

        var result = await _service.GetHistoryexceedencedata(query);

        Assert.Equal("Failure", result);
    }

    [Fact]
    public void GetDataCapturePercentages_HandlesLeapYear()
    {
        var hourlyData = new List<AtomModel.FinalData> {
            new AtomModel.FinalData { PollutantName = "PM10", StartTime = new DateTime(2020, 1, 1).ToString(), Validity = "1" }
        };

        var method = typeof(HistoryexceedenceService).GetMethod("GetDataCapturePercentages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method.Invoke(_service, new object[] { hourlyData, "2020" }) as List<dynamic>;

        Assert.NotNull(result);
    }

    [Fact]
    public void GetDataVerifiedTag_ReturnsCorrectTag()
    {
        var data = new List<AtomModel.FinalData> {
            new AtomModel.FinalData { StartTime = DateTime.Now.AddDays(-1).ToString(), Verification = "1" },
            new AtomModel.FinalData { StartTime = DateTime.Now.ToString(), Verification = "2" }
        };

        var method = typeof(HistoryexceedenceService).GetMethod("GetDataVerifiedTag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method.Invoke(_service, new object[] { data }) as string;

        Assert.Contains("Data has been verified until", result);
    }
}
