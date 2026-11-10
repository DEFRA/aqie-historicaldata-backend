using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    /// <summary>
    /// Maps a site's AgglomerationID to its country using the agglomeration master data.
    /// </summary>
    public static class AgglomerationCountryMapper
    {
        public const string UnknownCountry = "Unknown";

        private static readonly IReadOnlyDictionary<string, string> AgglomerationIdToCountry =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = "Northern Ireland",   // Northern Ireland
                ["2"] = "England",            // South East
                ["3"] = "England",            // North West & Merseyside
                ["4"] = "England",            // East Midlands
                ["5"] = "England",            // South West
                ["6"] = "Scotland",           // Scotland
                ["7"] = "England",            // North East
                ["8"] = "England",            // North East
                ["9"] = "England",            // Greater London
                ["10"] = "England",           // Eastern
                ["11"] = "Wales",             // Wales
                ["12"] = "Wales",             // Wales
                ["13"] = "England",           // South West
                ["14"] = "England",           // South East
                ["15"] = "England",           // South East
                ["16"] = "England",           // North West & Merseyside
                ["17"] = "England",           // North West & Merseyside
                ["18"] = "England",           // North West & Merseyside
                ["19"] = "England",           // North West & Merseyside
                ["20"] = "England",           // East Midlands
                ["21"] = "Scotland",          // Scotland
                ["22"] = "England",           // West Midlands
                ["23"] = "England",           // West Midlands
                ["24"] = "England",           // South East
                ["25"] = "England",           // West Midlands
                ["26"] = "England",           // Yorkshire & Humberside
                ["27"] = "England",           // Yorkshire & Humberside
                ["28"] = "England"            // Yorkshire & Humberside
            };

        /// <summary>
        /// Returns the country for the supplied agglomeration id, or <see cref="UnknownCountry"/> when not mapped.
        /// </summary>
        public static string GetCountryByAgglomerationId(string? agglomerationId)
        {
            if (string.IsNullOrWhiteSpace(agglomerationId))
            {
                return UnknownCountry;
            }

            return AgglomerationIdToCountry.TryGetValue(agglomerationId.Trim(), out var country)
                ? country
                : UnknownCountry;
        }

        /// <summary>
        /// Sets <see cref="SiteInfo.Country"/> from <see cref="SiteInfo.AgglomerationID"/>.
        /// </summary>
        public static SiteInfo MapCountryByAgglomeration(this SiteInfo site)
        {
            ArgumentNullException.ThrowIfNull(site);
            site.Country = GetCountryByAgglomerationId(site.AgglomerationID);
            return site;
        }

        /// <summary>
        /// Sets the country on every site in the collection.
        /// </summary>
        public static List<SiteInfo> MapCountriesByAgglomeration(this List<SiteInfo>? sites)
        {
            if (sites is null || sites.Count == 0)
            {
                return sites ?? new List<SiteInfo>();
            }

            foreach (var site in sites)
            {
                site.MapCountryByAgglomeration();
            }

            return sites;
        }
    }
}