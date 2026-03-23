using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAwss3BucketService
    {
        [ExcludeFromCodeCoverage]
        Task<string> WriteCsvToAwsS3BucketAsync(List<FinalData> finalList, QueryStringData data, string downloadType);
    }
}
