using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionHourlyFetchService
    {
        [ExcludeFromCodeCoverage]
        Task<List<FinalData>> GetAtomDataSelectionHourlyFetchService(List<PollutantDetails> filteredstationpollutant, string pollutantName);
    }
}
