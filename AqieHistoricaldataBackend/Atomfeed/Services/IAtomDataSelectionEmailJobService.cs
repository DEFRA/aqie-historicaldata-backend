using MongoDB.Bson.Serialization.IdGenerators;
using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionEmailJobService
    {
        [ExcludeFromCodeCoverage]
        Task<string> GetAtomemailjobDataSelection(QueryStringData data);
        Task ProcessPendingEmailJobsAsync(CancellationToken stoppingToken);
       
        //Task ProcessPendingEmailJobsAsync();
    }
}
