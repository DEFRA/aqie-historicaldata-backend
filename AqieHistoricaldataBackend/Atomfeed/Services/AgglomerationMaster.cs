namespace AqieHistoricaldataBackend.Atomfeed.Models
{
    public static class AgglomerationMaster
    {
        // AgglomerationID -> AgglomerationRegion master mapping
        public static readonly IReadOnlyDictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = "Northern Ireland",
                ["2"] = "South East",
                ["3"] = "North West & Merseyside",
                ["4"] = "East Midlands",
                ["5"] = "South West",
                ["6"] = "Scotland",
                ["7"] = "North East",
                ["8"] = "North East",
                ["9"] = "Greater London",
                ["10"] = "Eastern",
                ["11"] = "Wales",
                ["12"] = "Wales",
                ["13"] = "South West",
                ["14"] = "South East",
                ["15"] = "South East",
                ["16"] = "North West & Merseyside",
                ["17"] = "North West & Merseyside",
                ["18"] = "North West & Merseyside",
                ["19"] = "North West & Merseyside",
                ["20"] = "East Midlands",
                ["21"] = "Scotland",
                ["22"] = "West Midlands",
                ["23"] = "West Midlands",
                ["24"] = "South East",
                ["25"] = "West Midlands",
                ["26"] = "Yorkshire & Humberside",
                ["27"] = "Yorkshire & Humberside",
                ["28"] = "Yorkshire & Humberside",
            };

        public static string Resolve(string? agglomerationId) =>
            !string.IsNullOrWhiteSpace(agglomerationId) && Map.TryGetValue(agglomerationId.Trim(), out var name)
                ? name
                : "Not found in provided mapping";
    }
}