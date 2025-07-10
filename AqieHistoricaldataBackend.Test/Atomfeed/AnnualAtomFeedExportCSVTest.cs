
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AqieHistoricaldataBackend.Atomfeed.Services;

// Mocked model classes
namespace AqieHistoricaldataBackend.Atomfeed.Models
{
    public class FinalData
    {
        public string ReportDate { get; set; }
        public string AnnualPollutantname { get; set; }
        public double Total { get; set; }
        public string AnnualVerification { get; set; }
        public string DailyPollutantname { get; set; }
        public string DailyVerification { get; set; }
    }

    public class pivotpollutant
    {
        public string date { get; set; }
        public List<SubpollutantItem> Subpollutant { get; set; }
    }

    public class SubpollutantItem
    {
        public string pollutantname { get; set; }
        public string pollutantvalue { get; set; }
        public string verification { get; set; }
    }

    public class querystringdata
    {
        public string stationreaddate { get; set; }
        public string sitename { get; set; }
        public string siteType { get; set; }
        public string region { get; set; }
        public string latitude { get; set; }
        public string longitude { get; set; }
    }
}

namespace AqieHistoricaldataBackend.Tests
{
    using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

    public class AnnualAtomFeedExportCSVTests
    {
        private readonly Mock<ILogger<HourlyAtomFeedExportCSV>> _mockLogger;
        private readonly AnnualAtomFeedExportCSV _service;

        public AnnualAtomFeedExportCSVTests()
        {
            _mockLogger = new Mock<ILogger<HourlyAtomFeedExportCSV>>();
            _service = new AnnualAtomFeedExportCSV(_mockLogger.Object);
        }

        private QueryStringData GetSampleQueryData() => new()
        {
            StationReadDate = "2024-01-01",
            SiteName = "Test Site",
            SiteType = "Urban",
            Region = "London",
            Latitude = "51.5074",
            Longitude = "-0.1278"
        };

        [Fact]
        public async Task ExportCsv_WithValidData_ReturnsNonEmptyByteArray()
        {
            var finalList = new List<FinalData>
            {
                new FinalData { ReportDate = "2023", AnnualPollutantName = "PM10", Total = 10, AnnualVerification = "1" },
                new FinalData { ReportDate = "2023", AnnualPollutantName = "PM2.5", Total = 20, AnnualVerification = "2" }
            };

            var result = await _service.annualatomfeedexport_csv(finalList, GetSampleQueryData());

            Assert.NotNull(result);
            Assert.NotEmpty(result);
            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("PM10 particulate matter", csv);
            Assert.Contains("V", csv);
            Assert.Contains("P", csv);
        }

        [Fact]
        public async Task ExportCsv_WithEmptyFinalList_ReturnsHeaderOnly()
        {
            var result = await _service.annualatomfeedexport_csv(new List<FinalData>(), GetSampleQueryData());
            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("Annual Average data from Defra", csv);
            Assert.Contains("Date", csv);
        }

        [Fact]
        public async Task ExportCsv_WithUnknownPollutant_UsesRawName()
        {
            var finalList = new List<FinalData>
            {
                new FinalData { ReportDate = "2023", AnnualPollutantName = "O3", Total = 15, AnnualVerification = "1" }
            };

            var result = await _service.annualatomfeedexport_csv(finalList, GetSampleQueryData());
            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("O3", csv);
        }

        [Fact]
        public async Task ExportCsv_WithUnknownVerificationCode_UsesOthers()
        {
            var finalList = new List<FinalData>
            {
                new FinalData { ReportDate = "2023", AnnualPollutantName = "PM10", Total = 10, AnnualVerification = "9" }
            };

            var result = await _service.annualatomfeedexport_csv(finalList, GetSampleQueryData());
            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("others", csv);
        }

        [Fact]
        public async Task ExportCsv_WithZeroTotal_ReturnsNoData()
        {
            var finalList = new List<FinalData>
            {
                new FinalData { ReportDate = "2023", AnnualPollutantName = "PM10", Total = 0, AnnualVerification = "1" }
            };

            var result = await _service.annualatomfeedexport_csv(finalList, GetSampleQueryData());
            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("no data", csv);
        }

