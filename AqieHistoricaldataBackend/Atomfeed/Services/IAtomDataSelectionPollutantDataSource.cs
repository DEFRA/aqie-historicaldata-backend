using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionPollutantDataSource
    {
        [ExcludeFromCodeCoverage]
        Task<dynamic> GetAtomPollutantDataSource(QueryStringData data);
    }
}
