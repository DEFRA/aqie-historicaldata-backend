
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;
using System;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AnnualAtomFeedExportCSVTests
    {
        private readonly Mock<ILogger<HourlyAtomFeedExportCSV>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly AnnualAtomFeedExportCSV _service;

        public AnnualAtomFeedExportCSVTests()
        {
            _loggerMock = new Mock<ILogger<HourlyAtomFeedExportCSV>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _service = new AnnualAtomFeedExportCSV(_loggerMock.Object, _httpClientFactoryMock.Object);
        }

        [Fact]
        public async Task ExportCSV_WithValidData_ReturnsByteArray()
        {
            var finalList = new List<Finaldata>
        {
            new Finaldata
            {
                ReportDate = DateTime.Now.Year.ToString(),
                AnnualPollutantname = "PM10",
                Total = 10,
                AnnualVerification = "1"
            }
        };

            var query = new querystringdata
            {
                stationreaddate = DateTime.Now.ToString(),
                region = "RegionX",
                siteType = "Urban",
                sitename = "SiteA",
                latitude = "51.5074",
                longitude = "-0.1278"
            };

            var result = await _service.annualatomfeedexport_csv(finalList, query);

            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        [Fact]
        public async Task ExportCSV_WithEmptyFinalList_ReturnsHeaderOnly()
        {
            var result = await _service.annualatomfeedexport_csv(new List<Finaldata>(), new querystringdata
            {
                stationreaddate = DateTime.Now.ToString(),
                region = "RegionX",
                siteType = "Urban",
                sitename = "SiteA",
                latitude = "51.5074",
                longitude = "-0.1278"
            });

            Assert.NotNull(result);
            Assert.Contains("Annual Average data from Defra", System.Text.Encoding.UTF8.GetString(result));
        }

        [Fact]
        public async Task ExportCSV_WithUnknownVerificationCode_ReturnsOthers()
        {
            var finalList = new List<Finaldata>
        {
            new Finaldata
            {
                ReportDate = "2023",
                AnnualPollutantname = "NO2",
                Total = 5,
                AnnualVerification = "9"
            }
        };

            var result = await _service.annualatomfeedexport_csv(finalList, new querystringdata
            {
                stationreaddate = "2023-01-01",
                region = "RegionX",
                siteType = "Urban",
                sitename = "SiteA",
                latitude = "51.5074",
                longitude = "-0.1278"
            });

            var csv = System.Text.Encoding.UTF8.GetString(result);
            Assert.Contains("others", csv);
        }

        [Fact]
        public async Task ExportCSV_WithZeroTotal_ReturnsNoData()
        {
            var finalList = new List<Finaldata>
        {
            new Finaldata
            {
                ReportDate = "2023",
                AnnualPollutantname = "O3",
                Total = 0,
                AnnualVerification = "2"
            }
        };

            var result = await _service.annualatomfeedexport_csv(finalList, new querystringdata
            {
                stationreaddate = "2023-01-01",
                region = "RegionX",
                siteType = "Urban",
                sitename = "SiteA",
                latitude = "51.5074",
                longitude = "-0.1278"
            });

            var csv = System.Text.Encoding.UTF8.GetString(result);
            Assert.Contains("no data", csv);
        }

        [Fact]
        public async Task ExportCSV_WithInvalidDate_LogsErrorAndReturnsFallback()
        {
            var result = await _service.annualatomfeedexport_csv(new List<Finaldata>(), new querystringdata
            {
                stationreaddate = "invalid-date",
                region = "RegionX",
                siteType = "Urban",
                sitename = "SiteA",
                latitude = "51.5074",
                longitude = "-0.1278"
            });

            Assert.Equal(new byte[] { 0x20 }, result);

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