using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomHistoryService
    {
        public Task<string> AtomHealthcheck();
        public Task<string> GetAtomHourlydata(string name);
    }
}
