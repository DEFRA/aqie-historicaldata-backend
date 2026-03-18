using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IDataSelectionHourlyAtomFeedExportCsv
    {
        [ExcludeFromCodeCoverage]
        Task<byte[]> dataSelectionHourlyAtomFeedExportCSV(List<FinalData> Final_list, QueryStringData data);
    }
}
