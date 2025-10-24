using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionService
    {
        [ExcludeFromCodeCoverage]
        Task<string> GetatomDataSelectiondata(QueryStringData data);
    }
}
