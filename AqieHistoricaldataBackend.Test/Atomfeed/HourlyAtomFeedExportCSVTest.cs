using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;
using System;
using System.Collections.Generic;
using System.Text;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class HourlyAtomFeedExportCSVTests
    {
        private readonly Mock<ILogger<HourlyAtomFeedExportCSV>> _mockLogger;
        private readonly HourlyAtomFeedExportCSV _service;

        public HourlyAtomFeedExportCSVTests()
        {
            _mockLogger = new Mock<ILogger<HourlyAtomFeedExportCSV>>();
            _service = new HourlyAtomFeedExportCSV(_mockLogger.Object);
        }

        [Fact]
        public void ExportCSV_WithValidData_ReturnsByteArray()
        {
            var finalList = new List<Finaldata>
        {
            new Finaldata
            {
                StartTime = "2025-03-20T06:00:00Z",
                Pollutantname = "NO2",
                Value = "12.5",
                Verification = "1"
            },
            new Finaldata
            {
                StartTime = "2025-03-20T06:00:00Z",
                Pollutantname = "PM10",
                Value = "-99",
                Verification = "3"
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

            var result = _service.hourlyatomfeedexport_csv(finalList, queryData);

            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("NO2", csv);
            Assert.Contains("no data", csv);
            Assert.Contains("V", csv);
            Assert.Contains("N", csv);
        }

        [Fact]
        public void ExportCSV_WithEmptyFinalList_ReturnsCSVWithHeadersOnly()
        {
            var result = _service.hourlyatomfeedexport_csv(new List<Finaldata>(), new querystringdata
            {
                stationreaddate = "2025-03-20T06:05:20.893Z",
                region = "London",
                siteType = "Urban",
                sitename = "Site A",
                latitude = "51.5074",
                longitude = "-0.1278"
            });

            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("Hourly data from Defra", csv);
            Assert.DoesNotContain("NO2", csv);
        }

        [Fact]
        public void ExportCSV_WithInvalidDate_LogsErrorAndReturnsFallback()
        {
            var result = _service.hourlyatomfeedexport_csv(new List<Finaldata>(), new querystringdata
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
                StartTime = "2025-03-20T06:00:00Z",
                Pollutantname = "O3",
                Value = "15",
                Verification = "9"
            }
        };

            var result = _service.hourlyatomfeedexport_csv(finalList, new querystringdata
            {
                stationreaddate = "2025-03-20T06:05:20.893Z",
                region = "London",
                siteType = "Urban",
                sitename = "Site A",
                latitude = "51.5074",
                longitude = "-0.1278"
            });

            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("others", csv);
        }
    }
}