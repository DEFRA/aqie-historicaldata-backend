using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomRegionService
    {
        [ExcludeFromCodeCoverage]
        Task<List<RegionInfo>> GetDistinctRegions();
    }
}
