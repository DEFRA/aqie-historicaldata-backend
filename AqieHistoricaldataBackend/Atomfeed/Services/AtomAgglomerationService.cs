using AqieHistoricaldataBackend.Atomfeed.Models;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomAgglomerationService(
        ILogger<AtomAgglomerationService> Logger,
        IHttpClientFactory httpClientFactory,
        IAuthService AuthService) : IAtomAgglomerationService
    {
        public async Task<List<AgglomerationInfo>> GetDistinctAgglomerations()
        {
            try
            {
                var token = await AuthService.GetRicardoToken();
                var sitemetadatainfo = await RicardoSiteMetadata.FetchSiteMetadata(httpClientFactory, token);

                if (sitemetadatainfo is null || sitemetadatainfo.Count == 0)
                {
                    Logger.LogWarning("No site metadata returned while building agglomeration list.");
                    return new List<AgglomerationInfo>();
                }

                var agglomerations = sitemetadatainfo
                    .Where(s => !string.IsNullOrWhiteSpace(s.AgglomerationID))
                    .Select(s => s.AgglomerationID!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(id => new
                    {
                        Id = id,
                        Region = AgglomerationMaster.Resolve(id),
                        Sort = int.TryParse(id, out var n) ? n : int.MaxValue
                    })
                    .GroupBy(x => x.Region, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new AgglomerationInfo
                    {
                        AgglomerationId = string.Join(",", g.OrderBy(x => x.Sort).Select(x => x.Id)),
                        AgglomerationRegion = g.Key
                    })
                    .OrderBy(a => int.TryParse(a.AgglomerationId!.Split(',')[0], out var n) ? n : int.MaxValue)
                    .ToList();

                Logger.LogInformation("Resolved {Count} distinct agglomerations from site metadata.", agglomerations.Count);
                return agglomerations;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error building distinct agglomeration list {Error}", ex.Message);
                return new List<AgglomerationInfo>();
            }
        }
    }
}