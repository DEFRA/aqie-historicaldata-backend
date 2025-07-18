using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IHistoryexceedenceService
    {
        [ExcludeFromCodeCoverage]
        Task<dynamic> GetHistoryexceedencedata(QueryStringData data);
    }
}
