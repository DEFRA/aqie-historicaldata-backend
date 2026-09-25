using System.Globalization;
using AqieHistoricaldataBackend.Atomfeed.Models;
using Microsoft.Extensions.Caching.Memory;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomObservationStationsService
    {
        Task<ObservationStationsResult> GetStationsAsync(string network, string? pollutant);
    }

    /// <summary>
    /// Lists stations keyed by the same `localSiteId` the observations endpoint expects, so the two
    /// cannot drift apart. Station metadata comes from Ricardo (AURN) or MongoDB (non-AURN).
    /// </summary>
    public class AtomObservationStationsService(
        ILogger<AtomObservationStationsService> logger,
        IAtomDataSelectionStationService stationService,
        IMemoryCache cache) : IAtomObservationStationsService
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

        public async Task<ObservationStationsResult> GetStationsAsync(string network, string? pollutant)
        {
            var key = $"obs-stations::{network}";

            if (!cache.TryGetValue(key, out List<ObservationStation>? stations) || stations is null)
            {
                var sites = await stationService.GetObservationStationsAsync(network);
                stations = sites.Select(s => Map(s, network)).OrderBy(s => s.Name).ToList();
                cache.Set(key, stations, CacheDuration);
                logger.LogInformation("Cached {Count} {Network} observation stations", stations.Count, network);
            }

            var filtered = pollutant is null
                ? stations
                : stations.Where(s => s.Pollutants.Any(p => p.Code == pollutant)).ToList();

            return new ObservationStationsResult
            {
                Network = network,
                Count = filtered.Count,
                Stations = filtered
            };
        }

        private static ObservationStation Map(SiteInfo site, string network) => new()
        {
            SiteId = site.LocalSiteId ?? string.Empty,
            Name = site.SiteName ?? string.Empty,
            Network = network,
            Latitude = ParseCoordinate(site.Latitude),
            Longitude = ParseCoordinate(site.Longitude),
            Region = site.ZoneRegion,
            AreaType = site.AreaType,
            SiteType = site.SiteType,
            NetworkType = site.NetworkType,
            Pollutants = MapPollutants(site.Pollutants)
        };

        /// <summary>
        /// Upstream lists PM as several instrument variants under HTML-encoded names, so the served
        /// five are collapsed onto their canonical name and code, and anything else is passed
        /// through with the markup stripped and an empty code.
        /// </summary>
        private static List<ObservationStationPollutant> MapPollutants(List<PollutantInfo>? pollutants) =>
            (pollutants ?? [])
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => new { Source = p, Definition = ObservationsPollutants.Resolve(p.Name) })
                .GroupBy(x => x.Definition?.Code ?? StripMarkup(x.Source.Name!))
                .Select(g =>
                {
                    var first = g.First();
                    return new ObservationStationPollutant
                    {
                        Name = first.Definition?.Name ?? StripMarkup(first.Source.Name!),
                        Code = first.Definition?.Code ?? string.Empty,
                        StartDate = g.Select(x => x.Source.StartDate).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)),
                        EndDate = g.Select(x => x.Source.EndDate).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d))
                    };
                })
                .OrderBy(p => p.Name)
                .ToList();

        private static string StripMarkup(string value) =>
            value.Replace("<sub>", string.Empty, StringComparison.OrdinalIgnoreCase)
                 .Replace("</sub>", string.Empty, StringComparison.OrdinalIgnoreCase)
                 .Trim();

        private static decimal? ParseCoordinate(string? value) =>
            decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }
}
