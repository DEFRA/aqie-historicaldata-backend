
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using AtomModel = AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

public class HourlyAtomFeedExportCSVTests
{
    private readonly Mock<ILogger<HourlyAtomFeedExportCSV>> _mockLogger;
    private readonly HourlyAtomFeedExportCSV _service;

    public HourlyAtomFeedExportCSVTests()
    {
        _mockLogger = new Mock<ILogger<HourlyAtomFeedExportCSV>>();
        _service = new HourlyAtomFeedExportCSV(_mockLogger.Object);
    }

    private AtomModel.querystringdata GetSampleQueryData() => new()
    {
        sitename = "Test Site",
        siteType = "Urban",
        region = "London",
        latitude = "51.5074",
        longitude = "-0.1278",
        stationreaddate = DateTime.UtcNow.ToString()
    };

    [Fact]
    public async Task ExportCsv_ReturnsValidCsv_ForValidInput()
    {
        var finalList = new List<AtomModel.Finaldata>
        {
            new AtomModel.Finaldata { StartTime = DateTime.UtcNow.ToString(), Pollutantname = "PM10", Value = "12", Verification = "1" },
            new AtomModel.Finaldata { StartTime = DateTime.UtcNow.ToString(), Pollutantname = "NO2", Value = "20", Verification = "2" }
        };

        var result = await _service.hourlyatomfeedexport_csv(finalList, GetSampleQueryData());

        var csv = Encoding.UTF8.GetString(result);
        Assert.Contains("PM10 particulate matter", csv);
        Assert.Contains("NO2", csv);
        Assert.Contains("V", csv);
        Assert.Contains("P", csv);
    }

    [Fact]
    public async Task ExportCsv_HandlesEmptyFinalList()
    {
        var result = await _service.hourlyatomfeedexport_csv(new List<AtomModel.Finaldata>(), GetSampleQueryData());

        var csv = Encoding.UTF8.GetString(result);
        Assert.Contains("Hourly data from Defra", csv);
        Assert.Contains("Date,Time", csv);
    }

    [Fact]
    public async Task ExportCsv_HandlesInvalidStationReadDate()
    {
        var data = GetSampleQueryData();
        data.stationreaddate = "invalid-date";

        var result = await _service.hourlyatomfeedexport_csv(new List<AtomModel.Finaldata>(), data);

        Assert.Equal(new byte[] { 0x20 }, result);
        _mockLogger.Verify(
                     x => x.Log(
                     LogLevel.Error,
                     It.IsAny<EventId>(),
                     It.Is<It.IsAnyType>((v, t) => true),
                     It.IsAny<Exception>(),
                     It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                     Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExportCsv_ReplacesMinus99WithNoData()
    {
        var finalList = new List<AtomModel.Finaldata>
        {
            new AtomModel.Finaldata { StartTime = DateTime.UtcNow.ToString(), Pollutantname = "O3", Value = "-99", Verification = "1" }
        };

        var result = await _service.hourlyatomfeedexport_csv(finalList, GetSampleQueryData());

        var csv = Encoding.UTF8.GetString(result);
        Assert.Contains("no data", csv);
    }

    [Fact]
    public async Task ExportCsv_HandlesUnknownVerificationCode()
    {
        var finalList = new List<AtomModel.Finaldata>
        {
            new AtomModel.Finaldata { StartTime = DateTime.UtcNow.ToString(), Pollutantname = "CO", Value = "5", Verification = "9" }
        };

        var result = await _service.hourlyatomfeedexport_csv(finalList, GetSampleQueryData());

        var csv = Encoding.UTF8.GetString(result);
        Assert.Contains("others", csv);
    }

    [Fact]
    public async Task ExportCsv_ReturnsFallbackByteArray_OnException()
    {
        var data = GetSampleQueryData();
        data.stationreaddate = "not-a-date"; // Force FormatException

        var result = await _service.hourlyatomfeedexport_csv(new List<AtomModel.Finaldata>(), data);

        Assert.Equal(new byte[] { 0x20 }, result);
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
