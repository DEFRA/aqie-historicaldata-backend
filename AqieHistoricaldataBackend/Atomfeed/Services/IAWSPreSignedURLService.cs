using System.Diagnostics.CodeAnalysis;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAWSPreSignedURLService
    {
        [ExcludeFromCodeCoverage]
        Task<string> GeneratePreSignedURL(string bucketName, string keyName, double duration);
    }
}
