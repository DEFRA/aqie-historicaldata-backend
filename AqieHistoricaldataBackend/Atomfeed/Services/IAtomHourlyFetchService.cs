using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomHourlyFetchService
    {
         Task<List<FinalData>> GetAtomHourlydatafetch(string siteID, string year, string downloadfilter);
    }
}
