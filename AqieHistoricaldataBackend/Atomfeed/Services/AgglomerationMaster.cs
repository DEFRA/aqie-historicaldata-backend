namespace AqieHistoricaldataBackend.Atomfeed.Models
{
    public static class AgglomerationMaster
    {
        // AgglomerationID -> AgglomerationRegion master mapping
        public static readonly IReadOnlyDictionary<string, string> Map =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = "Belfast Metropolitan Urban Area",
                ["2"] = "Reading/Wokingham Urban Area",
                ["3"] = "Greater Manchester Urban Area",
                ["4"] = "Nottingham Urban Area",
                ["5"] = "Bournemouth Urban Area",
                ["6"] = "Glasgow Urban Area",
                ["7"] = "Tyneside",
                ["8"] = "Teesside Urban Area",
                ["9"] = "Greater London Urban Area",
                ["10"] = "Greater London Urban Area",
                ["11"] = "Swansea Urban Area",
                ["12"] = "Cardiff Urban Area",
                ["13"] = "Bristol Urban Area",
                ["14"] = "Portsmouth Urban Area",
                ["15"] = "Southampton Urban Area",
                ["16"] = "Blackpool Urban Area",
                ["17"] = "Liverpool Urban Area",
                ["18"] = "Preston Urban Area",
                ["19"] = "Birkenhead Urban Area",
                ["20"] = "Leicester Urban Area",
                ["21"] = "Edinburgh Urban Area",
                ["22"] = "The Potteries",
                ["23"] = "West Midlands Urban Area",
                ["24"] = "Brighton/Worthing/Littlehampton",
                ["25"] = "Coventry/Bedworth",
                ["26"] = "West Yorkshire Urban Area",
                ["27"] = "Kingston upon Hull",
                ["28"] = "Sheffield Urban Area",
            };

        public static string Resolve(string? agglomerationId) =>
            !string.IsNullOrWhiteSpace(agglomerationId) && Map.TryGetValue(agglomerationId.Trim(), out var name)
                ? name
                : "Not found in provided mapping";
    }
}