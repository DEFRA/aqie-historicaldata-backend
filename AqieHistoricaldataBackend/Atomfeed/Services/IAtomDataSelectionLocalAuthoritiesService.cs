using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionLocalAuthoritiesService
    {
        [ExcludeFromCodeCoverage]
        Task<List<LocalAuthorityData>> GetAtomDataSelectionLocalAuthoritiesService(string region);
    }
}
