using AqieHistoricaldataBackend.Atomfeed.Models;
using System.Diagnostics.CodeAnalysis;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionStationService
    {
        [ExcludeFromCodeCoverage]
        Task<object> GetAtomDataSelectionStation(AtomHistoryModel.QueryStringData queryStringData);
    }
}
