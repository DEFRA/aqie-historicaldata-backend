using AqieHistoricaldataBackend.Utils.Mongo;
using MongoDB.Driver;
using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionPollutantDataSource(
            ILogger<HistoryexceedenceService> Logger,
            IMongoDbClientFactory MongoDbClientFactory) : IAtomDataSelectionPollutantDataSource
    {
        private static readonly HashSet<string> AurnPollutantIds = ["36", "37", "38", "39", "40", "44", "45", "46"];

        private static readonly List<string> AurnHardcodedSources =
        [
            "Near real-time data from Defra",
            "Automatic Urban and Rural Network (AURN)"
        ];

        private const string OtherDataFromDefra = "Other data from Defra";

        [ExcludeFromCodeCoverage]
        public async Task<dynamic> GetAtomPollutantDataSource(QueryStringData data)
        {
            try
            {
                var siteCollection = MongoDbClientFactory.GetCollection<StationDetailDocument>("aqie_atom_non_aurn_networks_station_details");

                var pollutantIds = data.pollutantId?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    ?? [];

                var filter = Builders<StationDetailDocument>.Filter.In(x => x.pollutantID, pollutantIds);

                var dataSourceList = await siteCollection
                    .Find(filter)
                    .Project(x => x.NetworkType)
                    .ToListAsync();

                var hasAurnPollutants = pollutantIds.Any(AurnPollutantIds.Contains);
                var dbResults = dataSourceList.Where(x => x is not null).Distinct().ToList();
                var hasResults = dbResults.Count > 0;

                IEnumerable<string> prefixSources;
                if (hasAurnPollutants)
                {
                    prefixSources = [.. AurnHardcodedSources, OtherDataFromDefra];
                }
                else if (hasResults)
                {
                    prefixSources = [OtherDataFromDefra];
                }
                else
                {
                    prefixSources = [];
                }

                var finalList = prefixSources
                    .Concat(dbResults!)
                    .Distinct()
                    .ToList();

                return finalList;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in Atom GetAtomPollutantDataSource");
                return "Failure";
            }
        }
    }
}
