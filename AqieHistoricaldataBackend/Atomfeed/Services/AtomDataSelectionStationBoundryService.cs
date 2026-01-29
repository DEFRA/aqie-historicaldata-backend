
using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Operation.Valid;
using NetTopologySuite.Simplify;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static AqieHistoricaldataBackend.Atomfeed.Models.AtomHistoryModel;
using static AqieHistoricaldataBackend.Atomfeed.Services.AtomDataSelectionStationBoundryService;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public class AtomDataSelectionStationBoundryService(
        ILogger<AtomDataSelectionStationBoundryService> Logger,
        IAtomDataSelectionLocalAuthoritiesService AtomDataSelectionLocalAuthoritiesService,
        IHttpClientFactory httpClientFactory
    ) : IAtomDataSelectionStationBoundryService
    {
        // Single GeometryFactory to reduce allocations
        private static readonly GeometryFactory s_geometryFactory =
            NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        // Prepared boundary + envelope for fast checks
        private sealed class Boundary
        {
            public string Name { get; }
            public Geometry Geometry { get; }
            public IPreparedGeometry Prepared { get; }
            public Envelope Envelope { get; }

            public Boundary(string name, Geometry geometry)
            {
                Name = name;
                Geometry = geometry;
                Prepared = PreparedGeometryFactory.Prepare(geometry);
                Envelope = geometry.EnvelopeInternal;
            }
        }

        // Country → Lazy<Boundary> for lock-free, idempotent loading
        private static readonly ConcurrentDictionary<string, Lazy<Boundary>> CountryBoundariesLazy =
            new(StringComparer.OrdinalIgnoreCase);

        // Map country to GeoJSON once
        private static string? GetGeoJsonPath(string country) => country switch
        {
            "England" => "GeoBoundaries/england.geojson",
            "Wales" => "GeoBoundaries/wales.geojson",
            "Scotland" => "GeoBoundaries/scotland.geojson",
            "Northern Ireland" => "GeoBoundaries/northern_ireland.geojson",
            _ => null
        };

        /// <summary>
        /// Load (or get cached) prepared boundary for a country.
        /// Uses Lazy<T> to ensure single-load and lock-free reads.
        /// </summary>
        private static Boundary GetOrLoadBoundary(string country, ILogger logger) =>
            CountryBoundariesLazy.GetOrAdd(country, c => new Lazy<Boundary>(() =>
            {
                var path = GetGeoJsonPath(c);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    throw new FileNotFoundException($"GeoJSON path not found for '{c}'", path ?? "<null>");

                var geom = LoadGeometryFromGeoJson(path!, logger);

                // Fix invalid geometry only if needed (faster & safer than Buffer(0))
                if (!geom.IsValid)
                {
                    geom = GeometryFixer.Fix(geom);
                }

                // Optional: small simplification tolerance (degrees) to speed up PIP
                // ~1e-4 deg ≈ 10–11 m; adjust to your tolerance needs
                var simplified = TopologyPreservingSimplifier.Simplify(geom, 1e-4);

                return new Boundary(c, simplified);
            }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        /// <summary>
        /// Ensure specified countries are present in the cache (upserts via Lazy).
        /// </summary>
        private static void EnsureBoundariesLoaded(IEnumerable<string> countries, ILogger logger)
        {
            foreach (var c in countries)
            {
                try { _ = GetOrLoadBoundary(c, logger); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to load boundary for {Country}", c);
                }
            }
        }

        /// <summary>
        /// Read GeoJSON robustly:
        /// 1) Try FeatureCollection, Union geometries if multiple
        /// 2) Fall back to reading a single Geometry
        /// </summary>
        private static Geometry LoadGeometryFromGeoJson(string path, ILogger logger)
        {
            var fullPath = Path.GetFullPath(path);
            var geoJsonText = File.ReadAllText(fullPath);
            var reader = new GeoJsonReader();

            // Try FeatureCollection
            try
            {
                var fc = reader.Read<FeatureCollection>(geoJsonText);
                if (fc is not null && fc.Count > 0)
                {
                    var geoms = new List<Geometry>(fc.Count);
                    foreach (var f in fc)
                    {
                        if (f?.Geometry is not null)
                            geoms.Add(f.Geometry);
                    }

                    if (geoms.Count == 0)
                        throw new InvalidDataException($"No geometries in FeatureCollection: {fullPath}");

                    if (geoms.Count == 1)
                        return geoms[0];

                    // Efficient union for multiple polygons/multipolygons
                    return UnaryUnionOp.Union(geoms);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "FeatureCollection read failed; trying single Geometry for {Path}", fullPath);
            }

            // Try as single Geometry (Polygon / MultiPolygon)
            try
            {
                var geom = reader.Read<Geometry>(geoJsonText);
                if (geom is not null)
                    return geom;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read geometry from GeoJSON at {Path}", fullPath);
            }

            throw new InvalidDataException($"Unsupported or invalid GeoJSON at: {fullPath}");
        }

        private static int CountryPriority(string country) => country switch
        {
            "Northern Ireland" => 0,
            "Wales" => 1,
            "Scotland" => 2,
            "England" => 3,
            _ => 4
        };

        public async Task<List<SiteInfo>> GetAtomDataSelectionStationBoundryService(
            List<SiteInfo> filtered_station_pollutant,
            string region,
            string regiontype)
        {
            try
            {
                // Explicit branch check; avoid accidental fallthrough on blank region
                if (string.Equals(regiontype, "Country", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(region))
                        return new List<SiteInfo>();

                    // Parse, distinct (case-insensitive), and sort by priority
                    var selectedCountries =
                        region.Split(',')
                              .Select(s => s.Trim())
                              .Where(s => !string.IsNullOrEmpty(s))
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .OrderBy(c => CountryPriority(c))
                              .ToList();

                    if (selectedCountries.Count == 0)
                        return new List<SiteInfo>();

                    // Load (or get cached) boundaries
                    EnsureBoundariesLoaded(selectedCountries, Logger);

                    var boundaries = new List<Boundary>(selectedCountries.Count);
                    foreach (var c in selectedCountries)
                    {
                        try
                        {
                            boundaries.Add(GetOrLoadBoundary(c, Logger));
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Boundary retrieval failed for {Country}", c);
                        }
                    }

                    if (boundaries.Count == 0)
                        return new List<SiteInfo>();

                    // Heuristic: parallelize on big batches
                    bool useParallel = filtered_station_pollutant.Count >= 1500 &&
                                       Environment.ProcessorCount > 1;

                    var result = useParallel
                        ? ProcessParallel(filtered_station_pollutant, boundaries)
                        : ProcessSequential(filtered_station_pollutant, boundaries);

                    return await Task.FromResult(result).ConfigureAwait(false);
                }
                else if (string.Equals(regiontype, "LocalAuthority", StringComparison.OrdinalIgnoreCase))
                {
                    // Change the ambiguous call to explicitly cast to the intended return type
                    var localAuthoritiesresult = await AtomDataSelectionLocalAuthoritiesService.GetAtomDataSelectionLocalAuthoritiesService(region);

                    if (localAuthoritiesresult is null)
                        return new List<SiteInfo>();

                    // Build STRtree of LA points
                    var str = new STRtree<Point>();
                    int laCount = 0;
                    foreach (var la in localAuthoritiesresult)
                    {
                        // dynamic assumed to have Latitude, Longitude; adjust types if needed
                        if (la is null) continue;
                        if (!TryToDouble(la.Latitude, out var laLat)) continue;
                        if (!TryToDouble(la.Longitude, out var laLon)) continue;
                        if (laLat == 0 && laLon == 0) continue;

                        var p = s_geometryFactory.CreatePoint(new Coordinate(laLon, laLat));
                        str.Insert(new Envelope(p.Coordinate), p);
                        laCount++;
                    }
                    str.Build();

                    Logger.LogInformation("Built STRtree for {Count} local authority points for region {Region}", laCount, region);

                    // Proximity filter using indexed candidate selection + Haversine
                    const double maxDistanceMeters = 30_000d;

                    var filtered = new List<SiteInfo>(capacity: Math.Min(2048, filtered_station_pollutant.Count));

                    foreach (var site in filtered_station_pollutant)
                    {
                        if (!TryParseLatLon(site.Latitude, site.Longitude, out double lat, out double lon))
                            continue;

                        // Compute conservative query envelope in degrees
                        // (Latitude degrees ~ 111.32km; longitude scales with cos(lat))
                        double degLat = maxDistanceMeters / 111_320d;
                        double cosLat = Math.Cos(lat * Math.PI / 180d);
                        // Avoid div by zero near poles
                        double degLon = maxDistanceMeters / (111_320d * Math.Max(1e-6, cosLat));

                        var env = new Envelope(lon - degLon, lon + degLon, lat - degLat, lat + degLat);
                        var candidates = str.Query(env);

                        bool near = false;
                        for (int i = 0; i < candidates.Count; i++)
                        {
                            var pt = candidates[i];
                            if (HaversineMeters(lat, lon, pt.Y, pt.X) <= maxDistanceMeters)
                            {
                                near = true;
                                break;
                            }
                        }

                        if (near)
                            filtered.Add(site);
                    }

                    Logger.LogInformation("Filtered {Count} stations near local authorities for region: {Region}", filtered.Count, region);
                    return filtered;
                }

                // Unknown region type
                return new List<SiteInfo>();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in Atom GetAtomDataSelectionStationBoundryService");
                return new List<SiteInfo>();
            }
        }

        // ------------------------------
        // Processing helpers (Country)
        // ------------------------------

        private static List<SiteInfo> ProcessSequential(List<SiteInfo> sites, List<Boundary> boundaries)
        {
            var filtered = new List<SiteInfo>(sites.Count);

            // Union envelope for cheap global rejection
            var unionEnv = new Envelope();
            for (int b = 0; b < boundaries.Count; b++)
                unionEnv.ExpandToInclude(boundaries[b].Envelope);

            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (!TryParseLatLon(site.Latitude, site.Longitude, out double lat, out double lon))
                    continue;

                if (!unionEnv.Contains(lon, lat))
                    continue;

                Point? point = null; // lazy create only if at least one envelope hits

                string? matchedCountry = null;
                for (int b = 0; b < boundaries.Count; b++)
                {
                    var bd = boundaries[b];
                    if (!bd.Envelope.Contains(lon, lat))
                        continue;

                    point ??= s_geometryFactory.CreatePoint(new Coordinate(lon, lat));

                    // Note: Covers includes boundary points; use Contains for strictly inside
                    if (bd.Prepared.Covers(point))
                    {
                        matchedCountry = bd.Name;
                        break;
                    }
                }

                if (matchedCountry is not null)
                {
                    site.Country = matchedCountry;
                    filtered.Add(site);
                }
            }

            return filtered;
        }

        private static List<SiteInfo> ProcessParallel(List<SiteInfo> sites, List<Boundary> boundaries)
        {
            // Precompute union envelope
            var unionEnv = new Envelope();
            for (int b = 0; b < boundaries.Count; b++)
                unionEnv.ExpandToInclude(boundaries[b].Envelope);

            // Collect results in thread-local buffers
            var results = new ConcurrentBag<List<SiteInfo>>();

            Parallel.ForEach(
                Partitioner.Create(0, sites.Count),
                () => new List<SiteInfo>(256),
                (range, _, local) =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        var site = sites[i];
                        if (!TryParseLatLon(site.Latitude, site.Longitude, out double lat, out double lon))
                            continue;

                        if (!unionEnv.Contains(lon, lat))
                            continue;

                        Point? point = null;
                        string? matchedCountry = null;

                        for (int b = 0; b < boundaries.Count; b++)
                        {
                            var bd = boundaries[b];
                            if (!bd.Envelope.Contains(lon, lat))
                                continue;

                            point ??= s_geometryFactory.CreatePoint(new Coordinate(lon, lat));
                            if (bd.Prepared.Covers(point))
                            {
                                matchedCountry = bd.Name;
                                break;
                            }
                        }

                        if (matchedCountry is not null)
                        {
                            site.Country = matchedCountry;
                            local.Add(site);
                        }
                    }
                    return local;
                },
                local => results.Add(local));

            // Flatten
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

            // Optional: sanity ranges
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
