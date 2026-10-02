using AqieHistoricaldataBackend.Utils.Mongo;
using MongoDB.Driver;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionPollutantDataSource(
            ILogger<HistoryexceedenceService> Logger,
            IMongoDbClientFactory MongoDbClientFactory) : IAtomDataSelectionPollutantDataSource
    {
        private static readonly HashSet<string> AurnPollutantIds =
            ["36", "37", "38", "39", "40", "44", "45", "46"];

        private static readonly HashSet<string> HydrocarbonPollutantIds =
            ["10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24",
             "25", "26", "27", "28", "29", "30", "31", "32", "33", "34", "35", "41", "42", "43",
             "168", "169", "170", "171", "172", "173", "174", "175", "176", "177", "178", "179"];

        private const string AurnCategory = "Near real-time data from Defra";
        private const string AurnNetworkName = "Automatic Urban and Rural Network (AURN)";
        private const string HydrocarbonNetworkName = "Automatic Hydrocarbon Network";
        private const string OtherDataFromDefra = "Other data from Defra";
        private const string ExcludedNetworkId = "10";

        public async Task<dynamic> GetAtomPollutantDataSource(QueryStringData data)
        {
            try
            {
                var siteCollection = MongoDbClientFactory.GetCollection<StationDetailDocument>("aqie_atom_non_aurn_networks_station_details");

                var pollutantIds = data.pollutantId?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    ?? [];

                var builder = Builders<StationDetailDocument>.Filter;

                var filter = builder.And(
                    builder.In(x => x.pollutantID, pollutantIds),
                    builder.Ne(x => x.NetworkID, ExcludedNetworkId));

                var rawResults = await siteCollection
                    .Find(filter)
                    .ToListAsync();

                var aurnPollutantIds = pollutantIds.Where(AurnPollutantIds.Contains).ToList();
                var hydrocarbonPollutantIds = pollutantIds.Where(HydrocarbonPollutantIds.Contains).ToList();

                var nearRealTimeNetworks = new List<dynamic>();

                if (aurnPollutantIds.Count > 0)
                {
                    nearRealTimeNetworks.Add(new
                    {
                        name = AurnNetworkName,
                        pollutantID = string.Join(",", aurnPollutantIds)
                    });
                }

                if (hydrocarbonPollutantIds.Count > 0)
                {
                    nearRealTimeNetworks.Add(new
                    {
                        name = HydrocarbonNetworkName,
                        pollutantID = string.Join(",", hydrocarbonPollutantIds)
                    });
                }

                var dbNetworks = rawResults
                    .Where(x => x.NetworkType is not null && x.pollutantID is not null)
                    .GroupBy(x => new { x.NetworkType, x.NetworkID })
                    .Select(g => new
                    {
                        name = g.Key.NetworkType!,
                        id = int.TryParse(g.Key.NetworkID, out var parsed) ? parsed : -2,
                        pollutantID = string.Join(",", g.Select(x => x.pollutantID).Distinct().OrderBy(p => p))
                    })
                    .ToList<dynamic>();

                var hasResults = dbNetworks.Count > 0;
                var result = new List<dynamic>();

                if (nearRealTimeNetworks.Count > 0)
                {
                    result.Add(new
                    {
                        category = AurnCategory,
                        networks = (object)nearRealTimeNetworks
                    });
                }

                if (hasResults)
                {
                    result.Add(new
                    {
                        category = OtherDataFromDefra,
                        networks = (object)dbNetworks
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in Atom GetAtomPollutantDataSource");
                return "Failure";
            }
        }
    }
}
