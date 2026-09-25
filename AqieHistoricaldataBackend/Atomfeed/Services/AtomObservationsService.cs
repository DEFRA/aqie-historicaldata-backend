using System.Globalization;
using AqieHistoricaldataBackend.Atomfeed.Models;
using Microsoft.Extensions.Caching.Memory;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    /// <summary>Raised when the upstream Defra feed could not be read, as distinct from holding no data.</summary>
    public sealed class UpstreamFeedException(string message) : Exception(message);

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
        IAtomHourlyFetchService atomHourlyFetchService,
        IMemoryCache cache) : IAtomObservationsService
    {
        private const decimal MinimumDailyCapture = 0.75m;

        /// <summary>
        /// Short enough that a newly published hour appears promptly, long enough that the two
        /// calls a station page makes only parse the year's XML once.
        /// </summary>
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

        public async Task<ObservationsResult> GetObservationsAsync(ObservationsRequest request)
        {
            var anchorYear = request.Year ?? DateTime.UtcNow.Year;
            var rows = await FetchYearAsync(request, anchorYear);

            // The current year's feed may not be published yet; fall back a year unless one was named.
            if (rows.Count == 0 && request.Year is null)
            {
                anchorYear -= 1;
                rows = await FetchYearAsync(request, anchorYear);
            }

            DateTime? windowStart = null;
            DateTime? windowEnd = null;

            if (request.Window is not null && rows.Count > 0)
            {
                (rows, windowStart, windowEnd) = await ApplyWindowAsync(rows, request, anchorYear, request.Window.Value);
            }

            var items = request.Aggregation == ObservationsAggregation.Daily
                ? AggregateDaily(rows)
                : ToHourly(rows);

            if (items.Count == 0)
            {
                return new ObservationsResult
                {
                    SiteId = request.SiteId,
                    Network = request.Network,
                    Period = request.Period,
                    Aggregation = request.Aggregation.ToString().ToLowerInvariant(),
                    Anchor = request.Anchor.ToString().ToLowerInvariant(),
                    WindowFrom = windowStart.HasValue ? Format(windowStart.Value) : null,
                    WindowTo = windowEnd.HasValue ? Format(windowEnd.Value) : null,
                    Pollutants = [],
                    Count = 0,
                    Observations = []
                };
            }

            var timestamps = items.Select(i => i.Timestamp).ToList();

            return new ObservationsResult
            {
                SiteId = request.SiteId,
                Network = request.Network,
                Period = request.Period,
                Aggregation = request.Aggregation.ToString().ToLowerInvariant(),
                Anchor = request.Anchor.ToString().ToLowerInvariant(),
                WindowFrom = windowStart.HasValue ? Format(windowStart.Value) : null,
                WindowTo = windowEnd.HasValue ? Format(windowEnd.Value) : null,
                From = timestamps.Min(),
                To = timestamps.Max(),
                Pollutants = items.Select(i => i.Pollutant).Distinct().OrderBy(p => p).ToList(),
                Count = items.Count,
                Observations = items
            };
        }

        private async Task<List<FinalData>> FetchYearAsync(ObservationsRequest request, int year)
        {
            var key = $"obs::{request.Network}::{request.SiteId}::{year}::{request.Pollutant ?? "all"}";

            if (cache.TryGetValue(key, out List<FinalData>? cached) && cached is not null)
            {
                logger.LogInformation("Observations cache hit for {CacheKey}", key);
                return cached;
            }

            var outcome = await atomHourlyFetchService.GetAtomHourlydatafetchWithStatus(
                request.SiteId,
                year.ToString(CultureInfo.InvariantCulture),
                request.Pollutant ?? string.Empty,
                request.Network);

            if (outcome.UpstreamFailed)
            {
                throw new UpstreamFeedException(
                    $"Upstream Defra feed failed for site {request.SiteId} year {year}.");
            }

            logger.LogInformation(
                "Observations fetch for site {SiteId} year {Year} returned {RowCount} rows",
                request.SiteId, year, outcome.Rows.Count);

            cache.Set(key, outcome.Rows, CacheDuration);
            return outcome.Rows;
        }

        /// <summary>
        /// Trims to the requested window. With <see cref="ObservationsAnchor.Latest"/> the window
        /// ends at the most recent reading present, because the Defra feeds lag; with
        /// <see cref="ObservationsAnchor.Now"/> it ends at the current time, which lets a caller
        /// report genuine trailing data capture. Pulls the preceding year's feed as well when the
        /// window crosses 1 January.
        /// </summary>
        private async Task<(List<FinalData> Rows, DateTime? Start, DateTime? End)> ApplyWindowAsync(
            List<FinalData> rows, ObservationsRequest request, int anchorYear, TimeSpan window)
        {
            DateTime? anchor = request.Anchor == ObservationsAnchor.Now
                ? DateTime.UtcNow
                : rows.Select(r => ParseTimestamp(r.StartTime)).Where(t => t.HasValue).Max();

            if (anchor is null)
                return ([], null, null);

            var start = anchor.Value - window;

            if (start < new DateTime(anchorYear, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            {
                var previous = await FetchYearAsync(request, anchorYear - 1);
                rows = [.. previous, .. rows];
            }

            var trimmed = rows
                .Where(r =>
                {
                    var t = ParseTimestamp(r.StartTime);
                    return t.HasValue && t.Value > start && t.Value <= anchor.Value;
                })
                .ToList();

            return (trimmed, start, anchor.Value);
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
                    PollutantCode = ObservationsPollutants.CodeFor(x.Row.PollutantName),
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
                        PollutantCode = ObservationsPollutants.CodeFor(g.Key.Pollutant),
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
