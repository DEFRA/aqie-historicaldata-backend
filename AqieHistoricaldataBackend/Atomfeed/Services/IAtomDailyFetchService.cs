using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDailyFetchService
    {
        Task<List<FinalData>> GetAtomDailydatafetch(List<FinalData> finalhourlypollutantresult, QueryStringData data);
    }
}
