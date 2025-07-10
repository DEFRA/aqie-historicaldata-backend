using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAWSS3BucketService
    {
        Task<string> writecsvtoawss3bucket(List<FinalData> Final_list, QueryStringData data, string downloadtype);
    }
}
