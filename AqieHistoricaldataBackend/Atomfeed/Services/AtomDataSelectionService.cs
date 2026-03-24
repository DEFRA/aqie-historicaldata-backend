using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionService(ILogger<HistoryexceedenceService> Logger,
    IAtomDataSelectionStationService AtomDataSelectionStationService) : IAtomDataSelectionService
    {
        public async Task<string> GetatomDataSelectiondata(QueryStringData data)
        {
            try
            {
                string? pollutantName = data.pollutantName;
                string? datasource = data.dataSource;
                string? year = data.Year;
                string? region = data.Region;
                string? regiontype = data.regiontype;
                string? dataselectorfiltertype = data.dataselectorfiltertype;
                string? dataselectordownloadtype = data.dataselectordownloadtype;
                string? email = data.email;

                var stationresultcountData = await AtomDataSelectionStationService.GetAtomDataSelectionStation(pollutantName, datasource, year, region, regiontype, dataselectorfiltertype, dataselectordownloadtype, email);
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
