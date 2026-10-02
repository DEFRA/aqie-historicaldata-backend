using Amazon.Runtime;
using AqieHistoricaldataBackend.Atomfeed.Models;
using AqieHistoricaldataBackend.Utils.Mongo;
using MongoDB.Driver;
using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    /// <summary>
    /// Helper containing site filtering / retrieval logic used by <see cref="AtomDataSelectionStationService"/>.
    /// </summary>
    public static class AtomSiteFilterHelper
    {
        private static readonly HashSet<string> NonAurnPollutants =
        [
            "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20",
            "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31",
            "32", "33", "34", "35", "41", "42", "43", "168", "169", "170",
            "171", "172", "173", "174", "175", "176", "177", "178", "179"
        ];

        public static async Task<(string? DataSource, List<SiteInfo> Sites)> ResolveSitesAsync(
            QueryStringData queryStringData,
            string pollutantName,
            string resolvedPollutantName,
            IAuthService authService,
            IHttpClientFactory httpClientFactory,
            ILogger logger,
            IMongoDbClientFactory mongoDbClientFactory)
        {
            string? datasource = queryStringData.dataSource;
            string? networkId = queryStringData.networkId;
            var sites = new List<SiteInfo>();

            if (datasource == "AURN" && NonAurnPollutants.Contains(pollutantName))
            {
                datasource = "NON-AURN";
                networkId = "10";
            }

            if (datasource == "AURN")
            {
                var token = await authService.GetRicardoToken();
                var sitemetadatainfo = await RicardoSiteMetadata.FetchSiteMetadata(httpClientFactory, token);
                sites = AtomSiteFilterHelper.FilterSitesByPollutants(sitemetadatainfo, resolvedPollutantName, logger);
            }
            else if (datasource == "NON-AURN")
            {
                sites = await AtomSiteFilterHelper.GetSiteInfoAsync(pollutantName, networkId ?? string.Empty, mongoDbClientFactory);

                // networkId 10 represents AURN within the NON-AURN data source
                if (networkId == "10")
                {
                    datasource = "AURN";
                }
            }

            return (datasource, sites);
        }

        internal static object BuildCountResult(List<SiteInfo> stationData, string? datasource)
        {
            if (datasource == "AURN")
            {
                return stationData.Count.ToString();
            }

            var networkTypeCounts = stationData
                .GroupBy(s => s.NetworkType ?? "Unknown")
                .Select(g => new { NetworkType = g.Key, Count = g.Count() })
                .ToList();

            return networkTypeCounts.Count > 0
                ? networkTypeCounts
                : new[] { new { NetworkType = "Unknown", Count = stationData.Count } }.ToList();
        }
        public static List<SiteInfo> FilterSitesByPollutants(List<SiteInfo> sites, string pollutantName, ILogger logger)
        {
            var mappedPollutants = GetMappedPollutants(pollutantName, logger, includeUnknowns: true);

            return sites
                .Select(site => new SiteInfo
                {
                    SiteName = site.SiteName,
                    LocalSiteId = site.LocalSiteId,
                    AreaType = site.AreaType,
                    SiteType = site.SiteType,
                    RegionId = site.RegionId,
                    ZoneRegion = site.ZoneRegion,
                    Latitude = site.Latitude,
                    Longitude = site.Longitude,
                    Pollutants = (site.Pollutants ?? new List<PollutantInfo>())
                        .Where(p => p.Name != null && mappedPollutants
                            .Any(tp => p.Name.Contains(tp, StringComparison.OrdinalIgnoreCase)))
                        .ToList()
                })
                .Where(site => site.Pollutants?.Count > 0)
                .GroupBy(site => site.LocalSiteId)
                .Select(g => g.First())
                .ToList();
        }

        public static List<SiteInfo> FilterSitesByRegionId(List<SiteInfo> filteredSites, string? regionId)
        {
            if (filteredSites == null || filteredSites.Count == 0)
            {
                return new List<SiteInfo>();
            }

            if (string.IsNullOrWhiteSpace(regionId))
            {
                return filteredSites;
            }

            // supports single or comma separated region ids e.g. "1,3,7"
            var regionIds = regionId
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return filteredSites
                .Where(site => site.RegionId != null && regionIds.Contains(site.RegionId.Trim()))
                .ToList();
        }

        public static List<SiteInfo> FilterSitesByYearRanges(List<SiteInfo> sites, string year)
        {
            var yearRanges = ParseYearRanges(year);

            return sites
                .Where(site =>
                    site.Pollutants != null &&
                    site.Pollutants.Any(p => IsPollutantInYearRange(p, yearRanges)))
                .ToList();
        }

        public static async Task<List<SiteInfo>> GetSiteInfoAsync(
             string pollutantName, string networkId, IMongoDbClientFactory MongoDbClientFactory)
        {
            var siteCollection = MongoDbClientFactory.GetCollection<StationDetailDocument>("aqie_atom_non_aurn_networks_station_details");

            var pollutantIds = pollutantName
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var networkIds = networkId
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var pollutantFilter = Builders<StationDetailDocument>.Filter
                .In(d => d.pollutantID, pollutantIds);

            var combinedFilter = networkIds.Count > 0
                ? Builders<StationDetailDocument>.Filter.And(
                    pollutantFilter,
                    Builders<StationDetailDocument>.Filter.In(d => d.NetworkID, networkIds))
                : pollutantFilter;

            var documents = await siteCollection.Find(combinedFilter).ToListAsync();

            return documents
                .GroupBy(d => new { d.SiteID, d.NetworkID })
                .Select(g =>
                {
                    var first = g.First();
                    var (areaType, siteType) = SplitEnvironmentType(first.EnvironmentType);
                    return new SiteInfo
                    {
                        LocalSiteId = first.SiteID,
                        SiteName = first.SiteName,
                        AreaType = areaType,
                        SiteType = siteType,
                        Latitude = first.Latitude,
                        Longitude = first.Longitude,
                        NetworkType = first.NetworkType,
                        ZoneRegion = first.Region,
                        Pollutants = g
                            .Where(d => d.PollutantName != null)
                            .Select(d => new PollutantInfo
                            {
                                Name = d.PollutantName,
                                StartDate = d.StartDate,
                                EndDate = d.EndDate
                            })
                            .ToList()
                    };
                })
                .ToList();
        }

        internal static (string? AreaType, string? SiteType) SplitEnvironmentType(string? environmentType)
        {
            if (string.IsNullOrWhiteSpace(environmentType))
                return (null, null);

            var trimmed = environmentType.Trim();

            var lastSpace = trimmed.LastIndexOf(' ');
            if (lastSpace < 0)
                return (null, trimmed); // single word — treat as SiteType only

            return (
                trimmed[..lastSpace].Trim(),
                trimmed[(lastSpace + 1)..].Trim()
            );
        }

        internal static List<(DateTime Start, DateTime End)> ParseYearRanges(string year)
        {
            var yearInts = year.Split(',')
                .Select(y => int.TryParse(y, out int val) ? val : (int?)null)
                .Where(y => y.HasValue)
                .Select(y => y!.Value)
                .ToList();

            return yearInts
                .Select(y => (
                    Start: new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    End: new DateTime(y, 12, 31, 0, 0, 0, DateTimeKind.Utc)))
                .ToList();
        }

        internal static bool IsPollutantInYearRange(PollutantInfo pollutant, List<(DateTime Start, DateTime End)> yearRanges)
        {
            if (!DateTime.TryParseExact(pollutant.StartDate, "dd/MM/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime startDate))
                return false;

            bool hasEndDate = DateTime.TryParseExact(pollutant.EndDate, "dd/MM/yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTime endDate);

            return yearRanges.Any(range =>
                hasEndDate
                    ? startDate <= range.End && endDate >= range.Start
                    : startDate <= range.End);
        }

        internal static List<string> GetMappedPollutants(string pollutantName, ILogger logger, bool includeUnknowns = false)
        {
            var result = new List<string>();
            var names = pollutantName.Split(',');

            var pollutantMap = new Dictionary<string, List<string>>
            {
                { "Ozone", new List<string> { "Ozone" } },
                { "Fine particulate matter (PM2.5)", new List<string>
                    {
                        "PM<sub>2.5</sub> (Hourly measured)",
                        "Volatile PM<sub>2.5</sub> (Hourly measured)",
                        "Non-volatile PM<sub>2.5</sub> (Hourly measured)",
                        "PM<sub>2.5</sub> particulate matter (Hourly measured)"
                    }
                },
                { "Particulate matter (PM10)", new List<string>
                    {
                        "PM<sub>10</sub> (Hourly measured)",
                        "Volatile PM<sub>10</sub> (Hourly measured)",
                        "Non-volatile PM<sub>10</sub> (Hourly measured)",
                        "PM<sub>10</sub> particulate matter (Hourly measured)"
                    }
                },
                { "NO2", new List<string> { "Nitrogen dioxide" } },
                { "CO", new List<string> { "Carbon monoxide" } },
                { "SO2", new List<string> { "Sulphur dioxide" } },
                { "NOx", new List<string> { "Nitrogen oxides as nitrogen dioxide" } },
                { "NO", new List<string> { "Nitric oxide" } }
            };

            foreach (var name in names)
            {
                var trimmedName = name.Trim();
                if (pollutantMap.TryGetValue(trimmedName, out var mappedNames))
                {
                    result.AddRange(mappedNames);
                }
                else if (includeUnknowns)
                {
                    result.Add(trimmedName);
                }
                else
                {
                    // Defensive branch: only reachable if this method is called with
                    // includeUnknowns: false and an unknown pollutant key.
                    // Current callers always pass includeUnknowns: true, so this is
                    // an intentional dead-code guard.
                    [ExcludeFromCodeCoverage]
                    static void Warn(ILogger l, string n) =>
                        l.LogWarning("Unknown pollutant '{PollutantName}' not found in map.", n);
                    Warn(logger, trimmedName);
                }
            }

            return result;
        }
        internal static async Task<string> ResolvePollutantNameAsync(string pollutantName, ILogger logger, IMongoDbClientFactory MongoDbClientFactory)
        {
            var pollutantIds = pollutantName
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var pollutantCollection = MongoDbClientFactory.GetCollection<PollutantMasterDocument>(
                "aqie_atom_non_aurn_networks_pollutant_master");

            var idFilter = Builders<PollutantMasterDocument>.Filter
                .In(d => d.pollutantID, pollutantIds);

            var matchedPollutants = await pollutantCollection.Find(idFilter).ToListAsync();

            var resolvedPollutantName = string.Join(",",
                matchedPollutants
                    .Where(p => !string.IsNullOrEmpty(p.pollutantName))
                    .Select(p => p.pollutantName!));

            logger.LogInformation("Resolved pollutant names: {Names}", resolvedPollutantName);
            return resolvedPollutantName;
        }
    }
}