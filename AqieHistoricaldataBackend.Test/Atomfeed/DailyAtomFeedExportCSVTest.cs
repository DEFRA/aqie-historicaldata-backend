using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

public class DailyAtomFeedExportCSVTests
{
    private readonly Mock<ILogger<HourlyAtomFeedExportCSV>> _mockLogger;
    private readonly DailyAtomFeedExportCSV _service;

    public DailyAtomFeedExportCSVTests()
    {
        _mockLogger = new Mock<ILogger<HourlyAtomFeedExportCSV>>();
        _service = new DailyAtomFeedExportCSV(_mockLogger.Object);
    }

    private querystringdata GetSampleQueryData() => new()
    {
        stationreaddate = DateTime.UtcNow.ToString(),
        sitename = "Test Site",
        siteType = "Urban",
        region = "London",
        latitude = "51.5074",
        longitude = "-0.1278"
    };

    [Fact]
    public void ExportCSV_ReturnsValidCSV_ForValidInput()
    {
        var data = GetSampleQueryData();
        var finalList = new List<Finaldata>
        {
            new() { ReportDate = DateTime.UtcNow.ToString(), DailyPollutantname = "PM10", Total = 10, DailyVerification = "1" },
            new() { ReportDate = DateTime.UtcNow.ToString(), DailyPollutantname = "NO2", Total = 20, DailyVerification = "2" }
        };

        var result = _service.dailyatomfeedexport_csv(finalList, data);
        var csv = Encoding.UTF8.GetString(result);

        Assert.Contains("PM10 particulate matter", csv);
        Assert.Contains("NO2", csv);
        Assert.Contains("V", csv);
        Assert.Contains("P", csv);
    }

    [Fact]
    public void ExportCSV_HandlesEmptyFinalList()
    {
        var data = GetSampleQueryData();
        var result = _service.dailyatomfeedexport_csv(new List<Finaldata>(), data);
        var csv = Encoding.UTF8.GetString(result);

        Assert.Contains("Daily data from Defra", csv);
        Assert.Contains("Date", csv); // Only headers
    }

    [Fact]
    public void ExportCSV_HandlesTotalZero_AsNoData()
    {
        var data = GetSampleQueryData();
        var finalList = new List<Finaldata>
        {
            new() { ReportDate = DateTime.UtcNow.ToString(), DailyPollutantname = "O3", Total = 0, DailyVerification = "3" }
        };

        var result = _service.dailyatomfeedexport_csv(finalList, data);
        var csv = Encoding.UTF8.GetString(result);

        Assert.Contains("no data", csv);
        Assert.Contains("N", csv);
    }

    [Fact]
    public void ExportCSV_HandlesUnknownVerification()
    {
        var data = GetSampleQueryData();
        var finalList = new List<Finaldata>
        {
            new() { ReportDate = DateTime.UtcNow.ToString(), DailyPollutantname = "CO", Total = 5, DailyVerification = "X" }
        };

        var result = _service.dailyatomfeedexport_csv(finalList, data);
        var csv = Encoding.UTF8.GetString(result);

        Assert.Contains("others", csv);
    }

    [Fact]
    public void ExportCSV_ReturnsFallbackByte_OnException()
    {
        var faultyService = new DailyAtomFeedExportCSV(_mockLogger.Object);

        var result = faultyService.dailyatomfeedexport_csv(null, null);

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
