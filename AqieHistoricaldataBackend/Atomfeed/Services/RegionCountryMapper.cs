using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    /// <summary>
    /// Maps a site's RegionId to its country using the region master data.
    /// </summary>
    public static class RegionCountryMapper
    {
        public const string UnknownCountry = "Unknown";

        private static readonly IReadOnlyDictionary<string, string> RegionIdToCountry =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = "Northern Ireland",   // Northern Ireland
                ["3"] = "Scotland",           // North East Scotland
                ["4"] = "Wales",              // North Wales
                ["5"] = "Scotland",           // Highland
                ["6"] = "Scotland",           // Central Scotland
                ["7"] = "England",            // Eastern
                ["8"] = "England",            // South East
                ["9"] = "Wales",              // South Wales
                ["10"] = "England",           // North West And Merseyside
                ["11"] = "England",           // South West
                ["12"] = "England",           // East Midlands
                ["13"] = "Scotland",          // Scottish Borders
                ["14"] = "England",           // North East
                ["15"] = "England",           // Greater London
                ["16"] = "England",           // West Midlands
                ["17"] = "England",           // Yorkshire And Humberside
                ["18"] = "Other"              // Isle of Man - Crown Dependency
            };

        /// <summary>
        /// Returns the country for the supplied region id, or <see cref="UnknownCountry"/> when not mapped.
        /// </summary>
        public static string GetCountryByRegionId(string? regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId))
            {
                return UnknownCountry;
            }

            return RegionIdToCountry.TryGetValue(regionId.Trim(), out var country)
                ? country
                : UnknownCountry;
        }

        /// <summary>
        /// Sets <see cref="SiteInfo.Country"/> from <see cref="SiteInfo.RegionId"/>.
        /// </summary>
        public static SiteInfo MapCountry(this SiteInfo site)
        {
            ArgumentNullException.ThrowIfNull(site);
            site.Country = GetCountryByRegionId(site.RegionId);
            return site;
        }

        /// <summary>
        /// Sets the country on every site in the collection.
        /// </summary>
        public static List<SiteInfo> MapCountriesByRegion(this List<SiteInfo>? sites)
        {
            if (sites is null || sites.Count == 0)
            {
                return sites ?? new List<SiteInfo>();
            }

            foreach (var site in sites)
            {
                site.MapCountry();
            }

            return sites;
        }
    }
}