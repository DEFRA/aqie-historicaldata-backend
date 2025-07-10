using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomAnnualFetchService
    {
        Task<List<FinalData>> GetAtomAnnualdatafetch(List<FinalData> finalhourlypollutantresult, QueryStringData data);
    }
}
