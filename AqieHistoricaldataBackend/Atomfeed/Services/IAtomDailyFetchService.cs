using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDailyFetchService
    {
        List<Finaldata> GetAtomDailydatafetch(List<Finaldata> finalhourlypollutantresult, querystringdata data);
    }
}
