using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAWSS3BucketService
    {
        Task<string> writecsvtoawss3bucket(List<Finaldata> Final_list, querystringdata data, string downloadtype);
    }
}
