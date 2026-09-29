using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Simplify;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AqieHistoricaldataBackend.Atomfeed.Services.GeoBoundary
{
    /// <summary>Prepared, immutable country boundary held in memory.</summary>
    public sealed class CountryBoundary
    {
        public string Name { get; }
        public Geometry Geometry { get; }
        public IPreparedGeometry Prepared { get; }
        public Envelope Envelope { get; }

        public CountryBoundary(string name, Geometry geometry)
        {
            Name = name;
            Geometry = geometry;
            Prepared = PreparedGeometryFactory.Prepare(geometry);
            Envelope = geometry.EnvelopeInternal;
        }
    }

    public interface IGeoBoundaryProvider
    {
        /// <summary>Loads every configured GeoJSON boundary once. Safe to call multiple times.</summary>
        void LoadAll();

        CountryBoundary? Get(string country);

        IReadOnlyList<CountryBoundary> GetMany(IEnumerable<string> countries);
    }

    /// <summary>
    /// Application start -> Load GeoJSON once -> store in memory -> reuse for all requests.
    /// Registered as a singleton; warmed up by <see cref="GeoBoundaryWarmupHostedService"/>.
    /// </summary>
    public sealed class GeoBoundaryProvider : IGeoBoundaryProvider
    {
        private static readonly IReadOnlyDictionary<string, string> GeoJsonPaths =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["England"] = "GeoBoundaries/england.geojson",
                ["Wales"] = "GeoBoundaries/wales.geojson",
                ["Scotland"] = "GeoBoundaries/scotland.geojson",
                ["Northern Ireland"] = "GeoBoundaries/northern_ireland.geojson",
            };

        private readonly IHostEnvironment _env;
        private readonly ILogger<GeoBoundaryProvider> _logger;

        // Immutable snapshot published once loading completes -> lock-free reads.
        private volatile IReadOnlyDictionary<string, CountryBoundary> _cache =
            new Dictionary<string, CountryBoundary>(StringComparer.OrdinalIgnoreCase);

        private int _loaded;

        public GeoBoundaryProvider(IHostEnvironment env, ILogger<GeoBoundaryProvider> logger)
        {
            _env = env;
            _logger = logger;
        }

        public void LoadAll()
        {
            if (Interlocked.Exchange(ref _loaded, 1) == 1)
                return;

            var map = new Dictionary<string, CountryBoundary>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in GeoJsonPaths)
            {
                try
                {
                    map[kvp.Key] = LoadBoundary(kvp.Key, kvp.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load boundary for {Country}", kvp.Key);
                }
            }

            _cache = map;
            _logger.LogInformation("Loaded {Count} country boundaries into memory", map.Count);
        }

        public CountryBoundary? Get(string country)
        {
            if (string.IsNullOrWhiteSpace(country))
                return null;

            if (_loaded == 0)
                LoadAll();

            return _cache.TryGetValue(country, out var boundary) ? boundary : null;
        }

        public IReadOnlyList<CountryBoundary> GetMany(IEnumerable<string> countries)
        {
            var list = new List<CountryBoundary>();
            foreach (var c in countries)
            {
                var b = Get(c);
                if (b is not null)
                    list.Add(b);
            }
            return list;
        }

        private CountryBoundary LoadBoundary(string country, string relPath)
        {
            var fileInfo = _env.ContentRootFileProvider.GetFileInfo(relPath);
            var fullPath = fileInfo.Exists ? fileInfo.PhysicalPath : null;

            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"GeoJSON file not found for country '{country}'. Relative path attempted: '{relPath}'.",
                    relPath);
            }

            _logger.LogInformation("Loading GeoJSON for {Country} from {Path}", country, fullPath);

            var geom = ReadGeometry(fullPath);
            geom = FixIfInvalid(geom);
            var simplified = TopologyPreservingSimplifier.Simplify(geom, 1e-4);

            return new CountryBoundary(country, simplified);
        }

        private Geometry ReadGeometry(string fullPath)
        {
            var geoJsonText = File.ReadAllText(fullPath);
            var reader = new GeoJsonReader();

            return TryReadAsFeatureCollection(reader, geoJsonText, fullPath)
                   ?? TryReadAsSingleGeometry(reader, geoJsonText, fullPath)
                   ?? throw new InvalidDataException($"Unsupported or invalid GeoJSON at: {fullPath}");
        }

        private Geometry? TryReadAsFeatureCollection(GeoJsonReader reader, string geoJsonText, string fullPath)
        {
            try
            {
                var fc = reader.Read<FeatureCollection>(geoJsonText);
                if (fc is null || fc.Count == 0)
                    return null;

                var geoms = new List<Geometry>(fc.Count);
                foreach (var f in fc)
                {
                    if (f?.Geometry is not null)
                        geoms.Add(f.Geometry);
                }

                if (geoms.Count == 0)
                    return null;

                return geoms.Count == 1 ? geoms[0] : UnaryUnionOp.Union(geoms);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "FeatureCollection read failed; trying single Geometry for {Path}", fullPath);
                return null;
            }
        }

        private Geometry? TryReadAsSingleGeometry(GeoJsonReader reader, string geoJsonText, string fullPath)
        {
            try
            {
                return reader.Read<Geometry>(geoJsonText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read geometry from GeoJSON at {Path}", fullPath);
                return null;
            }
        }

        private Geometry FixIfInvalid(Geometry geom)
        {
            if (geom.IsValid)
                return geom;

            try
            {
                return NetTopologySuite.Geometries.Utilities.GeometryFixer.Fix(geom);
            }
            catch { /* fall through */ }

            try
            {
                var fixedByBuffer = geom.Buffer(0);
                if (fixedByBuffer is not null && fixedByBuffer.IsValid)
                    return fixedByBuffer;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Buffer(0) attempt to fix invalid geometry failed");
            }

            _logger.LogWarning("Geometry remains invalid after fix attempts; proceeding as-is");
            return geom;
        }
    }

    /// <summary>Warms the boundary cache at application start.</summary>
    public sealed class GeoBoundaryWarmupHostedService : IHostedService
    {
        private readonly IGeoBoundaryProvider _provider;

        public GeoBoundaryWarmupHostedService(IGeoBoundaryProvider provider) => _provider = provider;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _provider.LoadAll();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}