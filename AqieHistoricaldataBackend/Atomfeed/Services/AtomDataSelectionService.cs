using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionService(ILogger<HistoryexceedenceService> Logger,
    IAtomDataSelectionStationService AtomDataSelectionStationService) : IAtomDataSelectionService
    {
        public async Task<object> GetatomDataSelectiondata(QueryStringData data)
        {
            try
            {
                var stationresultcountData = await AtomDataSelectionStationService.GetAtomDataSelectionStation(data);
                return stationresultcountData;
            }
            catch (Exception ex)
            {
                // Log the error message and stack trace as two separate LogError invocations
                Logger.LogError(ex, "Error in Atom atomDataSelectiondata");
                return "Failure";
            }
        }
    }
}
