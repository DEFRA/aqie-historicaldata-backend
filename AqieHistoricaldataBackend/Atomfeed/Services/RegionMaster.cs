namespace AqieHistoricaldataBackend.Atomfeed.Models
{
    public static class RegionMaster
    {
        // RegionId -> RegionName master mapping
        public static readonly IReadOnlyDictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = "Northern Ireland",
                ["3"] = "North East Scotland",
                ["4"] = "North Wales",
                ["5"] = "Highland",
                ["6"] = "Central Scotland",
                ["7"] = "Eastern",
                ["8"] = "South East",
                ["9"] = "South Wales",
                ["10"] = "North West And Merseyside",
                ["11"] = "South West",
                ["12"] = "East Midlands",
                ["13"] = "Scottish Borders",
                ["14"] = "North East",
                ["15"] = "Greater London",
                ["16"] = "West Midlands",
                ["17"] = "Yorkshire And Humberside",
                ["18"] = "Isle of Man",
            };

        public static string Resolve(string? regionId) =>
            !string.IsNullOrWhiteSpace(regionId) && Map.TryGetValue(regionId.Trim(), out var name)
                ? name
                : "Not found in provided mapping";
    }
}