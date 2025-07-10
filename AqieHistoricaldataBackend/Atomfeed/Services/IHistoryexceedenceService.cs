using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IHistoryexceedenceService
    {
        Task<dynamic> GetHistoryexceedencedata(QueryStringData data);
    }
}
