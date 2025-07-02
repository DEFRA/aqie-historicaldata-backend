using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAnnualAtomFeedExportCSV
    {
        byte[] annualatomfeedexport_csv(List<Finaldata> Final_list, querystringdata data);
    }
}
