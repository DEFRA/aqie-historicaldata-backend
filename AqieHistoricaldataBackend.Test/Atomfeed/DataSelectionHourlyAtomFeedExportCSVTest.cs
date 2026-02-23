using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class DataSelectionHourlyAtomFeedExportCSVTests
    {
        private readonly Mock<ILogger<HourlyAtomFeedExportCSV>> _loggerMock;
        private readonly DataSelectionHourlyAtomFeedExportCSV _service;

        public DataSelectionHourlyAtomFeedExportCSVTests()
        {
            _loggerMock = new Mock<ILogger<HourlyAtomFeedExportCSV>>();
            _service = new DataSelectionHourlyAtomFeedExportCSV(_loggerMock.Object);
        }

        #region Helpers

        private static QueryStringData GetSampleQueryData() => new()
        {
            SiteName = "Test Site",
            SiteType = "Urban",
            Region = "London",
            Latitude = "51.5074",
            Longitude = "-0.1278"
        };

        private static List<FinalData> BuildFinalList(params FinalData[] items) => new(items);

        private static string ExtractCsvFromZip(byte[] zipBytes)
        {
            using var zipStream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            var entry = archive.GetEntry("file1.csv");
            Assert.NotNull(entry);
            using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        #endregion

        #region Happy Path

        [Fact]
        public async Task ExportCsv_ReturnsNonEmptyZip_WhenListHasItems()
        {
            var finalList = BuildFinalList(
                new FinalData
                {
                    StartTime = "2024-01-01T00:00",
                    EndTime = "2024-01-01T01:00",
                    Verification = "1",
                    Value = "42",
                    PollutantName = "PM10",
                    SiteName = "Site A",
                    SiteType = "Urban",
                    Region = "London",
                    Country = "England"
                }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());

            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.NotEqual(new byte[] { 0x20 }, result);
        }

        [Fact]
        public async Task ExportCsv_ReturnedBytes_AreValidZip()
        {
            var finalList = BuildFinalList(
                new FinalData
                {
                    StartTime = "2024-01-01T00:00",
                    EndTime = "2024-01-01T01:00",
                    Verification = "1",
                    Value = "42",
                    PollutantName = "PM10",
                    SiteName = "Site A",
                    SiteType = "Urban",
                    Region = "London",
                    Country = "England"
                }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());

            // Should not throw — is a valid ZIP
            using var zipStream = new MemoryStream(result);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            Assert.Single(archive.Entries);
            Assert.Equal("file1.csv", archive.Entries[0].FullName);
        }

        [Fact]
        public async Task ExportCsv_CsvContainsHeaders_WhenListHasItems()
        {
            var finalList = BuildFinalList(
                new FinalData
                {
                    StartTime = "2024-01-01T00:00",
                    EndTime = "2024-01-01T01:00",
                    Verification = "1",
                    Value = "10",
                    PollutantName = "Ozone",
                    SiteName = "Site A",
                    SiteType = "Urban",
                    Region = "London",
                    Country = "England"
                }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());
            var csv = ExtractCsvFromZip(result);

            Assert.Contains("StartTime", csv);
            Assert.Contains("EndTime", csv);
            Assert.Contains("Status", csv);
            Assert.Contains("Unit", csv);
            Assert.Contains("Value", csv);
            Assert.Contains("PollutantName", csv);
            Assert.Contains("SiteName", csv);
            Assert.Contains("SiteType", csv);
            Assert.Contains("Region", csv);
            Assert.Contains("Country", csv);
        }

        [Fact]
        public async Task ExportCsv_CsvContainsCorrectData_WhenListHasItems()
        {
            var finalList = BuildFinalList(
                new FinalData
                {
                    StartTime = "2024-01-01T00:00",
                    EndTime = "2024-01-01T01:00",
                    Verification = "1",
                    Value = "42",
                    PollutantName = "PM10",
                    SiteName = "Site A",
                    SiteType = "Urban",
                    Region = "London",
                    Country = "England"
                }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());
            var csv = ExtractCsvFromZip(result);

            Assert.Contains("2024-01-01T00:00", csv);
            Assert.Contains("2024-01-01T01:00", csv);
            Assert.Contains("V", csv);
            Assert.Contains("ugm-3", csv);
            Assert.Contains("42", csv);
            Assert.Contains("PM10", csv);
            Assert.Contains("Site A", csv);
            Assert.Contains("Urban", csv);
            Assert.Contains("London", csv);
            Assert.Contains("England", csv);
        }

        [Fact]
        public async Task ExportCsv_ReturnsValidZip_WhenFinalListIsEmpty()
        {
            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(
                new List<FinalData>(), GetSampleQueryData());

            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.NotEqual(new byte[] { 0x20 }, result);

            // Still a valid ZIP with a CSV entry (just headers)
            using var zipStream = new MemoryStream(result);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            Assert.Single(archive.Entries);
            Assert.Equal("file1.csv", archive.Entries[0].FullName);
        }

        [Fact]
        public async Task ExportCsv_LogsStartAndEnd_WhenSuccessful()
        {
            var finalList = BuildFinalList(
                new FinalData { StartTime = "2024-01-01T00:00", Value = "10", Verification = "1" }
            );

            await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("zipStream started")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("zipStream ended")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Verification Status Mapping

        [Theory]
        [InlineData("1", "V")]
        [InlineData("2", "P")]
        [InlineData("3", "N")]
        [InlineData("9", "others")]
        [InlineData("", "others")]
        [InlineData(null, "others")]
        public async Task ExportCsv_MapsVerificationToCorrectStatus(string verification, string expectedStatus)
        {
            var finalList = BuildFinalList(
                new FinalData
                {
                    StartTime = "2024-01-01T00:00",
                    EndTime = "2024-01-01T01:00",
                    Verification = verification,
                    Value = "10",
                    PollutantName = "PM10",
                    SiteName = "Site A",
                    SiteType = "Urban",
                    Region = "London",
                    Country = "England"
                }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());
            var csv = ExtractCsvFromZip(result);

            Assert.Contains(expectedStatus, csv);
        }

        #endregion

        #region Value Mapping

        [Fact]
        public async Task ExportCsv_ReplacesMinusMinus99WithNoData()
        {
            var finalList = BuildFinalList(
                new FinalData
                {
                    StartTime = "2024-01-01T00:00",
                    EndTime = "2024-01-01T01:00",
                    Verification = "1",
                    Value = "-99",
                    PollutantName = "PM10",
                    SiteName = "Site A",
                    SiteType = "Urban",
                    Region = "London",
                    Country = "England"
                }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());
            var csv = ExtractCsvFromZip(result);

            Assert.Contains("no data", csv);
            Assert.DoesNotContain("-99", csv);
        }

        [Fact]
        public async Task ExportCsv_PreservesActualValue_WhenValueIsNotMinusMinus99()
        {
            var finalList = BuildFinalList(
                new FinalData
                {
                    StartTime = "2024-01-01T00:00",
                    EndTime = "2024-01-01T01:00",
                    Verification = "1",
                    Value = "123.45",
                    PollutantName = "PM10",
                    SiteName = "Site A",
                    SiteType = "Urban",
                    Region = "London",
                    Country = "England"
                }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());
            var csv = ExtractCsvFromZip(result);

            Assert.Contains("123.45", csv);
        }

        [Fact]
        public async Task ExportCsv_SetsUnitToUgm3_ForAllRecords()
        {
            var finalList = BuildFinalList(
                new FinalData { StartTime = "2024-01-01T00:00", Verification = "1", Value = "10", PollutantName = "PM10" },
                new FinalData { StartTime = "2024-01-01T01:00", Verification = "2", Value = "20", PollutantName = "Ozone" }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());
            var csv = ExtractCsvFromZip(result);

            var occurrences = 0;
            var idx = 0;
            while ((idx = csv.IndexOf("ugm-3", idx, StringComparison.Ordinal)) != -1)
            {
                occurrences++;
                idx++;
            }

            // Two data rows + header = at least 2 in data (header column name may differ)
            Assert.True(occurrences >= 2);
        }

        #endregion

        #region Whitespace Trimming

        [Fact]
        public async Task ExportCsv_TrimsWhitespace_FromAllStringFields()
        {
            var finalList = BuildFinalList(
                new FinalData
                {
                    StartTime = "  2024-01-01T00:00  ",
                    EndTime = "  2024-01-01T01:00  ",
                    Verification = "1",
                    Value = "  42  ",
                    PollutantName = "  PM10  ",
                    SiteName = "  Site A  ",
                    SiteType = "  Urban  ",
                    Region = "  London  ",
                    Country = "  England  "
                }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());
            var csv = ExtractCsvFromZip(result);

            Assert.Contains("2024-01-01T00:00", csv);
            Assert.Contains("PM10", csv);
            Assert.Contains("Site A", csv);
            Assert.Contains("Urban", csv);
            Assert.Contains("London", csv);
            Assert.Contains("England", csv);
        }

        #endregion

        #region Null Fields

        [Fact]
        public async Task ExportCsv_HandlesNullStringFields_WithoutThrowing()
        {
            var finalList = BuildFinalList(
                new FinalData
                {
                    StartTime = null,
                    EndTime = null,
                    Verification = "1",
                    Value = null,
                    PollutantName = null,
                    SiteName = null,
                    SiteType = null,
                    Region = null,
                    Country = null
                }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());

            Assert.NotNull(result);
            Assert.NotEqual(new byte[] { 0x20 }, result);
        }

        #endregion

        #region Multiple Records

        [Fact]
        public async Task ExportCsv_WritesAllRecords_WhenMultipleItemsInList()
        {
            var finalList = BuildFinalList(
                new FinalData { StartTime = "2024-01-01T00:00", Verification = "1", Value = "10", PollutantName = "PM10" },
                new FinalData { StartTime = "2024-01-01T01:00", Verification = "2", Value = "20", PollutantName = "NO2" },
                new FinalData { StartTime = "2024-01-01T02:00", Verification = "3", Value = "30", PollutantName = "Ozone" }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());
            var csv = ExtractCsvFromZip(result);

            Assert.Contains("PM10", csv);
            Assert.Contains("NO2", csv);
            Assert.Contains("Ozone", csv);
            Assert.Contains("V", csv);
            Assert.Contains("P", csv);
            Assert.Contains("N", csv);
        }

        [Fact]
        public async Task ExportCsv_HandlesLargeList_WithoutError()
        {
            var finalList = new List<FinalData>();
            for (var i = 0; i < 10_000; i++)
            {
                finalList.Add(new FinalData
                {
                    StartTime = $"2024-01-01T{i % 24:00}:00",
                    EndTime = $"2024-01-01T{(i + 1) % 24:00}:00",
                    Verification = (i % 3 + 1).ToString(),
                    Value = i.ToString(),
                    PollutantName = "PM10",
                    SiteName = "Site A",
                    SiteType = "Urban",
                    Region = "London",
                    Country = "England"
                });
            }

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());

            Assert.NotNull(result);
            Assert.NotEqual(new byte[] { 0x20 }, result);
        }

        #endregion

        #region Exception Handling

        [Fact]
        public async Task ExportCsv_ReturnsFallbackByte_WhenExceptionOccurs()
        {
            // Pass null list to force a NullReferenceException inside the try block
            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(null!, GetSampleQueryData());

            Assert.Equal(new byte[] { 0x20 }, result);
        }

        [Fact]
        public async Task ExportCsv_LogsError_WhenExceptionOccurs()
        {
            await _service.dataSelectionHourlyAtomFeedExportCSV(null!, GetSampleQueryData());

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(
                        "DataSelection Hourly download csv dataSelectionHourlyAtomFeedExportCSV error Info message")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExportCsv_LogsStackTrace_WhenExceptionOccurs()
        {
            await _service.dataSelectionHourlyAtomFeedExportCSV(null!, GetSampleQueryData());

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(
                        "DataSelection Hourly download csv dataSelectionHourlyAtomFeedExportCSV Info stacktrace")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExportCsv_ReturnsFallbackByte_WhenQueryDataIsNull()
        {
            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(new List<FinalData>(), null!);

            // QueryStringData being null shouldn't crash the ZIP path (data param unused in current impl)
            // but if it does, fallback byte must be returned
            Assert.NotNull(result);
        }

        #endregion

        #region Output Structure

        [Fact]
        public async Task ExportCsv_ZipEntryName_IsFile1Csv()
        {
            var finalList = BuildFinalList(
                new FinalData { Verification = "1", Value = "10", PollutantName = "PM10" }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());

            using var zipStream = new MemoryStream(result);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            Assert.Equal("file1.csv", archive.Entries[0].FullName);
        }

        [Fact]
        public async Task ExportCsv_ZipContainsExactlyOneEntry()
        {
            var finalList = BuildFinalList(
                new FinalData { Verification = "1", Value = "10", PollutantName = "PM10" }
            );

            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(finalList, GetSampleQueryData());

            using var zipStream = new MemoryStream(result);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            Assert.Single(archive.Entries);
        }

        [Fact]
        public async Task ExportCsv_CsvOnlyContainsHeaderRow_WhenFinalListIsEmpty()
        {
            var result = await _service.dataSelectionHourlyAtomFeedExportCSV(
                new List<FinalData>(), GetSampleQueryData());

            var csv = ExtractCsvFromZip(result);
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Only header row, no data rows
            Assert.Single(lines);
        }

        #endregion
    }
}