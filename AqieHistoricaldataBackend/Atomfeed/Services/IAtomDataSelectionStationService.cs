using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionStationService
    {
        [ExcludeFromCodeCoverage]
        //Task<string> GetAtomDataSelectionStation(QueryString data);
        Task<string> GetAtomDataSelectionStation(string pollutantName, string datasource, string year, string region, string dataselectorfiltertype, string dataselectordownloadtype, string email);
    }
}
