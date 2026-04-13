using AqieHistoricaldataBackend.Utils.Mongo;
using MongoDB.Driver;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionPollutantMaster(
        ILogger<HistoryexceedenceService> Logger,
        IMongoDbClientFactory MongoDbClientFactory
                                                ) : IAtomDataSelectionPollutantMaster
    {
        public async Task<dynamic> GetPollutantMaster()
        {
            try
            {
                var pollutantCollection = MongoDbClientFactory.GetCollection<PollutantMasterDocument>("aqie_atom_non_aurn_networks_pollutant_master");

                var pollutantList = await pollutantCollection
                    .Find(_ => true)
                    .Project(x => new
                    {
                        x.pollutantID,
                        x.pollutantName,
                        x.pollutant_Abbreviation,
                        x.pollutant_value
                    })
                    .ToListAsync();

                if (pollutantList.Count != 0)
                {
                    return  pollutantList;
                }
                else
                {
                    Logger.LogWarning("Pollutant master collection is empty.");
                    return "Empty";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in Atom GetPollutantMaster");
                return "Failure";
            }
        }
    }
}
