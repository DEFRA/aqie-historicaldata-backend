using System.Globalization;
using System.Text.Json.Serialization;

namespace AqieHistoricaldataBackend.Atomfeed.Models
{
    public enum ObservationsAggregation
    {
        Hourly,
        Daily
    }

    public sealed class ObservationsRequest
    {
        public required string SiteId { get; init; }
        public string? Pollutant { get; init; }
        public required string Period { get; init; }
        public ObservationsAggregation Aggregation { get; init; }
        public int? Year { get; init; }

        /// <summary>Null means "no trimming" — the whole requested year is returned.</summary>
        public TimeSpan? Window { get; init; }

        public static bool TryCreate(
            string? siteId,
            string? pollutant,
            string? period,
            string? aggregation,
            string? year,
            IReadOnlyCollection<string> knownPollutants,
            out ObservationsRequest? request,
            out string? error)
        {
            request = null;
            error = null;

            if (string.IsNullOrWhiteSpace(siteId))
            {
                error = "siteId is required.";
                return false;
            }

            period = string.IsNullOrWhiteSpace(period) ? "7days" : period.Trim().ToLowerInvariant();
            TimeSpan? window = period switch
            {
                "24hours" => TimeSpan.FromHours(24),
                "7days" => TimeSpan.FromDays(7),
                "30days" => TimeSpan.FromDays(30),
                "year" => null,
                _ => TimeSpan.MinValue
            };

            if (window == TimeSpan.MinValue)
            {
                error = "period must be one of: 24hours, 7days, 30days, year.";
                return false;
            }

            aggregation = string.IsNullOrWhiteSpace(aggregation) ? "hourly" : aggregation.Trim().ToLowerInvariant();
            if (aggregation is not ("hourly" or "daily"))
            {
                error = "aggregation must be one of: hourly, daily.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(pollutant)
                && !knownPollutants.Contains(pollutant, StringComparer.OrdinalIgnoreCase))
            {
                error = $"pollutant must be one of: {string.Join(", ", knownPollutants)}.";
                return false;
            }

            int? parsedYear = null;
            if (!string.IsNullOrWhiteSpace(year))
            {
                if (!int.TryParse(year, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                    || y < 1960 || y > DateTime.UtcNow.Year)
                {
                    error = $"year must be a number between 1960 and {DateTime.UtcNow.Year}.";
                    return false;
                }

                parsedYear = y;
            }

            var matched = knownPollutants.FirstOrDefault(
                p => string.Equals(p, pollutant, StringComparison.OrdinalIgnoreCase));

            request = new ObservationsRequest
            {
                SiteId = siteId.Trim(),
                Pollutant = matched,
                Period = period,
                Aggregation = aggregation == "daily" ? ObservationsAggregation.Daily : ObservationsAggregation.Hourly,
                Year = parsedYear,
                Window = window
            };

            return true;
        }
    }

    public sealed class ObservationItem
    {
        public required string Timestamp { get; init; }
        public required string Pollutant { get; init; }

        /// <summary>Null where the feed reported no data, or daily capture was below threshold.</summary>
        public decimal? Value { get; init; }

        /// <summary>Verification flag: V, P or N. Hourly only.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Status { get; init; }

        /// <summary>Proportion of the day with readings. Daily only.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Capture { get; init; }
    }

    public sealed class ObservationsResult
    {
        public required string SiteId { get; init; }
        public required string Period { get; init; }
        public required string Aggregation { get; init; }
        public string? From { get; init; }
        public string? To { get; init; }
        public required IReadOnlyList<string> Pollutants { get; init; }
        public int Count { get; init; }
        public required IReadOnlyList<ObservationItem> Observations { get; init; }

        public static ObservationsResult Empty(ObservationsRequest request) => new()
        {
            SiteId = request.SiteId,
            Period = request.Period,
            Aggregation = request.Aggregation.ToString().ToLowerInvariant(),
            Pollutants = [],
            Count = 0,
            Observations = []
        };
    }
}
