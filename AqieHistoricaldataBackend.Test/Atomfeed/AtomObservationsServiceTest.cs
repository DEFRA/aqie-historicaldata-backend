using AqieHistoricaldataBackend.Atomfeed.Models;
using AqieHistoricaldataBackend.Atomfeed.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
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
            _sut = new AtomObservationsService(
                Mock.Of<ILogger<AtomObservationsService>>(),
                _fetch.Object,
                new MemoryCache(new MemoryCacheOptions()));
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
            => _fetch.Setup(f => f.GetAtomHourlydatafetchWithStatus(
                        It.IsAny<string>(), year.ToString(), It.IsAny<string>(), It.IsAny<string>()))
                     .ReturnsAsync(new AtomHourlyFetchOutcome(rows.ToList(), false));

        private void SetupUpstreamFailure(int year)
            => _fetch.Setup(f => f.GetAtomHourlydatafetchWithStatus(
                        It.IsAny<string>(), year.ToString(), It.IsAny<string>(), It.IsAny<string>()))
                     .ReturnsAsync(new AtomHourlyFetchOutcome([], true));

        private static ObservationsRequest Request(
            string period = "7days",
            string aggregation = "hourly",
            string? year = "2019",
            string? pollutant = null,
            string? network = null,
            string? anchor = null)
        {
            ObservationsRequest.TryCreate(
                "CLL2", pollutant, period, aggregation, year, network, anchor,
                out var request, out _).Should().BeTrue();
            return request!;
        }

        private static bool TryCreate(
            out ObservationsRequest? request,
            out string? error,
            string? siteId = "CLL2",
            string? pollutant = null,
            string? period = null,
            string? aggregation = null,
            string? year = null,
            string? network = null,
            string? anchor = null)
            => ObservationsRequest.TryCreate(
                siteId, pollutant, period, aggregation, year, network, anchor, out request, out error);

        #region Request validation

        [Theory]
        [InlineData("24hours")]
        [InlineData("7days")]
        [InlineData("30days")]
        public void TryCreate_AcceptsSupportedPeriods(string period)
        {
            TryCreate(out var request, out var error, period: period).Should().BeTrue();
            error.Should().BeNull();
            request!.Period.Should().Be(period);
        }

        [Fact]
        public void TryCreate_DefaultsToSevenDaysHourlyAurnLatest()
        {
            TryCreate(out var request, out _);

            request!.Period.Should().Be("7days");
            request.Aggregation.Should().Be(ObservationsAggregation.Hourly);
            request.Anchor.Should().Be(ObservationsAnchor.Latest);
            request.Network.Should().Be("AURN");
            request.Window.Should().Be(TimeSpan.FromDays(7));
        }

        [Fact]
        public void TryCreate_LeavesWindowNull_ForWholeYear()
        {
            TryCreate(out var request, out _, period: "year", aggregation: "daily");
            request!.Window.Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryCreate_RejectsMissingSiteId(string? siteId)
        {
            TryCreate(out _, out var error, siteId: siteId).Should().BeFalse();
            error.Should().Be("siteId is required.");
        }

        [Fact]
        public void TryCreate_RejectsUnknownPeriod()
        {
            TryCreate(out _, out var error, period: "3months").Should().BeFalse();
            error.Should().StartWith("period must be one of");
        }

        [Fact]
        public void TryCreate_RejectsUnknownPollutantRatherThanSilentlyReturningAll()
        {
            TryCreate(out _, out var error, pollutant: "carbon monoxide").Should().BeFalse();
            error.Should().StartWith("pollutant must be one of");
        }

        [Theory]
        [InlineData("nitrogen DIOXIDE", "Nitrogen dioxide")]
        [InlineData("NO2", "Nitrogen dioxide")]
        [InlineData("pm25", "PM2.5")]
        [InlineData("PM2.5", "PM2.5")]
        [InlineData("o3", "Ozone")]
        public void TryCreate_ResolvesPollutantByNameOrCode(string input, string expected)
        {
            TryCreate(out var request, out _, pollutant: input).Should().BeTrue();
            request!.Pollutant.Should().Be(expected);
        }

        [Theory]
        [InlineData("aurn", "AURN")]
        [InlineData("non-aurn", "NON-AURN")]
        public void TryCreate_NormalisesNetwork(string input, string expected)
        {
            TryCreate(out var request, out _, network: input).Should().BeTrue();
            request!.Network.Should().Be(expected);
        }

        [Fact]
        public void TryCreate_RejectsUnknownNetwork()
        {
            TryCreate(out _, out var error, network: "LAQN").Should().BeFalse();
            error.Should().Be("network must be one of: AURN, NON-AURN.");
        }

        [Theory]
        [InlineData("now", ObservationsAnchor.Now)]
        [InlineData("latest", ObservationsAnchor.Latest)]
        public void TryCreate_AcceptsAnchor(string input, ObservationsAnchor expected)
        {
            TryCreate(out var request, out _, anchor: input).Should().BeTrue();
            request!.Anchor.Should().Be(expected);
        }

        [Fact]
        public void TryCreate_RejectsUnknownAnchor()
        {
            TryCreate(out _, out var error, anchor: "yesterday").Should().BeFalse();
            error.Should().Be("anchor must be one of: latest, now.");
        }

        [Fact]
        public void TryCreate_RejectsWholeYearOfHourlyRowsForAllPollutants()
        {
            TryCreate(out _, out var error, period: "year", aggregation: "hourly").Should().BeFalse();
            error.Should().StartWith("period=year with aggregation=hourly requires a pollutant");
        }

        [Fact]
        public void TryCreate_AllowsWholeYearOfHourlyRows_ForASinglePollutant()
        {
            TryCreate(out _, out _, period: "year", aggregation: "hourly", pollutant: "NO2")
                .Should().BeTrue();
        }

        [Theory]
        [InlineData("not-a-year")]
        [InlineData("1959")]
        [InlineData("3000")]
        public void TryCreate_RejectsOutOfRangeYear(string year)
        {
            TryCreate(out _, out var error, year: year).Should().BeFalse();
            error.Should().StartWith("year must be a number");
        }

        #endregion

        #region Upstream failure

        [Fact]
        public async Task GetObservations_Throws_WhenUpstreamFeedFails()
        {
            SetupUpstreamFailure(2019);

            var act = () => _sut.GetObservationsAsync(Request());

            await act.Should().ThrowAsync<UpstreamFeedException>();
        }

        [Fact]
        public async Task GetObservations_DoesNotThrow_WhenFeedIsReadableButEmpty()
        {
            SetupYear(2019);

            var result = await _sut.GetObservationsAsync(Request());

            result.Count.Should().Be(0);
        }

        #endregion

        #region Network passthrough

        [Theory]
        [InlineData("AURN")]
        [InlineData("NON-AURN")]
        public async Task GetObservations_PassesNetworkToTheFetchService(string network)
        {
            SetupYear(2019, Row("2019-06-10T00:00:00Z", "10"));

            await _sut.GetObservationsAsync(Request(network: network));

            _fetch.Verify(f => f.GetAtomHourlydatafetchWithStatus(
                "CLL2", "2019", It.IsAny<string>(), network), Times.Once);
        }

        [Fact]
        public async Task GetObservations_EchoesNetworkInTheResult()
        {
            SetupYear(2019, Row("2019-06-10T00:00:00Z", "10"));

            var result = await _sut.GetObservationsAsync(Request(network: "NON-AURN"));

            result.Network.Should().Be("NON-AURN");
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
        public async Task GetObservations_ReturnsNothing_WhenAnchoredOnNowAndFeedLags()
        {
            SetupYear(2019, Row("2019-06-10T00:00:00Z", "10"));

            var result = await _sut.GetObservationsAsync(Request(period: "7days", anchor: "now"));

            result.Count.Should().Be(0);
        }

        [Fact]
        public async Task GetObservations_ReportsRequestedWindowBounds_SeparatelyFromDataBounds()
        {
            SetupYear(2019,
                Row("2019-06-01T00:00:00Z", "10"),
                Row("2019-06-10T00:00:00Z", "30"));

            var result = await _sut.GetObservationsAsync(Request(period: "7days"));

            result.WindowFrom.Should().Be("2019-06-03T00:00:00Z");
            result.WindowTo.Should().Be("2019-06-10T00:00:00Z");
            result.From.Should().Be("2019-06-10T00:00:00Z");
        }

        [Fact]
        public async Task GetObservations_ReturnsWholeYear_WhenPeriodIsYear()
        {
            SetupYear(2019,
                Row("2019-01-01T00:00:00Z", "10"),
                Row("2019-12-31T23:00:00Z", "20"));

            var result = await _sut.GetObservationsAsync(Request(period: "year", aggregation: "daily"));

            result.Count.Should().Be(2);
            _fetch.Verify(f => f.GetAtomHourlydatafetchWithStatus(
                It.IsAny<string>(), "2018", It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetObservations_FetchesPreviousYear_WhenWindowCrossesJanuaryFirst()
        {
            SetupYear(2019, Row("2019-01-02T00:00:00Z", "30"));
            SetupYear(2018, Row("2018-12-30T00:00:00Z", "10"), Row("2018-12-31T00:00:00Z", "20"));

            var result = await _sut.GetObservationsAsync(Request(period: "7days"));

            _fetch.Verify(f => f.GetAtomHourlydatafetchWithStatus(
                "CLL2", "2018", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            result.Count.Should().Be(3);
            result.From.Should().Be("2018-12-30T00:00:00Z");
        }

        [Fact]
        public async Task GetObservations_DoesNotFetchPreviousYear_WhenWindowFitsInsideTheYear()
        {
            SetupYear(2019, Row("2019-06-10T00:00:00Z", "10"));

            await _sut.GetObservationsAsync(Request(period: "24hours"));

            _fetch.Verify(f => f.GetAtomHourlydatafetchWithStatus(
                It.IsAny<string>(), "2018", It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Caching

        [Fact]
        public async Task GetObservations_FetchesUpstreamOnlyOnce_ForRepeatedIdenticalRequests()
        {
            SetupYear(2019, Row("2019-06-10T00:00:00Z", "10"));

            await _sut.GetObservationsAsync(Request());
            await _sut.GetObservationsAsync(Request());

            _fetch.Verify(f => f.GetAtomHourlydatafetchWithStatus(
                "CLL2", "2019", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task GetObservations_DoesNotShareCacheAcrossNetworks()
        {
            SetupYear(2019, Row("2019-06-10T00:00:00Z", "10"));

            await _sut.GetObservationsAsync(Request(network: "AURN"));
            await _sut.GetObservationsAsync(Request(network: "NON-AURN"));

            _fetch.Verify(f => f.GetAtomHourlydatafetchWithStatus(
                It.IsAny<string>(), "2019", It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
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
        [InlineData("Nitrogen dioxide", "NO2")]
        [InlineData("PM2.5", "PM25")]
        [InlineData("Ozone", "O3")]
        [InlineData("Sulphur dioxide", "SO2")]
        [InlineData("PM10", "PM10")]
        public async Task GetObservations_EmitsStablePollutantCode(string name, string code)
        {
            SetupYear(2019, Row("2019-06-10T00:00:00Z", "10", pollutant: name));

            var result = await _sut.GetObservationsAsync(Request());

            result.Observations.Single().PollutantCode.Should().Be(code);
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

            var result = await _sut.GetObservationsAsync(Request(year: null, period: "year", aggregation: "daily"));

            result.Count.Should().Be(1);
        }

        #endregion
    }
}
