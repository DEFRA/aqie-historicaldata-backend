using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Atomfeed.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System;
using System.Linq;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using AtomModel = AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class HourlyAtomFeedExportCSVTests
    {
        private readonly Mock<ILogger<HourlyAtomFeedExportCsv>> _mockLogger;
        private readonly HourlyAtomFeedExportCsv _service;

        public HourlyAtomFeedExportCSVTests()
        {
            _mockLogger = new Mock<ILogger<HourlyAtomFeedExportCsv>>();
            _service = new HourlyAtomFeedExportCsv(_mockLogger.Object);
        }

        private static QueryStringData GetSampleQueryData() => new()
        {
            SiteName = "Test Site",
            SiteType = "Urban",
            Region = "London",
            Latitude = "51.5074",
            Longitude = "-0.1278",
            StationReadDate = "2025-01-15 10:00:00"
        };

        private void VerifyErrorLogged()
        {
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
        public async Task ExportCsv_WritesHeaderMetadata()
        {
            var csv = Encoding.UTF8.GetString(
                await _service.hourlyatomfeedexport_csv(new List<AtomModel.FinalData>(), GetSampleQueryData()));

            Assert.Contains("Hourly data from Defra on", csv);
            Assert.Contains("Site Name,Test Site", csv);
            Assert.Contains("Site Type,Urban", csv);
            Assert.Contains("Region,London", csv);
            Assert.Contains("Latitude,51.5074", csv);
            Assert.Contains("Longitude,-0.1278", csv);
            Assert.Contains("Notes:,[1] All Data GMT hour ending", csv);
            Assert.Contains("Date,Time", csv);
        }

        [Fact]
        public async Task ExportCsv_ReturnsValidCsv_ForValidInput()
        {
            var finalList = new List<AtomModel.FinalData>
            {
                new() { StartTime = "2025-01-15 01:00:00", PollutantName = "PM10", Value = "12", Verification = "1" },
                new() { StartTime = "2025-01-15 01:00:00", PollutantName = "NO2", Value = "20", Verification = "2" }
            };

            var csv = Encoding.UTF8.GetString(
                await _service.hourlyatomfeedexport_csv(finalList, GetSampleQueryData()));

            Assert.Contains("PM10 particulate matter (Hourly measured)", csv);
            Assert.Contains("NO2,Status", csv);
            Assert.Contains("2025-01-15,01:00:00", csv);
            Assert.Contains("20,P", csv);
            Assert.Contains("12,V", csv);
        }

        [Fact]
        public async Task ExportCsv_UsesFriendlyHeader_ForPm25()
        {
            var finalList = new List<AtomModel.FinalData>
            {
                new() { StartTime = "2025-01-15 02:00:00", PollutantName = "PM2.5", Value = "8", Verification = "1" }
            };

            var csv = Encoding.UTF8.GetString(
                await _service.hourlyatomfeedexport_csv(finalList, GetSampleQueryData()));

            Assert.Contains("PM2.5 particulate matter (Hourly measured)", csv);
        }

        [Theory]
        [InlineData("1", "V")]
        [InlineData("2", "P")]
        [InlineData("3", "N")]
        [InlineData("9", "others")]
        [InlineData(null, "others")]
        public async Task ExportCsv_MapsVerificationCodes(string? code, string expected)
        {
            var finalList = new List<AtomModel.FinalData>
            {
                new() { StartTime = "2025-01-15 03:00:00", PollutantName = "CO", Value = "5", Verification = code }
            };

            var csv = Encoding.UTF8.GetString(
                await _service.hourlyatomfeedexport_csv(finalList, GetSampleQueryData()));

            Assert.Contains($"5,{expected}", csv);
        }

        [Fact]
        public async Task ExportCsv_ReplacesMinus99WithNoData()
        {
            var finalList = new List<AtomModel.FinalData>
            {
                new() { StartTime = "2025-01-15 04:00:00", PollutantName = "O3", Value = "-99", Verification = "1" }
            };

            var csv = Encoding.UTF8.GetString(
                await _service.hourlyatomfeedexport_csv(finalList, GetSampleQueryData()));

            Assert.Contains("no data,V", csv);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExportCsv_SkipsNullOrWhitespacePollutantNames(string? pollutantName)
        {
            var finalList = new List<AtomModel.FinalData>
            {
                new() { StartTime = "2025-01-15 05:00:00", PollutantName = pollutantName, Value = "1", Verification = "1" }
            };

            var csv = Encoding.UTF8.GetString(
                await _service.hourlyatomfeedexport_csv(finalList, GetSampleQueryData()));

            var columnHeaderLine = csv
                .Split('\n')
                .First(l => l.StartsWith("Date,Time", StringComparison.Ordinal))
                .TrimEnd('\r');

            Assert.Equal("Date,Time", columnHeaderLine);
            Assert.Contains("2025-01-15,05:00:00", csv);
        }

        [Fact]
        public async Task ExportCsv_WritesEmptyValues_WhenPollutantMissingForRow()
        {
            var finalList = new List<AtomModel.FinalData>
            {
                new() { StartTime = "2025-01-15 06:00:00", PollutantName = "NO2", Value = "10", Verification = "1" },
                new() { StartTime = "2025-01-15 07:00:00", PollutantName = "O3", Value = "30", Verification = "3" }
            };

            var csv = Encoding.UTF8.GetString(
                await _service.hourlyatomfeedexport_csv(finalList, GetSampleQueryData()));

            // Columns are ordered alphabetically: NO2, O3
            Assert.Contains("2025-01-15,06:00:00,10,V,,", csv);
            Assert.Contains("2025-01-15,07:00:00,,,30,N", csv);
        }

        [Fact]
        public async Task ExportCsv_GroupsRowsByDateAndTime()
        {
            var finalList = new List<AtomModel.FinalData>
            {
                new() { StartTime = "2025-01-15 08:00:00", PollutantName = "NO2", Value = "1", Verification = "1" },
                new() { StartTime = "2025-01-15 08:00:00", PollutantName = "O3", Value = "2", Verification = "2" },
                new() { StartTime = "2025-01-16 08:00:00", PollutantName = "NO2", Value = "3", Verification = "3" }
            };

            var csv = Encoding.UTF8.GetString(
                await _service.hourlyatomfeedexport_csv(finalList, GetSampleQueryData()));

            var dataRows = csv
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.StartsWith("2025-01-1", StringComparison.Ordinal))
                .ToList();

            Assert.Equal(2, dataRows.Count);
            Assert.Equal("2025-01-15,08:00:00,1,V,2,P", dataRows[0]);
            Assert.Equal("2025-01-16,08:00:00,3,N,,", dataRows[1]);
        }

        [Fact]
        public async Task ExportCsv_ReturnsFallbackByteArray_OnInvalidStationReadDate()
        {
            var data = GetSampleQueryData();
            data.StationReadDate = "not-a-date";

            var result = await _service.hourlyatomfeedexport_csv(new List<AtomModel.FinalData>(), data);

            Assert.Equal(new byte[] { 0x20 }, result);
            VerifyErrorLogged();
        }

        [Fact]
        public async Task ExportCsv_ReturnsFallbackByteArray_OnInvalidStartTime()
        {
            var finalList = new List<AtomModel.FinalData>
            {
                new() { StartTime = "bad-start-time", PollutantName = "NO2", Value = "1", Verification = "1" }
            };

            var result = await _service.hourlyatomfeedexport_csv(finalList, GetSampleQueryData());

            Assert.Equal(new byte[] { 0x20 }, result);
            VerifyErrorLogged();
        }

        [Fact]
        public async Task ExportCsv_ReturnsFallbackByteArray_OnNullFinalList()
        {
            var result = await _service.hourlyatomfeedexport_csv(null!, GetSampleQueryData());

            Assert.Equal(new byte[] { 0x20 }, result);
            VerifyErrorLogged();
        }
    }
}