using System.Diagnostics.CodeAnalysis;

namespace AqieHistoricaldataBackend.Atomfeed.Services
{
    public interface IAuthService
    {
        [ExcludeFromCodeCoverage]
        Task<string?> GetTokenAsync(string email, string password);
        Task<string> GetRicardoToken();
    }
}
