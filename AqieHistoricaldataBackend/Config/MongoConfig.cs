using System.Diagnostics.CodeAnalysis;

namespace AqieHistoricaldataBackend.Config;
[ExcludeFromCodeCoverage]
public class MongoConfig
{
    public string DatabaseUri { get; init; } = default!;
    public string DatabaseName { get; init; } = default!;
}