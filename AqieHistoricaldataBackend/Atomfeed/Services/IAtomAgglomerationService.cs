using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomAgglomerationService
    {
        [ExcludeFromCodeCoverage]
        Task<List<AgglomerationInfo>> GetDistinctAgglomerations();
    }
}