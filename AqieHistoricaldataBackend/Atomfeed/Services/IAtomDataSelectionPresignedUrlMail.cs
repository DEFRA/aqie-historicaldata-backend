using System.Diagnostics.CodeAnalysis;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAtomDataSelectionPresignedUrlMail
    {
        [ExcludeFromCodeCoverage]
        Task<string?> GetPresignedUrlMail(string jobId);
    }
}
