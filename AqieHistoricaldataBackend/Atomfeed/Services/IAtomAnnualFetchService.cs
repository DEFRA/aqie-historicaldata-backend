using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomAnnualFetchService
    {
        Task<List<Finaldata>> GetAtomAnnualdatafetch(List<Finaldata> finalhourlypollutantresult, querystringdata data);
    }
}
