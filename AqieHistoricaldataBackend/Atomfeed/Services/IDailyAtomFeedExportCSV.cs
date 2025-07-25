using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IDailyAtomFeedExportCSV
    {
        [ExcludeFromCodeCoverage]
        byte[] dailyatomfeedexport_csv(List<FinalData> Final_list, QueryStringData data);
    }
}
