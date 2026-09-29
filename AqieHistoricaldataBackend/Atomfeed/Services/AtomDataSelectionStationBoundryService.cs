using AqieHistoricaldataBackend.Atomfeed.Services.GeoBoundary;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Index.Strtree;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionStationBoundryService(
        ILogger<AtomDataSelectionStationBoundryService> Logger,
        IAtomDataSelectionLocalAuthoritiesService AtomDataSelectionLocalAuthoritiesService,
        IGeoBoundaryProvider BoundaryProvider
    ) : IAtomDataSelectionStationBoundryService
    {
        private static readonly GeometryFactory s_geometryFactory =
            NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        private static int CountryPriority(string country) => country switch
        {
            "Northern Ireland" => 0,
            "Wales" => 1,
            "Scotland" => 2,
            "England" => 3,
            _ => 4
        };

        // ------------------------------
        // Public entry point – thin dispatcher
        // ------------------------------

        public async Task<List<SiteInfo>> GetAtomDataSelectionStationBoundryService(
            List<SiteInfo> filteredstationpollutant,
            string region,
            string regiontype)
        {
            try
            {               

                if (string.Equals(regiontype, "Country", StringComparison.OrdinalIgnoreCase))
                    return HandleCountryFilter(filteredstationpollutant, region);

                if (string.Equals(regiontype, "LocalAuthority", StringComparison.OrdinalIgnoreCase))
                    return await HandleLocalAuthorityFilterAsync(filteredstationpollutant, region).ConfigureAwait(false);

                if (string.Equals(regiontype, "Region", StringComparison.OrdinalIgnoreCase))
                    return filteredstationpollutant;

                return new List<SiteInfo>();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in Atom GetAtomDataSelectionStationBoundryService");
                return new List<SiteInfo>();
            }
        }

        // ------------------------------
        // Country branch
        // ------------------------------

        private List<SiteInfo> HandleCountryFilter(List<SiteInfo> sites, string region)
        {
            if (string.IsNullOrWhiteSpace(region))
                return new List<SiteInfo>();

            var selectedCountries = region.Split(',')
                  .Select(s => s.Trim())
                  .Where(s => !string.IsNullOrEmpty(s))
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .OrderBy(CountryPriority)
                  .ToList();

            if (selectedCountries.Count == 0)
                return new List<SiteInfo>();

            // Boundaries are already in memory (loaded once at application start)
            var boundaries = BoundaryProvider.GetMany(selectedCountries);

            if (boundaries.Count == 0)
            {
                Logger.LogWarning("No in-memory boundaries found for region {Region}", region);
                return new List<SiteInfo>();
            }

            bool useParallel = sites.Count >= 1500 && Environment.ProcessorCount > 1;
            return useParallel
                ? ProcessParallel(sites, boundaries)
                : ProcessSequential(sites, boundaries);
        }

        // ------------------------------
        // Local Authority branch
        // ------------------------------

        private async Task<List<SiteInfo>> HandleLocalAuthorityFilterAsync(List<SiteInfo> sites, string region)
        {
            var localAuthoritiesResult = await AtomDataSelectionLocalAuthoritiesService
                .GetAtomDataSelectionLocalAuthoritiesService(region).ConfigureAwait(false);

            if (localAuthoritiesResult is null)
                return new List<SiteInfo>();

            var (tree, laCount) = BuildLocalAuthorityTree(localAuthoritiesResult);
            Logger.LogInformation("Built STRtree for {Count} local authority points for region {Region}", laCount, region);

            const double maxDistanceMeters = 8046d;
            var filtered = new List<SiteInfo>(capacity: Math.Min(2048, sites.Count));

            foreach (var site in sites)
            {
                if (!TryParseLatLon(site.Latitude, site.Longitude, out double lat, out double lon))
                    continue;

                var closestLA = FindNearestLocalAuthority(tree, lat, lon, maxDistanceMeters);
                if (closestLA is null)
                    continue;

                site.Country = string.Equals(closestLA.LA_REGION, "London", StringComparison.OrdinalIgnoreCase)
                    ? "England"
                    : closestLA.LA_REGION ?? string.Empty;

                filtered.Add(site);
            }

            Logger.LogInformation("Filtered {Count} stations near local authorities for region: {Region}", filtered.Count, region);
            return filtered;
        }

        private static (STRtree<(Point Point, LocalAuthorityData LocalAuthority)> Tree, int Count)
            BuildLocalAuthorityTree(List<LocalAuthorityData> localAuthorities)
        {
            var str = new STRtree<(Point Point, LocalAuthorityData LocalAuthority)>();
            int laCount = 0;

            foreach (var la in localAuthorities)
            {
                if (la is null) continue;

                double laLat = la.Latitude;
                double laLon = la.Longitude;

                if (Math.Abs(laLat) < 1e-9 && Math.Abs(laLon) < 1e-9) continue;

                var p = s_geometryFactory.CreatePoint(new Coordinate(laLon, laLat));
                str.Insert(new Envelope(p.Coordinate), (p, la));
                laCount++;
            }

            str.Build();
            return (str, laCount);
        }

        private static LocalAuthorityData? FindNearestLocalAuthority(
            STRtree<(Point Point, LocalAuthorityData LocalAuthority)> tree,
            double lat, double lon, double maxDistanceMeters)
        {
            double degLat = maxDistanceMeters / 111_320d;
            double cosLat = Math.Cos(lat * Math.PI / 180d);
            double degLon = maxDistanceMeters / (111_320d * Math.Max(1e-6, cosLat));

            var queryEnvelope = new Envelope(lon - degLon, lon + degLon, lat - degLat, lat + degLat);
            var candidates = tree.Query(queryEnvelope);

            LocalAuthorityData? closestLA = null;
            double minDistance = double.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                var (pt, la) = candidates[i];
                double distance = HaversineMeters(lat, lon, pt.Y, pt.X);

                if (distance <= maxDistanceMeters && distance < minDistance)
                {
                    minDistance = distance;
                    closestLA = la;
                }
            }

            return closestLA;
        }

        // ------------------------------
        // Processing helpers (Country)
        // ------------------------------

        private static string? TryMatchSiteToCountry(SiteInfo site, IReadOnlyList<CountryBoundary> boundaries, Envelope unionEnv)
        {
            if (!TryParseLatLon(site.Latitude, site.Longitude, out double lat, out double lon))
                return null;

            if (!unionEnv.Contains(lon, lat))
                return null;

            Point? point = null;

            for (int b = 0; b < boundaries.Count; b++)
            {
                var bd = boundaries[b];
                if (!bd.Envelope.Contains(lon, lat))
                    continue;

                point ??= s_geometryFactory.CreatePoint(new Coordinate(lon, lat));

                if (bd.Prepared.Covers(point))
                    return bd.Name;
            }

            return null;
        }

        private static List<SiteInfo> ProcessSequential(List<SiteInfo> sites, IReadOnlyList<CountryBoundary> boundaries)
        {
            var filtered = new List<SiteInfo>(sites.Count);

            var unionEnv = new Envelope();
            for (int b = 0; b < boundaries.Count; b++)
                unionEnv.ExpandToInclude(boundaries[b].Envelope);

            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                var matchedCountry = TryMatchSiteToCountry(site, boundaries, unionEnv);

                if (matchedCountry is not null)
                {
                    site.Country = matchedCountry;
                    filtered.Add(site);
                }
            }

            return filtered;
        }

        private static List<SiteInfo> ProcessParallel(List<SiteInfo> sites, IReadOnlyList<CountryBoundary> boundaries)
        {
            var unionEnv = new Envelope();
            for (int b = 0; b < boundaries.Count; b++)
                unionEnv.ExpandToInclude(boundaries[b].Envelope);

            var results = new ConcurrentBag<List<SiteInfo>>();

            Parallel.ForEach(
                Partitioner.Create(0, sites.Count),
                () => new List<SiteInfo>(256),
                (range, _, local) =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        var site = sites[i];
                        var matchedCountry = TryMatchSiteToCountry(site, boundaries, unionEnv);

                        if (matchedCountry is not null)
                        {
                            site.Country = matchedCountry;
                            local.Add(site);
                        }
                    }
                    return local;
                },
                local => results.Add(local));

            var total = 0;
            foreach (var l in results) total += l.Count;

            var final = new List<SiteInfo>(total);
            foreach (var l in results) final.AddRange(l);

            return final;
        }

        // ------------------------------
        // Utility helpers
        // ------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseLatLon(string? latStr, string? lonStr, out double lat, out double lon)
        {
            lat = lon = default;
            if (string.IsNullOrWhiteSpace(latStr) || string.IsNullOrWhiteSpace(lonStr))
                return false;

            if (!double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out lat))
                return false;

            if (!double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out lon))
                return false;

            if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
            {
                lat = lon = default;
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryToDouble(object? value, out double result)
        {
            switch (value)
            {
                case double d: result = d; return true;
                case float f: result = f; return true;
                case decimal m: result = (double)m; return true;
                case int i: result = i; return true;
                case long l: result = l; return true;
                case string s:
                    return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
                default:
                    result = default;
                    return false;
            }
        }

        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000d;
            double rad = Math.PI / 180d;
            double dLat = (lat2 - lat1) * rad;
            double dLon = (lon2 - lon1) * rad;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * rad) * Math.Cos(lat2 * rad) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
    }
}