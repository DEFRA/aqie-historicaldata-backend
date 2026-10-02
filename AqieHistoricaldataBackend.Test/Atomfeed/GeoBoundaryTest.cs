using AqieHistoricaldataBackend.Atomfeed.Services.GeoBoundary;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;

namespace AqieHistoricaldataBackend.Test.Atomfeed.Services;

public sealed class GeoBoundaryTest : IDisposable
{
    private readonly string _root;

    public GeoBoundaryTest()
    {
        _root = Path.Combine(Path.GetTempPath(), "geo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "GeoBoundaries"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeEnv : IHostEnvironment
    {
        public FakeEnv(string root)
        {
            ContentRootPath = root;
            ContentRootFileProvider = new PhysicalFileProvider(root);
        }

        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }

    private GeoBoundaryProvider CreateProvider() =>
        new(new FakeEnv(_root), NullLogger<GeoBoundaryProvider>.Instance);

    private void Write(string file, string content) =>
        File.WriteAllText(Path.Combine(_root, "GeoBoundaries", file), content);

    private static string Square(double minX, double minY, double size) =>
        $@"{{""type"":""Polygon"",""coordinates"":[[[{minX},{minY}],[{minX + size},{minY}],
           [{minX + size},{minY + size}],[{minX},{minY + size}],[{minX},{minY}]]]}}";

    private static string FeatureCollection(params string[] geometries)
    {
        var features = string.Join(",", geometries.Select(g =>
            $@"{{""type"":""Feature"",""properties"":{{}},""geometry"":{g}}}"));
        return $@"{{""type"":""FeatureCollection"",""features"":[{features}]}}";
    }

    private void WriteAllValid()
    {
        // single-feature collection, multi-feature collection (union), bare geometry, empty collection
        Write("england.geojson", FeatureCollection(Square(0, 0, 1)));
        Write("wales.geojson", FeatureCollection(Square(5, 5, 1), Square(9, 9, 1)));
        Write("scotland.geojson", Square(10, 10, 1));
        Write("northern_ireland.geojson", @"{""type"":""FeatureCollection"",""features"":[]}");
    }

    [Fact]
    public void LoadAll_LoadsAllSupportedGeoJsonShapes()
    {
        WriteAllValid();
        var provider = CreateProvider();

        provider.LoadAll();

        Assert.NotNull(provider.Get("England"));
        Assert.NotNull(provider.Get("Wales"));
        Assert.NotNull(provider.Get("Scotland"));
        // empty feature collection -> both readers fail -> boundary skipped
        Assert.Null(provider.Get("Northern Ireland"));
    }

    [Fact]
    public void CountryBoundary_ExposesPreparedGeometryAndEnvelope()
    {
        WriteAllValid();
        var boundary = CreateProvider().Get("England")!;

        Assert.Equal("England", boundary.Name);
        Assert.NotNull(boundary.Geometry);
        Assert.NotNull(boundary.Prepared);
        Assert.Equal(boundary.Geometry.EnvelopeInternal, boundary.Envelope);
        Assert.True(boundary.Prepared.Contains(
            new GeometryFactory().CreatePoint(new Coordinate(0.5, 0.5))));
    }

    [Fact]
    public void LoadAll_IsIdempotent()
    {
        WriteAllValid();
        var provider = CreateProvider();

        provider.LoadAll();
        var first = provider.Get("England");
        provider.LoadAll(); // second call returns immediately

        Assert.Same(first, provider.Get("England"));
    }

    [Fact]
    public void Get_TriggersLazyLoad_WhenNotLoaded()
    {
        WriteAllValid();
        var provider = CreateProvider();

        Assert.NotNull(provider.Get("scotland")); // case-insensitive + lazy load
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Get_ReturnsNull_ForNullOrWhitespace(string? country)
    {
        WriteAllValid();
        Assert.Null(CreateProvider().Get(country!));
    }

    [Fact]
    public void Get_ReturnsNull_ForUnknownCountry()
    {
        WriteAllValid();
        Assert.Null(CreateProvider().Get("France"));
    }

    [Fact]
    public void LoadAll_LogsAndSkips_WhenFilesMissing()
    {
        var provider = CreateProvider(); // no files written

        provider.LoadAll();

        Assert.Null(provider.Get("England"));
        Assert.Empty(provider.GetMany(new[] { "England", "Wales" }));
    }

    [Fact]
    public void LoadAll_SkipsCountry_WhenGeoJsonIsInvalid()
    {
        WriteAllValid();
        Write("england.geojson", "this-is-not-geojson");
        var provider = CreateProvider();

        provider.LoadAll();

        Assert.Null(provider.Get("England"));
        Assert.NotNull(provider.Get("Wales"));
    }

    [Fact]
    public void LoadAll_RepairsInvalidGeometry()
    {
        WriteAllValid();
        // self-intersecting "bow-tie" polygon -> invalid -> fixed during load
        Write("england.geojson",
            @"{""type"":""Polygon"",""coordinates"":[[[0,0],[2,2],[2,0],[0,2],[0,0]]]}");

        var boundary = CreateProvider().Get("England");

        Assert.NotNull(boundary);
        Assert.True(boundary!.Geometry.IsValid);
    }

    private static readonly string[] EnglandAndWales = ["England", "Wales"];
    private static readonly string[] MixedCountryNames = ["England", "Atlantis", "", "Scotland"];

    [Fact]
    public void GetMany_ReturnsOnlyResolvedBoundaries()
    {
        WriteAllValid();
        var provider = CreateProvider();

        var result = provider.GetMany(MixedCountryNames);

        Assert.Equal(2, result.Count);
        Assert.Collection(result,
            b => Assert.Equal("England", b.Name),
            b => Assert.Equal("Scotland", b.Name));
    }

    [Fact]
    public void GetMany_ReturnsEmpty_ForEmptyInput()
    {
        WriteAllValid();
        Assert.Empty(CreateProvider().GetMany(Array.Empty<string>()));
    }

    [Fact]
    public async Task WarmupHostedService_LoadsBoundariesOnStartAndStopsCleanly()
    {
        WriteAllValid();
        var provider = CreateProvider();
        var service = new GeoBoundaryWarmupHostedService(provider);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.NotNull(provider.Get("England"));
    }
}