using AqieHistoricaldataBackend.Atomfeed.Models;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomRegionService(
        ILogger<AtomRegionService> Logger,
        IHttpClientFactory httpClientFactory,
        IAuthService AuthService) : IAtomRegionService
    {
        public async Task<List<RegionInfo>> GetDistinctRegions()
        {
            try
            {
                var token = await AuthService.GetRicardoToken();
                var sitemetadatainfo = await RicardoSiteMetadata.FetchSiteMetadata(httpClientFactory, token);

                if (sitemetadatainfo is null || sitemetadatainfo.Count == 0)
                {
                    Logger.LogWarning("No site metadata returned while building region list.");
                    return new List<RegionInfo>();
                }

                var regions = sitemetadatainfo
                    .Where(s => !string.IsNullOrWhiteSpace(s.RegionId))
                    .Select(s => s.RegionId!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(id => new RegionInfo
                    {
                        RegionId = id,
                        RegionName = RegionMaster.Resolve(id)
                    })
                    .OrderBy(r => int.TryParse(r.RegionId, out var n) ? n : int.MaxValue)
                    .ToList();

                Logger.LogInformation("Resolved {Count} distinct regions from site metadata.", regions.Count);
                return regions;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error building distinct region list {Error}", ex.Message);
                return new List<RegionInfo>();
            }
        }
    }
}