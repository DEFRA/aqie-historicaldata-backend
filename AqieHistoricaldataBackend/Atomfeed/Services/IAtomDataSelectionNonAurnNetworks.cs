using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionNonAurnNetworks
    {
        [ExcludeFromCodeCoverage]
        Task<dynamic> GetAtomNonAurnNetworks(QueryStringData data);
        Task ExceltoMongoDB(string pollutantName);               
        Task ExceltoMongoDB_Station_detials(string pollutantName);
    }
}
