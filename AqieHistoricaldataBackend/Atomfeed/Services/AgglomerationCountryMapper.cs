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
                ["1"] = "Northern Ireland",   // Belfast Metropolitan Urban Area
                ["2"] = "England",            // Reading/Wokingham Urban Area
                ["3"] = "England",            // Greater Manchester Urban Area
                ["4"] = "England",            // Nottingham Urban Area
                ["5"] = "England",            // Bournemouth Urban Area
                ["6"] = "Scotland",           // Glasgow Urban Area
                ["7"] = "England",            // Tyneside
                ["8"] = "England",            // Teesside Urban Area
                ["9"] = "England",            // Greater London Urban Area
                ["10"] = "England",           // Southend Urban Area
                ["11"] = "Wales",             // Swansea Urban Area
                ["12"] = "Wales",             // Cardiff Urban Area
                ["13"] = "England",           // Bristol Urban Area
                ["14"] = "England",           // Portsmouth Urban Area
                ["15"] = "England",           // Southampton Urban Area
                ["16"] = "England",           // Blackpool Urban Area
                ["17"] = "England",           // Liverpool Urban Area
                ["18"] = "England",           // Preston Urban Area
                ["19"] = "England",           // Birkenhead Urban Area
                ["20"] = "England",           // Leicester Urban Area
                ["21"] = "Scotland",          // Edinburgh Urban Area
                ["22"] = "England",           // The Potteries
                ["23"] = "England",           // West Midlands Urban Area
                ["24"] = "England",           // Brighton/Worthing/Littlehampton
                ["25"] = "England",           // Coventry/Bedworth
                ["26"] = "England",           // West Yorkshire Urban Area
                ["27"] = "England",           // Kingston upon Hull
                ["28"] = "England"            // Sheffield Urban Area
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