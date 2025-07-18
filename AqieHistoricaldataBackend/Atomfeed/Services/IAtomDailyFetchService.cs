using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDailyFetchService
    {
        [ExcludeFromCodeCoverage]
        Task<List<FinalData>> GetAtomDailydatafetch(List<FinalData> finalhourlypollutantresult, QueryStringData data);
    }
}
