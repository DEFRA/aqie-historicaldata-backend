using AqieHistoricaldataBackend.Atomfeed.Models;
using AqieHistoricaldataBackend.Atomfeed.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Test.Atomfeed
{
    public class AtomObservationsServiceTest
    {
        private readonly Mock<IAtomHourlyFetchService> _fetch = new();
        private readonly AtomObservationsService _sut;

        public AtomObservationsServiceTest()
        {
            _sut = new AtomObservationsService(Mock.Of<ILogger<AtomObservationsService>>(), _fetch.Object);
        }

        private static FinalData Row(string startTime, string value, string pollutant = "Ozone", string verification = "1")
            => new()
            {
                StartTime = startTime,
                EndTime = startTime,
                Value = value,
                PollutantName = pollutant,
                Verification = verification,
                Validity = "1"
            };

        private void SetupYear(int year, params FinalData[] rows)
            => _fetch.Setup(f => f.GetAtomHourlydatafetch(It.IsAny<string>(), year.ToString(), It.IsAny<string>()))
                     .ReturnsAsync(rows.ToList());

        private static ObservationsRequest Request(
            string period = "7days", string aggregation = "hourly", string? year = "2019", string? pollutant = null)
        {
            ObservationsRequest.TryCreate(
                "CLL2", pollutant, period, aggregation, year,
                AtomObservationsService.KnownPollutants, out var request, out _).Should().BeTrue();
            return request!;
        }

        #region Request validation

        [Theory]
        [InlineData("24hours")]
        [InlineData("7days")]
        [InlineData("30days")]
        [InlineData("year")]
        public void TryCreate_AcceptsSupportedPeriods(string period)
        {
            var ok = ObservationsRequest.TryCreate(
                "CLL2", null, period, null, null,
                AtomObservationsService.KnownPollutants, out var request, out var error);

            ok.Should().BeTrue();
            error.Should().BeNull();
            request!.Period.Should().Be(period);
        }

        [Fact]
        public void TryCreate_DefaultsToSevenDaysHourly_WhenPeriodAndAggregationOmitted()
        {
            ObservationsRequest.TryCreate(
                "CLL2", null, null, null, null,
                AtomObservationsService.KnownPollutants, out var request, out _);

            request!.Period.Should().Be("7days");
            request.Aggregation.Should().Be(ObservationsAggregation.Hourly);
            request.Window.Should().Be(TimeSpan.FromDays(7));
        }

        [Fact]
        public void TryCreate_LeavesWindowNull_ForWholeYear()
        {
            ObservationsRequest.TryCreate(
                "CLL2", null, "year", null, null,
                AtomObservationsService.KnownPollutants, out var request, out _);

            request!.Window.Should().BeNull();
        }

        [Theory]
        [InlineData(null, "siteId is required.")]
        [InlineData("", "siteId is required.")]
        [InlineData("   ", "siteId is required.")]
        public void TryCreate_RejectsMissingSiteId(string? siteId, string expected)
        {
            var ok = ObservationsRequest.TryCreate(
                siteId, null, null, null, null,
                AtomObservationsService.KnownPollutants, out _, out var error);

            ok.Should().BeFalse();
            error.Should().Be(expected);
        }

        [Fact]
        public void TryCreate_RejectsUnknownPeriod()
        {
            var ok = ObservationsRequest.TryCreate(
                "CLL2", null, "3months", null, null,
                AtomObservationsService.KnownPollutants, out _, out var error);

            ok.Should().BeFalse();
            error.Should().Contain("period must be one of");
        }

        [Fact]
        public void TryCreate_RejectsUnknownPollutantRatherThanSilentlyReturningAll()
        {
            var ok = ObservationsRequest.TryCreate(
                "CLL2", "NO2", null, null, null,
                AtomObservationsService.KnownPollutants, out _, out var error);

            ok.Should().BeFalse();
            error.Should().Contain("pollutant must be one of");
        }

        [Fact]
        public void TryCreate_MatchesPollutantCaseInsensitively()
        {
            ObservationsRequest.TryCreate(
                "CLL2", "nitrogen DIOXIDE", null, null, null,
                AtomObservationsService.KnownPollutants, out var request, out _);

            request!.Pollutant.Should().Be("Nitrogen dioxide");
        }

        [Theory]
        [InlineData("not-a-year")]
        [InlineData("1959")]
        [InlineData("3000")]
        public void TryCreate_RejectsOutOfRangeYear(string year)
        {
            var ok = ObservationsRequest.TryCreate(
                "CLL2", null, null, null, year,
                AtomObservationsService.KnownPollutants, out _, out var error);

            ok.Should().BeFalse();
            error.Should().Contain("year must be a number");
        }

        #endregion

        #region Windowing

        [Fact]
        public async Task GetObservations_TrimsToWindow_AnchoredOnLatestReadingNotWallClock()
        {
            // Feed lags by years; anchoring on "now" would return nothing.
            SetupYear(2019,
                Row("2019-06-01T00:00:00Z", "10"),
                Row("2019-06-09T00:00:00Z", "20"),
                Row("2019-06-10T00:00:00Z", "30"));

            var result = await _sut.GetObservationsAsync(Request(period: "7days"));

            result.Count.Should().Be(2);
            result.From.Should().Be("2019-06-09T00:00:00Z");
            result.To.Should().Be("2019-06-10T00:00:00Z");
        }

        [Fact]
        public async Task GetObservations_ReturnsWholeYear_WhenPeriodIsYear()
        {
            SetupYear(2019,
                Row("2019-01-01T00:00:00Z", "10"),
                Row("2019-12-31T23:00:00Z", "20"));

            var result = await _sut.GetObservationsAsync(Request(period: "year"));

            result.Count.Should().Be(2);
            _fetch.Verify(f => f.GetAtomHourlydatafetch(It.IsAny<string>(), "2018", It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetObservations_FetchesPreviousYear_WhenWindowCrossesJanuaryFirst()
        {
            SetupYear(2019, Row("2019-01-02T00:00:00Z", "30"));
            SetupYear(2018, Row("2018-12-30T00:00:00Z", "10"), Row("2018-12-31T00:00:00Z", "20"));

            var result = await _sut.GetObservationsAsync(Request(period: "7days"));

            _fetch.Verify(f => f.GetAtomHourlydatafetch("CLL2", "2018", It.IsAny<string>()), Times.Once);
            result.Count.Should().Be(3);
            result.From.Should().Be("2018-12-30T00:00:00Z");
        }

        [Fact]
        public async Task GetObservations_DoesNotFetchPreviousYear_WhenWindowFitsInsideTheYear()
        {
            SetupYear(2019, Row("2019-06-10T00:00:00Z", "10"));

            await _sut.GetObservationsAsync(Request(period: "24hours"));

            _fetch.Verify(f => f.GetAtomHourlydatafetch(It.IsAny<string>(), "2018", It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Hourly output

        [Fact]
        public async Task GetObservations_MapsMinus99ToNullValue()
        {
            SetupYear(2019, Row("2019-06-10T00:00:00Z", "-99"));

            var result = await _sut.GetObservationsAsync(Request());

            result.Observations.Single().Value.Should().BeNull();
        }

        [Theory]
        [InlineData("1", "V")]
        [InlineData("2", "P")]
        [InlineData("3", "N")]
        [InlineData("9", null)]
        public async Task GetObservations_MapsVerificationFlags(string verification, string? expected)
        {
            SetupYear(2019, Row("2019-06-10T00:00:00Z", "10", verification: verification));

            var result = await _sut.GetObservationsAsync(Request());

            result.Observations.Single().Status.Should().Be(expected);
        }

        [Fact]
        public async Task GetObservations_OrdersByTimestampAndListsPollutants()
        {
            SetupYear(2019,
                Row("2019-06-10T01:00:00Z", "20", pollutant: "PM10"),
                Row("2019-06-10T00:00:00Z", "10", pollutant: "Ozone"));

            var result = await _sut.GetObservationsAsync(Request());

            result.Observations.Select(o => o.Timestamp)
                  .Should().ContainInOrder("2019-06-10T00:00:00Z", "2019-06-10T01:00:00Z");
            result.Pollutants.Should().ContainInOrder("Ozone", "PM10");
        }

        #endregion

        #region Daily aggregation

        [Fact]
        public async Task GetObservations_AveragesDailyValues_WhenCaptureIsSufficient()
        {
            var rows = Enumerable.Range(0, 24)
                .Select(h => Row($"2019-06-10T{h:00}:00:00Z", "10"))
                .ToArray();
            SetupYear(2019, rows);

            var result = await _sut.GetObservationsAsync(Request(period: "24hours", aggregation: "daily"));

            var day = result.Observations.Single();
            day.Timestamp.Should().Be("2019-06-10");
            day.Value.Should().Be(10m);
            day.Capture.Should().Be(1m);
        }

        [Fact]
        public async Task GetObservations_ReturnsNullValue_WhenDailyCaptureBelowThreshold()
        {
            // 12 of 24 readings present — below the 75% threshold. Must be null, not zero.
            var rows = Enumerable.Range(0, 24)
                .Select(h => Row($"2019-06-10T{h:00}:00:00Z", h < 12 ? "10" : "-99"))
                .ToArray();
            SetupYear(2019, rows);

            var result = await _sut.GetObservationsAsync(Request(period: "24hours", aggregation: "daily"));

            var day = result.Observations.Single();
            day.Value.Should().BeNull();
            day.Capture.Should().Be(0.5m);
        }

        #endregion

        #region Empty results

        [Fact]
        public async Task GetObservations_ReturnsEmptyResult_WhenFeedHasNoRows()
        {
            SetupYear(2019);

            var result = await _sut.GetObservationsAsync(Request());

            result.Count.Should().Be(0);
            result.Observations.Should().BeEmpty();
            result.From.Should().BeNull();
        }

        [Fact]
        public async Task GetObservations_FallsBackToPreviousYear_WhenCurrentYearUnpublishedAndNoYearRequested()
        {
            var currentYear = DateTime.UtcNow.Year;
            SetupYear(currentYear);
            SetupYear(currentYear - 1, Row($"{currentYear - 1}-06-10T00:00:00Z", "10"));

            var result = await _sut.GetObservationsAsync(Request(year: null));

            result.Count.Should().Be(1);
        }

        #endregion
    }
}
