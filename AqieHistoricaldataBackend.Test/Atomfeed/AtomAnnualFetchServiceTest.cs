using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AqieHistoricaldataBackend.Atomfeed.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
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

        // ----------------------------------------------------------------
        // Happy-path: valid data with non-zero totals
        // Covers: GroupBy + Select + validTotals.Any() == true path
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsCorrectAnnualAverage_WhenAllTotalsNonZero()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = "2024-01-01", Total = 10, DailyPollutantName = "NO2", DailyVerification = "Verified" },
                new FinalData { ReportDate = "2024-06-15", Total = 20, DailyPollutantName = "NO2", DailyVerification = "Verified" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Single(result);
            Assert.Equal("2024", result[0].ReportDate);
            Assert.Equal("NO2", result[0].AnnualPollutantName);
            Assert.Equal("Verified", result[0].AnnualVerification);
            Assert.Equal(15m, result[0].Total);
        }

        // ----------------------------------------------------------------
        // All totals are zero → validTotals.Any() == false path (returns 0)
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsZero_WhenAllTotalsAreZero()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = "2023-03-01", Total = 0, DailyPollutantName = "O3", DailyVerification = "Unverified" },
                new FinalData { ReportDate = "2023-09-10", Total = 0, DailyPollutantName = "O3", DailyVerification = "Unverified" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Single(result);
            Assert.Equal(0m, result[0].Total);
        }

        // ----------------------------------------------------------------
        // Mixed zero and non-zero totals → zeros excluded from average
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_ExcludesZeroTotals_FromAverage()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = "2022-01-01", Total = 0,  DailyPollutantName = "PM2.5", DailyVerification = "Verified" },
                new FinalData { ReportDate = "2022-05-20", Total = 40, DailyPollutantName = "PM2.5", DailyVerification = "Verified" },
                new FinalData { ReportDate = "2022-11-11", Total = 60, DailyPollutantName = "PM2.5", DailyVerification = "Verified" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Single(result);
            // Average of 40 and 60 (zero excluded) = 50
            Assert.Equal(50m, result[0].Total);
        }

        // ----------------------------------------------------------------
        // Empty daily-service result → empty annual result
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsEmpty_WhenDailyServiceReturnsEmpty()
        {
            var input = new List<FinalData>();

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(new List<FinalData>());

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Empty(result);
        }

        // ----------------------------------------------------------------
        // Multiple pollutants in the same year → one group per pollutant
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_GroupsByPollutantName_WithinSameYear()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = "2021-01-01", Total = 10, DailyPollutantName = "NO2", DailyVerification = "V" },
                new FinalData { ReportDate = "2021-06-01", Total = 30, DailyPollutantName = "NO2", DailyVerification = "V" },
                new FinalData { ReportDate = "2021-01-01", Total = 20, DailyPollutantName = "SO2", DailyVerification = "V" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.AnnualPollutantName == "NO2" && r.Total == 20m);
            Assert.Contains(result, r => r.AnnualPollutantName == "SO2" && r.Total == 20m);
        }

        // ----------------------------------------------------------------
        // Multiple years → one group per year/pollutant/verification combo
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_GroupsByYear_WhenMultipleYearsPresent()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = "2020-05-01", Total = 10, DailyPollutantName = "CO", DailyVerification = "V" },
                new FinalData { ReportDate = "2021-05-01", Total = 30, DailyPollutantName = "CO", DailyVerification = "V" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.ReportDate == "2020" && r.Total == 10m);
            Assert.Contains(result, r => r.ReportDate == "2021" && r.Total == 30m);
        }

        // ----------------------------------------------------------------
        // Same pollutant, different verification codes → separate groups
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_GroupsByVerification_WhenVerificationDiffers()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = "2024-01-01", Total = 10, DailyPollutantName = "NO2", DailyVerification = "Verified" },
                new FinalData { ReportDate = "2024-06-01", Total = 20, DailyPollutantName = "NO2", DailyVerification = "Unverified" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.AnnualVerification == "Verified"   && r.Total == 10m);
            Assert.Contains(result, r => r.AnnualVerification == "Unverified" && r.Total == 20m);
        }

        // ----------------------------------------------------------------
        // Single record → returns it as-is (no averaging needed)
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsSingleRecord_WhenOnlyOneEntry()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = "2024-07-04", Total = 42, DailyPollutantName = "PM10", DailyVerification = "V" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Single(result);
            Assert.Equal(42m, result[0].Total);
            Assert.Equal("PM10", result[0].AnnualPollutantName);
        }

        // ----------------------------------------------------------------
        // QueryStringData is null-safe: passes it through unchanged
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_HandlesNullQueryStringData()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = "2024-01-01", Total = 5, DailyPollutantName = "SO2", DailyVerification = "V" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, null!))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, null!);

            Assert.Single(result);
        }

        // ----------------------------------------------------------------
        // Exception from daily service → returns empty list + logs both
        // error messages (Message and StackTrace branches)
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsEmptyList_AndLogsError_WhenDailyServiceThrows()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = "2024-01-01", Total = 10, DailyPollutantName = "CO", DailyVerification = "V" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ThrowsAsync(new Exception("daily service failure"));

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Empty(result);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2));
        }

        // ----------------------------------------------------------------
        // Exception: invalid ReportDate causes Convert.ToDateTime to throw
        // → caught by the catch block, returns empty list
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsEmptyList_WhenReportDateIsInvalid()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = "not-a-date", Total = 10, DailyPollutantName = "NO2", DailyVerification = "V" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Empty(result);
        }

        // ----------------------------------------------------------------
        // Null ReportDate causes Convert.ToDateTime to throw
        // → caught by the catch block, returns empty list
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_ReturnsEmptyList_WhenReportDateIsNull()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = null, Total = 10, DailyPollutantName = "PM10", DailyVerification = "V" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Empty(result);
        }

        // ----------------------------------------------------------------
        // Exactly one non-zero and one zero total: average equals the
        // single non-zero value (not divided by 2)
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_AveragesOnlyNonZeroValues_NotAllValues()
        {
            var input = new List<FinalData>
            {
                new FinalData { ReportDate = "2024-02-01", Total = 0,  DailyPollutantName = "NO2", DailyVerification = "V" },
                new FinalData { ReportDate = "2024-03-01", Total = 100, DailyPollutantName = "NO2", DailyVerification = "V" }
            };

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Single(result);
            // Only 100 is in validTotals → average = 100, not (0+100)/2 = 50
            Assert.Equal(100m, result[0].Total);
        }

        // ----------------------------------------------------------------
        // Large dataset: verify count and spot-check a specific group
        // ----------------------------------------------------------------
        [Fact]
        public async Task GetAtomAnnualdatafetch_HandlesLargeDataset_Correctly()
        {
            var input = new List<FinalData>();
            for (var i = 1; i <= 365; i++)
            {
                input.Add(new FinalData
                {
                    ReportDate = new DateTime(2024, 1, 1).AddDays(i - 1).ToString("yyyy-MM-dd"),
                    Total = i,
                    DailyPollutantName = "NO2",
                    DailyVerification = "V"
                });
            }

            _dailyFetchServiceMock
                .Setup(s => s.GetAtomDailydatafetch(input, It.IsAny<QueryStringData>()))
                .ReturnsAsync(input);

            var result = await _service.GetAtomAnnualdatafetch(input, new QueryStringData());

            Assert.Single(result);
            Assert.Equal("2024", result[0].ReportDate);
            // Average of 1..365 = (365*366/2) / 365 = 183
            Assert.Equal(183m, result[0].Total);
        }
    }
}