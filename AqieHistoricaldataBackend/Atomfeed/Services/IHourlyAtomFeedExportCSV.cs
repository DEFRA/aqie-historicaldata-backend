using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IHourlyAtomFeedExportCSV
    {
        [ExcludeFromCodeCoverage]
        Task<byte[]> hourlyatomfeedexport_csv(List<FinalData> Final_list, QueryStringData data);
    }
}
