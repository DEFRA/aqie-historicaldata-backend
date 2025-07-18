using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomHourlyFetchService
    {
        [ExcludeFromCodeCoverage]
        Task<List<FinalData>> GetAtomHourlydatafetch(string siteID, string year, string downloadfilter);
    }
}
