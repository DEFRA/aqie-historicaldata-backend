using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDailyFetchService
    {
        Task<List<Finaldata>> GetAtomDailydatafetch(List<Finaldata> finalhourlypollutantresult, querystringdata data);
    }
}
