using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IHourlyAtomFeedExportCSV
    {
        byte[] hourlyatomfeedexport_csv(List<Finaldata> Final_list, querystringdata data);
    }
}
