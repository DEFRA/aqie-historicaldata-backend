using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionStationBoundryService
    {
        [ExcludeFromCodeCoverage]
        Task<List<PollutantDetails>> GetAtomDataSelectionStationBoundryService(List<PollutantDetails> filteredstationpollutant);
    }
}
