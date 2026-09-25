using System.Globalization;
using System.Text.Json.Serialization;

namespace AqieHistoricaldataBackend.Atomfeed.Models
{
    public enum ObservationsAggregation
    {
        Hourly,
        Daily
    }

    /// <summary>
    /// Anchors the requested window. <see cref="Latest"/> ends at the most recent reading in the
    /// feed, <see cref="Now"/> ends at the current time so a caller can report true trailing
    /// data capture.
    /// </summary>
    public enum ObservationsAnchor
    {
        Latest,
        Now
    }

    /// <summary>Display name paired with a stable code the consumer can safely switch on.</summary>
    public sealed record PollutantDefinition(string Name, string Code)
    {
        /// <summary>
        /// Names the Ricardo station metadata uses for the same measurement. It HTML-encodes
        /// subscripts and splits PM into instrument variants; only the hourly-measured ones are
        /// listed, matching the download journey's mapping.
        /// </summary>
        public IReadOnlyList<string> UpstreamAliases { get; init; } = [];
    }

    public static class ObservationsPollutants
    {
        public static readonly IReadOnlyList<PollutantDefinition> All =
        [
            new("Nitrogen dioxide", "NO2"),
            new("PM10", "PM10")
            {
                UpstreamAliases =
                [
                    "PM<sub>10</sub> (Hourly measured)",
                    "Volatile PM<sub>10</sub> (Hourly measured)",
                    "Non-volatile PM<sub>10</sub> (Hourly measured)",
                    "PM<sub>10</sub> particulate matter (Hourly measured)"
                ]
            },
            new("PM2.5", "PM25")
            {
                UpstreamAliases =
                [
                    "PM<sub>2.5</sub> (Hourly measured)",
                    "Volatile PM<sub>2.5</sub> (Hourly measured)",
                    "Non-volatile PM<sub>2.5</sub> (Hourly measured)",
                    "PM<sub>2.5</sub> particulate matter (Hourly measured)"
                ]
            },
            new("Ozone", "O3"),
            new("Sulphur dioxide", "SO2")
        ];

        public static IReadOnlyList<string> Names { get; } = All.Select(p => p.Name).ToList();

        /// <summary>Resolves a display name, a code, or an upstream alias, case-insensitively.</summary>
        public static PollutantDefinition? Resolve(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            return All.FirstOrDefault(p =>
                string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Code, trimmed, StringComparison.OrdinalIgnoreCase)
                || p.UpstreamAliases.Any(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase)));
        }

        public static string CodeFor(string? name) => All
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Code
            ?? string.Empty;
    }

    public sealed class ObservationsRequest
    {
        public required string SiteId { get; init; }
        public string? Pollutant { get; init; }
        public required string Period { get; init; }
        public ObservationsAggregation Aggregation { get; init; }
        public ObservationsAnchor Anchor { get; init; }
        public int? Year { get; init; }

        /// <summary>`AURN` or `NON-AURN`; selects the auto/ or non-auto/ upstream feed path.</summary>
        public required string Network { get; init; }

        /// <summary>Null means "no trimming" — the whole requested year is returned.</summary>
        public TimeSpan? Window { get; init; }

        public static bool TryCreate(
            string? siteId,
            string? pollutant,
            string? period,
            string? aggregation,
            string? year,
            string? network,
            string? anchor,
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

            PollutantDefinition? matched = null;
            if (!string.IsNullOrWhiteSpace(pollutant))
            {
                matched = ObservationsPollutants.Resolve(pollutant);
                if (matched is null)
                {
                    error = "pollutant must be one of: "
                          + string.Join(", ", ObservationsPollutants.All.Select(p => $"{p.Name} ({p.Code})"))
                          + ".";
                    return false;
                }
            }

            // A year of hourly rows for every pollutant is ~43,800 objects; refuse rather than ship it.
            if (period == "year" && aggregation == "hourly" && matched is null)
            {
                error = "period=year with aggregation=hourly requires a pollutant. "
                      + "Use aggregation=daily, or request one pollutant at a time.";
                return false;
            }

            network = string.IsNullOrWhiteSpace(network) ? "AURN" : network.Trim().ToUpperInvariant();
            if (network is not ("AURN" or "NON-AURN"))
            {
                error = "network must be one of: AURN, NON-AURN.";
                return false;
            }

            anchor = string.IsNullOrWhiteSpace(anchor) ? "latest" : anchor.Trim().ToLowerInvariant();
            if (anchor is not ("latest" or "now"))
            {
                error = "anchor must be one of: latest, now.";
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

            request = new ObservationsRequest
            {
                SiteId = siteId.Trim(),
                Pollutant = matched?.Name,
                Period = period,
                Aggregation = aggregation == "daily" ? ObservationsAggregation.Daily : ObservationsAggregation.Hourly,
                Anchor = anchor == "now" ? ObservationsAnchor.Now : ObservationsAnchor.Latest,
                Network = network,
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

        /// <summary>Stable identifier for the pollutant; prefer this over the display name.</summary>
        public required string PollutantCode { get; init; }

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
        public required string Network { get; init; }
        public required string Period { get; init; }
        public required string Aggregation { get; init; }
        public required string Anchor { get; init; }

        /// <summary>Bounds of the requested window, whether or not readings exist across all of it.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WindowFrom { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WindowTo { get; init; }

        /// <summary>Earliest and latest timestamps actually present in <see cref="Observations"/>.</summary>
        public string? From { get; init; }
        public string? To { get; init; }

        public required IReadOnlyList<string> Pollutants { get; init; }
        public int Count { get; init; }
        public required IReadOnlyList<ObservationItem> Observations { get; init; }

        public static ObservationsResult Empty(ObservationsRequest request) => new()
        {
            SiteId = request.SiteId,
            Network = request.Network,
            Period = request.Period,
            Aggregation = request.Aggregation.ToString().ToLowerInvariant(),
            Anchor = request.Anchor.ToString().ToLowerInvariant(),
            Pollutants = [],
            Count = 0,
            Observations = []
        };
    }

    public sealed class ObservationStation
    {
        public required string SiteId { get; init; }
        public required string Name { get; init; }
        public required string Network { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Region { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AreaType { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SiteType { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NetworkType { get; init; }

        public required IReadOnlyList<ObservationStationPollutant> Pollutants { get; init; }
    }

    public sealed class ObservationStationPollutant
    {
        public required string Name { get; init; }

        /// <summary>Empty when the pollutant is outside the five this API serves observations for.</summary>
        public required string Code { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StartDate { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EndDate { get; init; }
    }

    public sealed class ObservationStationsResult
    {
        public required string Network { get; init; }
        public int Count { get; init; }
        public required IReadOnlyList<ObservationStation> Stations { get; init; }
    }
}
