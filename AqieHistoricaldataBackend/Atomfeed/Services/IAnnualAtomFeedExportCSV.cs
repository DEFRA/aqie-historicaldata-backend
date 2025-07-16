using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{    
    public interface IAnnualAtomFeedExportCSV
    {        
        Task<byte[]> annualatomfeedexport_csv(List<FinalData> Final_list, QueryStringData data);
    }
}
