using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionJobStatus
    {
        [ExcludeFromCodeCoverage]
        Task<JobInfoDto> GetAtomDataSelectionJobStatusdata(string jobID);
    }
}
