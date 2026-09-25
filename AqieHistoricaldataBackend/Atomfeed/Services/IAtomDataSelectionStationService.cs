using AqieHistoricaldataBackend.Atomfeed.Models;
using System.Diagnostics.CodeAnalysis;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionStationService
    {
        [ExcludeFromCodeCoverage]
        Task<object> GetAtomDataSelectionStation(AtomHistoryModel.QueryStringData queryStringData);

        /// <summary>All stations for a network, keyed by the localSiteId the ATOM feeds use.</summary>
        Task<List<SiteInfo>> GetObservationStationsAsync(string network);
    }
}
