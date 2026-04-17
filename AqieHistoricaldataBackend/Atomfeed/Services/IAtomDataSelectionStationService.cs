using System.Diagnostics.CodeAnalysis;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionStationService
    {
        [ExcludeFromCodeCoverage]
        Task<object> GetAtomDataSelectionStation(string? pollutantName, string? datasource, string? year, string? region, string? regiontype, string? dataselectorfiltertype, string? dataselectordownloadtype, string? email);
    }
}
