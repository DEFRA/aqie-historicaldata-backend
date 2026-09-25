using System.Globalization;
using AqieHistoricaldataBackend.Atomfeed.Models;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomObservationsService
    {
        Task<ObservationsResult> GetObservationsAsync(ObservationsRequest request);
    }

    /// <summary>
    /// Serves station observations to the front end as JSON over a requested time window.
    /// Unlike the download endpoints this never writes to S3 — the rows are returned inline.
    /// </summary>
    public class AtomObservationsService(
        ILogger<AtomObservationsService> logger,
        IAtomHourlyFetchService atomHourlyFetchService) : IAtomObservationsService
    {
        public static readonly string[] KnownPollutants =
            ["Nitrogen dioxide", "PM10", "PM2.5", "Ozone", "Sulphur dioxide"];

        private const decimal MinimumDailyCapture = 0.75m;

        public async Task<ObservationsResult> GetObservationsAsync(ObservationsRequest request)
        {
            var anchorYear = request.Year ?? DateTime.UtcNow.Year;
            var rows = await FetchYearAsync(request.SiteId, anchorYear, request.Pollutant);

            // The current year's feed may not be published yet; fall back a year unless one was named.
            if (rows.Count == 0 && request.Year is null)
            {
                anchorYear -= 1;
                rows = await FetchYearAsync(request.SiteId, anchorYear, request.Pollutant);
            }

            if (rows.Count == 0)
                return ObservationsResult.Empty(request);

            var window = request.Window;
            if (window is not null)
            {
                rows = await ApplyWindowAsync(rows, request, anchorYear, window.Value);
                if (rows.Count == 0)
                    return ObservationsResult.Empty(request);
            }

            var items = request.Aggregation == ObservationsAggregation.Daily
                ? AggregateDaily(rows)
                : ToHourly(rows);

            var timestamps = items.Select(i => i.Timestamp).ToList();

            return new ObservationsResult
            {
                SiteId = request.SiteId,
                Period = request.Period,
                Aggregation = request.Aggregation.ToString().ToLowerInvariant(),
                From = timestamps.Count > 0 ? timestamps.Min() : null,
                To = timestamps.Count > 0 ? timestamps.Max() : null,
                Pollutants = items.Select(i => i.Pollutant).Distinct().OrderBy(p => p).ToList(),
                Count = items.Count,
                Observations = items
            };
        }

        private async Task<List<FinalData>> FetchYearAsync(string siteId, int year, string? pollutant)
        {
            var rows = await atomHourlyFetchService.GetAtomHourlydatafetch(
                siteId, year.ToString(CultureInfo.InvariantCulture), pollutant ?? string.Empty);

            logger.LogInformation(
                "Observations fetch for site {SiteId} year {Year} returned {RowCount} rows", siteId, year, rows.Count);

            return rows;
        }

        /// <summary>
        /// Trims to the requested window, anchored on the latest reading present in the feed rather
        /// than wall-clock now, because the Defra feeds lag. Pulls the preceding year's feed as well
        /// when the window crosses 1 January.
        /// </summary>
        private async Task<List<FinalData>> ApplyWindowAsync(
            List<FinalData> rows, ObservationsRequest request, int anchorYear, TimeSpan window)
        {
            var anchor = rows.Select(r => ParseTimestamp(r.StartTime)).Where(t => t.HasValue).Max();
            if (anchor is null)
                return [];

            var start = anchor.Value - window;

            if (start < new DateTime(anchorYear, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            {
                var previous = await FetchYearAsync(request.SiteId, anchorYear - 1, request.Pollutant);
                rows = [.. previous, .. rows];
            }

            return rows
                .Where(r =>
                {
                    var t = ParseTimestamp(r.StartTime);
                    return t.HasValue && t.Value > start && t.Value <= anchor.Value;
                })
                .ToList();
        }

        private static List<ObservationItem> ToHourly(List<FinalData> rows) =>
            rows
                .Select(r => new { Row = r, Timestamp = ParseTimestamp(r.StartTime) })
                .Where(x => x.Timestamp.HasValue)
                .OrderBy(x => x.Timestamp!.Value)
                .ThenBy(x => x.Row.PollutantName)
                .Select(x => new ObservationItem
                {
                    Timestamp = Format(x.Timestamp!.Value),
                    Pollutant = x.Row.PollutantName ?? string.Empty,
                    Value = ParseValue(x.Row.Value),
                    Status = MapStatus(x.Row.Verification)
                })
                .ToList();

        private static List<ObservationItem> AggregateDaily(List<FinalData> rows) =>
            rows
                .Select(r => new { Row = r, Timestamp = ParseTimestamp(r.StartTime) })
                .Where(x => x.Timestamp.HasValue)
                .GroupBy(x => new { Date = x.Timestamp!.Value.Date, Pollutant = x.Row.PollutantName })
                .OrderBy(g => g.Key.Date)
                .ThenBy(g => g.Key.Pollutant)
                .Select(g =>
                {
                    var values = g.Select(x => ParseValue(x.Row.Value))
                        .Where(v => v.HasValue)
                        .Select(v => v!.Value)
                        .ToList();
                    var capture = g.Any() ? (decimal)values.Count / g.Count() : 0m;
                    var sufficient = capture >= MinimumDailyCapture;

                    return new ObservationItem
                    {
                        Timestamp = g.Key.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Pollutant = g.Key.Pollutant ?? string.Empty,
                        // Null rather than zero below the capture threshold, so the caller can tell
                        // "not enough data" apart from a genuine reading of zero.
                        Value = sufficient ? Math.Round(values.Average(), 2) : null,
                        Capture = Math.Round(capture, 2)
                    };
                })
                .ToList();

        private static DateTime? ParseTimestamp(string? value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : null;

        private static string Format(DateTime value) =>
            value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        private static decimal? ParseValue(string? value) =>
            value is not null
            && value != "-99"
            && decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;

        private static string? MapStatus(string? verification) => verification switch
        {
            "1" => "V",
            "2" => "P",
            "3" => "N",
            _ => null
        };
    }
}
