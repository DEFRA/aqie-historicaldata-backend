using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AqieHistoricaldataBackend.Atomfeed.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class HistoryexceedenceServiceTests
    {
        private const string NO2 = "Nitrogen dioxide", SO2 = "Sulphur dioxide";
        private static readonly string[] ExpectedOnTheHourStartTimes =
            { "2023-01-01T01:00:00", "2023-01-01T02:00:00", "2023-01-01T03:00:00" };
        private static readonly string[] ExpectedPollutantOrder = { "PM2.5", "PM10", NO2, "Ozone", SO2 };
        private readonly Mock<ILogger<HistoryexceedenceService>> _loggerMock = new();
        private readonly Mock<IAtomHourlyFetchService> _hourlyServiceMock = new();
        private readonly Mock<IAtomDailyFetchService> _dailyServiceMock = new();
        private readonly HistoryexceedenceService _service;
        private List<FinalData>? _hourlyPassedToDaily;

        public HistoryexceedenceServiceTests() =>
            _service = new HistoryexceedenceService(_loggerMock.Object, _hourlyServiceMock.Object, _dailyServiceMock.Object);

        [Fact]
        public async Task GetHistoryexceedencedata_KeepsOnlyOnTheHourReadings()
        {
            var day = new DateTime(2023, 1, 1);
            var hourly = new List<FinalData>
            {
                Hourly(NO2, day.AddHours(1)), Hourly(NO2, day.AddMinutes(15)), Hourly(NO2, day.AddMinutes(30)),
                Hourly(NO2, day.AddMinutes(45)), Hourly(NO2, day.AddHours(2)), Hourly(NO2, day.AddSeconds(30)),
                new() { PollutantName = NO2, StartTime = "not-a-date", Value = "10", Validity = "1" },
                new() { PollutantName = NO2, StartTime = null, Value = "10", Validity = "1" },
                Hourly(NO2, day.AddHours(3)),
            };

            await RunAsync(hourly, new List<FinalData> { Daily(NO2, 10) });

            Assert.Equal(ExpectedOnTheHourStartTimes, _hourlyPassedToDaily!.Select(h => h.StartTime));
        }

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsMergedRowsInPollutantOrder()
        {
            var jan1 = new DateTime(2023, 1, 1);
            var no2 = Hours(NO2, jan1, 8760); no2[0].Value = "201"; no2[1].Value = "999"; no2[2].Value = "250"; no2[3].Value = "200";
            var so2 = Hours(SO2, jan1, 8760); so2[0].Value = "351"; so2[1].Value = "500"; so2[2].Value = "350";
            var pm10 = Hours("PM10", jan1, 8760); pm10[0].Value = "999";
            var hourly = new List<FinalData> { Hourly(NO2, jan1.AddMinutes(15), value: "999") } // quarter-hour exceedance must be ignored
                .Concat(Hours("Ozone", jan1, 3)).Concat(so2).Concat(no2).Concat(pm10).Concat(Hours("PM2.5", jan1, 3)).ToList();
            var daily = new List<FinalData>
            {
                Daily("PM10", 60), Daily("PM10", 70), Daily("PM10", 50.5m), Daily("PM10", 19.5m), Daily("PM10", 0),
                Daily(SO2, 130), Daily(SO2, 125.5m), Daily(SO2, 14.5m),
                Daily(NO2, 40), Daily(NO2, 50), Daily("PM2.5", 5), Daily("Ozone", 5),
            };

            var rows = await RunAsync(hourly, daily);

            Assert.Equal(ExpectedPollutantOrder, rows.Select(r => Prop<string>(r, "PollutantName")));
            AssertRow(rows[0], "n/a", "n/a", "-", 0);
            AssertRow(rows[1], "n/a", "2", "50", 100);
            AssertRow(rows[2], "3", "n/a", "45", 100);
            AssertRow(rows[3], "n/a", "n/a", "-", 0);
            AssertRow(rows[4], "2", "1", "90", 100);
            Assert.All(rows, r => Assert.Equal("Data has been verified", Prop<string>(r, "dataVerifiedTag")));
        }

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsZeroForRegulatedPollutantsWithoutExceedances()
        {
            var t = new DateTime(2023, 1, 1, 1, 0, 0);
            var rows = await RunAsync(
                new List<FinalData> { Hourly(NO2, t), Hourly(SO2, t), Hourly("PM10", t) },
                new List<FinalData> { Daily(NO2, 10), Daily(SO2, 10), Daily("PM10", 10) });

            AssertRow(Row(rows, "PM10"), "n/a", "0", "-", 0);
            AssertRow(Row(rows, NO2), "0", "n/a", "-", 0);
            AssertRow(Row(rows, SO2), "0", "0", "-", 0);
        }

        [Fact]
        public async Task GetHistoryexceedencedata_OmitsPollutantsWithoutValidHoursOrDailyData()
        {
            var t = new DateTime(2023, 1, 1, 1, 0, 0);
            var rows = await RunAsync(
                new List<FinalData> { Hourly(NO2, t, validity: "0"), Hourly(SO2, t), Hourly("PM10", t) },
                new List<FinalData> { Daily(NO2, 10), Daily("PM10", 10) });

            Assert.Equal("PM10", Prop<string>(Assert.Single(rows), "PollutantName"));
        }

        [Fact]
        public async Task GetHistoryexceedencedata_DataCaptureCountsDistinctValidHoursWithinYear()
        {
            var jan1 = new DateTime(2023, 1, 1);
            var hourly = Hours(NO2, jan1, 876)                          // 10% of 8760
                .Concat(Hours(NO2, jan1, 876))                           // duplicates
                .Concat(Hours(NO2, jan1.AddHours(-100), 100))            // previous year
                .Concat(Hours(NO2, jan1.AddYears(1), 100))               // next year (end boundary)
                .Concat(Hours(NO2, jan1.AddHours(1000), 200, validity: "0"))
                .Concat(Hours(NO2, jan1.AddHours(2000), 50, validity: null))
                .ToList();

            var rows = await RunAsync(hourly, new List<FinalData> { Daily(NO2, 40) });

            AssertRow(Assert.Single(rows), "0", "n/a", "-", 10);
        }

        [Fact]
        public async Task GetHistoryexceedencedata_VerifiedTagUsesLastHourlyReadingBeforeFirstUnverified()
        {
            var hourly = new List<FinalData>
            {
                Hourly(NO2, new DateTime(2023, 1, 5, 1, 0, 0)),
                Hourly(NO2, new DateTime(2023, 1, 6, 0, 15, 0)),          // filtered out, so must not be used
                Hourly(NO2, new DateTime(2023, 1, 7, 2, 0, 0), verification: "2"),
            };

            var rows = await RunAsync(hourly, new List<FinalData> { Daily(NO2, 10) });

            Assert.Equal($"Data has been verified until {new DateTime(2023, 1, 5):dd MMMM}",
                Prop<string>(Assert.Single(rows), "dataVerifiedTag"));
        }

        [Fact]
        public async Task GetHistoryexceedencedata_VerifiedTagIsNotVerified_WhenFirstReadingUnverified()
        {
            var rows = await RunAsync(
                new List<FinalData> { Hourly(NO2, new DateTime(2023, 1, 1, 1, 0, 0), verification: "2") },
                new List<FinalData> { Daily(NO2, 10) });

            Assert.Equal("Data has not been verified", Prop<string>(Assert.Single(rows), "dataVerifiedTag"));
        }

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsFailureAndLogs_WhenHourlyValueIsNotNumeric()
        {
            _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<FinalData> { Hourly(NO2, new DateTime(2023, 1, 1, 1, 0, 0), value: "invalid") });

            object? result = await _service.GetHistoryexceedencedata(new QueryStringData { SiteId = "site1", Year = "2023" });

            Assert.Equal("Failure", result);
            VerifyErrorLogged<FormatException>();
        }

        [Fact]
        public async Task GetHistoryexceedencedata_ReturnsFailureAndLogs_WhenHourlyServiceThrows()
        {
            _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Service error"));

            object? result = await _service.GetHistoryexceedencedata(new QueryStringData { SiteId = "site1", Year = "2023" });

            Assert.Equal("Failure", result);
            VerifyErrorLogged<InvalidOperationException>();
        }

        [Fact]
        public async Task GetHistoryexceedencedata_DefaultsNullSiteAndYearToEmpty_AndFailsOnMissingYear()
        {
            _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("", "", "All")).ReturnsAsync(new List<FinalData>());
            _dailyServiceMock.Setup(s => s.GetAtomDailydatafetch(It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>()))
                .ReturnsAsync(new List<FinalData>());

            object? result = await _service.GetHistoryexceedencedata(new QueryStringData());

            Assert.Equal("Failure", result);
            _hourlyServiceMock.Verify(s => s.GetAtomHourlydatafetch("", "", "All"), Times.Once);
            VerifyErrorLogged<FormatException>();
        }

        [Fact]
        public void GetDataCapturePercentages_UsesLeapYearHours()
        {
            var row = Assert.Single(InvokeCapture(Hours("PM10", new DateTime(2024, 1, 1), 4392), "2024"));
            Assert.Equal(50.0, Prop<double>(row, "DataCapturePercentage"), 10);
        }

        [Fact]
        public void GetDataCapturePercentages_CurrentYearUsesDaysUpToToday()
        {
            var today = DateTime.Today;
            var data = Hours("PM10", new DateTime(today.Year, 1, 1), 24).Concat(Hours("PM10", today, 2)).ToList();

            var row = Assert.Single(InvokeCapture(data, today.Year.ToString()));

            Assert.Equal(100.0 / (today.DayOfYear - 1), Prop<double>(row, "DataCapturePercentage"), 10);
        }

        [Fact]
        public void GetDataCapturePercentages_IgnoresUnparseableStartTimes()
        {
            var data = Hours("PM10", new DateTime(2023, 1, 1), 876);
            data.Add(new FinalData { PollutantName = "PM10", StartTime = "not-a-date", Validity = "1" });

            var row = Assert.Single(InvokeCapture(data, "2023"));

            Assert.Equal(10.0, Prop<double>(row, "DataCapturePercentage"), 10);
        }

        [Fact]
        public void GetAnnualExceedances_ReturnsNullTotal_WhenAllTotalsAreZero()
        {
            var method = typeof(HistoryexceedenceService).GetMethod("GetAnnualExceedances", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = (List<FinalData>)method.Invoke(null, new object[] { new List<FinalData> { Daily("PM10", 0, "2024-01-01") } })!;

            var row = Assert.Single(result);
            Assert.Equal("2024", row.ReportDate);
            Assert.Null(row.Total);
        }

        private static string Fmt(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        private static FinalData Hourly(string pollutant, DateTime t, string value = "10", string? validity = "1", string verification = "1") =>
            new() { PollutantName = pollutant, StartTime = Fmt(t), Value = value, Validity = validity, Verification = verification };

        private static List<FinalData> Hours(string pollutant, DateTime start, int count, string? validity = "1") =>
            Enumerable.Range(0, count).Select(i => Hourly(pollutant, start.AddHours(i), validity: validity)).ToList();

        private static FinalData Daily(string pollutant, decimal total, string reportDate = "2023-01-01") =>
            new() { DailyPollutantName = pollutant, Total = total, ReportDate = reportDate };

        private static T Prop<T>(object row, string name) => (T)row.GetType().GetProperty(name)!.GetValue(row)!;

        private static object Row(List<object> rows, string pollutant) => rows.Single(r => Prop<string>(r, "PollutantName") == pollutant);

        private static void AssertRow(object row, string hourly, string daily, string annual, double capture)
        {
            Assert.Equal(hourly, Prop<string>(row, "hourlyCount"));
            Assert.Equal(daily, Prop<string>(row, "dailyCount"));
            Assert.Equal(annual, Prop<string>(row, "annualcount"));
            Assert.Equal(capture, Prop<double>(row, "dataCapturePercentage"));
        }

        private static List<object> InvokeCapture(List<FinalData> data, string year) =>
            ((List<dynamic>)typeof(HistoryexceedenceService)
                .GetMethod("GetDataCapturePercentages", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object[] { data, year })!).Cast<object>().ToList();

        private async Task<List<object>> RunAsync(List<FinalData> hourly, List<FinalData> daily, string year = "2023")
        {
            _hourlyServiceMock.Setup(s => s.GetAtomHourlydatafetch("site1", year, "All")).ReturnsAsync(hourly);
            _dailyServiceMock.Setup(s => s.GetAtomDailydatafetch(It.IsAny<List<FinalData>>(), It.IsAny<QueryStringData>()))
                .Callback<List<FinalData>, QueryStringData>((h, _) => _hourlyPassedToDaily = h)
                .ReturnsAsync(daily);

            object? result = await _service.GetHistoryexceedencedata(new QueryStringData { SiteId = "site1", Year = year });
            return Assert.IsAssignableFrom<IEnumerable<object>>(result).ToList();
        }

        private void VerifyErrorLogged<TException>() where TException : Exception =>
            _loggerMock.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<TException>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}