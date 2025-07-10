using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomHistoryService
    {
        public Task<string> AtomHealthcheck();
        public Task<string> GetAtomHourlydata(QueryStringData data);

        public Task<dynamic> GetHistoryexceedencedata(QueryStringData data);

    }
}
