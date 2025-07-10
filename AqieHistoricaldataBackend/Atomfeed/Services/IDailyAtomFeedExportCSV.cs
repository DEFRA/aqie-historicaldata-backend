using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IDailyAtomFeedExportCSV
    {
        byte[] dailyatomfeedexport_csv(List<FinalData> Final_list, QueryStringData data);
    }
}
