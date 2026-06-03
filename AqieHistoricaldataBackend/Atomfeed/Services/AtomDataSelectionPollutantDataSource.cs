using AqieHistoricaldataBackend.Utils.Mongo;
using MongoDB.Driver;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionPollutantDataSource(
            ILogger<HistoryexceedenceService> Logger,
            IMongoDbClientFactory MongoDbClientFactory) : IAtomDataSelectionPollutantDataSource
    {
        private static readonly HashSet<string> AurnPollutantIds = ["36", "37", "38", "39", "40", "44", "45", "46"];

        private const string AurnCategory = "Near real-time data from Defra";
        private const string AurnNetworkName = "Automatic Urban and Rural Network (AURN)";
        private const string OtherDataFromDefra = "Other data from Defra";

        public async Task<dynamic> GetAtomPollutantDataSource(QueryStringData data)
        {
            try
            {
                var siteCollection = MongoDbClientFactory.GetCollection<StationDetailDocument>("aqie_atom_non_aurn_networks_station_details");

                var pollutantIds = data.pollutantId?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    ?? [];

                var filter = Builders<StationDetailDocument>.Filter.In(x => x.pollutantID, pollutantIds);

                var rawResults = await siteCollection
                    .Find(filter)
                    .ToListAsync();

                var hasAurnPollutants = pollutantIds.Any(AurnPollutantIds.Contains);

                var dbNetworks = rawResults
                    .Where(x => x.NetworkType is not null)
                    .DistinctBy(x => x.NetworkType)
                    .Select(x => new
                    {
                        name = x.NetworkType!,
                        id   = int.TryParse(x.NetworkID, out var parsed) ? parsed : -2
                    })
                    .ToList<dynamic>();

                var hasResults = dbNetworks.Count > 0;
                var result = new List<dynamic>();

                if (hasAurnPollutants)
                {
                    result.Add(new
                    {
                        category = AurnCategory,
                        networks = (object)new List<string> { AurnNetworkName }
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
