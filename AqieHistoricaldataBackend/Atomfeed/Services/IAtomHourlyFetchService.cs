using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    /// <summary>Rows plus whether the upstream feed itself failed, as opposed to holding no data.</summary>
    public sealed record AtomHourlyFetchOutcome(List<FinalData> Rows, bool UpstreamFailed);

    public interface IAtomHourlyFetchService
    {
        [ExcludeFromCodeCoverage]
        Task<List<FinalData>> GetAtomHourlydatafetch(string siteID, string year, string downloadfilter);

        Task<AtomHourlyFetchOutcome> GetAtomHourlydatafetchWithStatus(
            string siteID, string year, string downloadfilter, string? dataSource = null);
    }
}
