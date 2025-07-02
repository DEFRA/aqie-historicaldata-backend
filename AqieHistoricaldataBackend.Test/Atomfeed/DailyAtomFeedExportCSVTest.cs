using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;
using System.Collections.Generic;
using System.Text;
using System;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class DailyAtomFeedExportCSVTests
    {
        private readonly Mock<ILogger<HourlyAtomFeedExportCSV>> _mockLogger;
        private readonly DailyAtomFeedExportCSV _service;

        public DailyAtomFeedExportCSVTests()
        {
            _mockLogger = new Mock<ILogger<HourlyAtomFeedExportCSV>>();
            _service = new DailyAtomFeedExportCSV(_mockLogger.Object);
        }

        [Fact]
        public void ExportCSV_WithValidData_ReturnsExpectedCSV()
        {
            var finalList = new List<Finaldata>
        {
            new Finaldata
            {
                ReportDate = "2025-03-20T06:00:00Z",
                DailyPollutantname = "PM10",
                Total = 10,
                DailyVerification = "1"
            },
            new Finaldata
            {
                ReportDate = "2025-03-20T06:00:00Z",
                DailyPollutantname = "NO2",
                Total = 0,
                DailyVerification = "3"
            }
        };

            var queryData = new querystringdata
            {
                stationreaddate = "2025-03-20T06:05:20.893Z",
                region = "London",
                siteType = "Urban",
                sitename = "Site A",
                latitude = "51.5074",
                longitude = "-0.1278"
            };

            var result = _service.dailyatomfeedexport_csv(finalList, queryData);
            var csv = Encoding.UTF8.GetString(result);

            Assert.Contains("PM10 particulate matter", csv);
            Assert.Contains("NO2", csv);
            Assert.Contains("no data", csv);
            Assert.Contains("V", csv);
            Assert.Contains("N", csv);
        }

        [Fact]
        public void ExportCSV_WithEmptyFinalList_ReturnsCSVWithHeadersOnly()
        {
            var result = _service.dailyatomfeedexport_csv(new List<Finaldata>(), new querystringdata
            {
                stationreaddate = "2025-03-20T06:05:20.893Z",
                region = "London",
                siteType = "Urban",
                sitename = "Site A",
                latitude = "51.5074",
                longitude = "-0.1278"
            });

            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("Daily data from Defra", csv);
            Assert.Contains("Date", csv);
        }

        [Fact]
        public void ExportCSV_WithInvalidDate_LogsErrorAndReturnsFallback()
        {
            var result = _service.dailyatomfeedexport_csv(new List<Finaldata>(), new querystringdata
            {
                stationreaddate = "invalid-date",
                region = "London",
                siteType = "Urban",
                sitename = "Site A",
                latitude = "51.5074",
                longitude = "-0.1278"
            });

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
        public void ExportCSV_WithUnknownVerificationCode_ReturnsOthers()
        {
            var finalList = new List<Finaldata>
        {
            new Finaldata
            {
                ReportDate = "2025-03-20T06:00:00Z",
                DailyPollutantname = "O3",
                Total = 5,
                DailyVerification = "9"
            }
        };

            var queryData = new querystringdata
            {
                stationreaddate = "2025-03-20T06:05:20.893Z",
                region = "London",
                siteType = "Urban",
                sitename = "Site A",
                latitude = "51.5074",
                longitude = "-0.1278"
            };

            var result = _service.dailyatomfeedexport_csv(finalList, queryData);
            var csv = Encoding.UTF8.GetString(result);

            Assert.Contains("others", csv);
        }
    }
}