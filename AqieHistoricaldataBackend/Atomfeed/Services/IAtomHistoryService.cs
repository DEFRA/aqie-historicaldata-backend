using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomHistoryService
    {
        [ExcludeFromCodeCoverage]
        //public Task<string> AtomHealthcheck();
        public Task<string> GetAtomHourlydata(QueryStringData data);

        public Task<dynamic> GetHistoryexceedencedata(QueryStringData data);
        public Task<dynamic> GetatomDataSelectiondata(QueryStringData data);
        public Task<JobInfoDto> GetAtomDataSelectionJobStatusdata(QueryStringData data);

    }
}