        [Fact]
        public async Task ExportCsv_WithCurrentYear_UsesTodayAsEndDate()
        {
            var currentYear = DateTime.Now.Year.ToString();
            var finalList = new List<FinalData>
            {
                new FinalData { ReportDate = currentYear, AnnualPollutantName = "PM10", Total = 10, AnnualVerification = "1" }
            };

            var result = await _service.annualatomfeedexport_csv(finalList, GetSampleQueryData());
            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains(DateTime.Now.ToString("dd/MM/yyyy"), csv);
        }

        [Fact]
        public async Task ExportCsv_WithInvalidDate_LogsErrorAndReturnsFallback()
        {
            var data = GetSampleQueryData();
            data.StationReadDate = "invalid-date";

            var result = await _service.annualatomfeedexport_csv(new List<FinalData>(), data);

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
        public async Task ExportCsv_WithNullFieldsInFinalData_HandlesGracefully()
        {
            var finalList = new List<FinalData>
        {
            new FinalData { ReportDate = "2023", AnnualPollutantName = null, Total = 0, AnnualVerification = null }
        };

            var result = await _service.annualatomfeedexport_csv(finalList, GetSampleQueryData());
            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("no data", csv);
            Assert.Contains("others", csv);
        }


        [Fact]
        public async Task ExportCsv_WithNullQueryFields_StillGeneratesCsv()
        {
            var data = new QueryStringData
            {
                StationReadDate = DateTime.Now.ToString(),
                SiteName = null,
                SiteType = null,
                Region = null,
                Latitude = null,
                Longitude = null
            };

            var result = await _service.annualatomfeedexport_csv(new List<FinalData>(), data);
            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("Site Name,", csv);
            Assert.Contains("Site Type,", csv);
        }

        [Fact]
        public async Task ExportCsv_WithMultipleYears_GroupsByYear()
        {
            var finalList = new List<FinalData>
            {
                new FinalData { ReportDate = "2022", AnnualPollutantName = "PM10", Total = 10, AnnualVerification = "1" },
                new FinalData { ReportDate = "2023", AnnualPollutantName = "PM10", Total = 20, AnnualVerification = "2" }
            };

            var result = await _service.annualatomfeedexport_csv(finalList, GetSampleQueryData());
            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("01/01/2022 to 31/12/2022", csv);
            Assert.Contains("01/01/2023 to", csv);
        }

        [Fact]
        public async Task ExportCsv_WithDuplicatePollutants_UsesDistinctHeaders()
        {
            var finalList = new List<FinalData>
            {
                new FinalData { ReportDate = "2023", AnnualPollutantName = "PM10", Total = 10, AnnualVerification = "1" },
                new FinalData { ReportDate = "2023", AnnualPollutantName = "PM10", Total = 15, AnnualVerification = "2" }
            };

            var result = await _service.annualatomfeedexport_csv(finalList, GetSampleQueryData());
            var csv = Encoding.UTF8.GetString(result);
            Assert.Equal(1, csv.Split("PM10 particulate matter").Length - 1);
        }

        [Fact]
        public async Task ExportCsv_WithSpecialCharacterPollutant_EncodesCorrectly()
        {
            var finalList = new List<FinalData>
            {
                new FinalData { ReportDate = "2023", AnnualPollutantName = "NO₂", Total = 25, AnnualVerification = "1" }
            };

            var result = await _service.annualatomfeedexport_csv(finalList, GetSampleQueryData());
            var csv = Encoding.UTF8.GetString(result);
            Assert.Contains("NO₂", csv);
        }

        [Fact]
        public async Task ExportCsv_WithLargeDataset_ContainsExpectedPollutants()
        {
            var finalList = new List<FinalData>();
            for (int i = 0; i < 1000; i++)
            {
                finalList.Add(new FinalData
                {
                    ReportDate = "2023",
                    AnnualPollutantName = $"Pollutant{i % 10}",
                    Total = i,
                    AnnualVerification = (i % 3 + 1).ToString()
                });
            }

            var result = await _service.annualatomfeedexport_csv(finalList, GetSampleQueryData());
            var csv = Encoding.UTF8.GetString(result);

            for (int i = 0; i < 10; i++)
            {
                Assert.Contains($"Pollutant{i}", csv);
            }
        }

    }
}
